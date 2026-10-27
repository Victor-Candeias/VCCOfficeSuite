using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Vcc.Office.CoreShell;

public sealed record StoragePaths(string Root)
{
    public string DataDirectory => Path.Combine(Root, "data");
    public string RecoveryDirectory => Path.Combine(Root, "recovery");
    public string DatabasePath => Path.Combine(DataDirectory, "office.db");

    public static StoragePaths ForLocalApplication(string? root = null)
        => new(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VCCOffice"));
}

public sealed class MigrationRunner
{
    private readonly IReadOnlyList<(int Version, string Sql)> _migrations =
    [
        (1, """
        CREATE TABLE IF NOT EXISTS app_settings (key TEXT NOT NULL PRIMARY KEY, value_json TEXT NOT NULL, updated_at TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS recovery_sessions (id TEXT NOT NULL PRIMARY KEY, module TEXT NOT NULL, source_path TEXT NOT NULL, snapshot_path TEXT NOT NULL, status TEXT NOT NULL, created_at TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_recovery_sessions_status ON recovery_sessions(status);
        """),
        (2, """
        CREATE TABLE IF NOT EXISTS data_tables (name TEXT NOT NULL PRIMARY KEY, display_name TEXT NOT NULL, created_at TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS data_columns (table_name TEXT NOT NULL, name TEXT NOT NULL, type TEXT NOT NULL, nullable INTEGER NOT NULL, position INTEGER NOT NULL, PRIMARY KEY(table_name, name), FOREIGN KEY(table_name) REFERENCES data_tables(name) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS data_relations (name TEXT NOT NULL PRIMARY KEY, parent_table TEXT NOT NULL, parent_column TEXT NOT NULL, child_table TEXT NOT NULL, child_column TEXT NOT NULL, FOREIGN KEY(parent_table) REFERENCES data_tables(name) ON DELETE CASCADE, FOREIGN KEY(child_table) REFERENCES data_tables(name) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS data_saved_queries (name TEXT NOT NULL PRIMARY KEY, sql TEXT NOT NULL, created_at TEXT NOT NULL);
        """),
        (3, """
        CREATE TABLE IF NOT EXISTS data_forms (name TEXT NOT NULL PRIMARY KEY, table_name TEXT NOT NULL, display_name TEXT NOT NULL, created_at TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS data_form_fields (form_name TEXT NOT NULL, name TEXT NOT NULL, label TEXT NOT NULL, type TEXT NOT NULL, required INTEGER NOT NULL, position INTEGER NOT NULL, PRIMARY KEY(form_name, name), FOREIGN KEY(form_name) REFERENCES data_forms(name) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS data_reports (name TEXT NOT NULL PRIMARY KEY, display_name TEXT NOT NULL, query_sql TEXT NOT NULL, created_at TEXT NOT NULL);
        """)
    ];

    public async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER NOT NULL PRIMARY KEY, applied_at TEXT NOT NULL, checksum TEXT NOT NULL);";
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var migration in _migrations)
        {
            await using var exists = connection.CreateCommand();
            exists.Transaction = transaction;
            exists.CommandText = "SELECT 1 FROM schema_migrations WHERE version = $version";
            exists.Parameters.AddWithValue("$version", migration.Version);
            if (await exists.ExecuteScalarAsync(cancellationToken) is not null) continue;

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = migration.Sql;
            await command.ExecuteNonQueryAsync(cancellationToken);

            await using var record = connection.CreateCommand();
            record.Transaction = transaction;
            record.CommandText = "INSERT INTO schema_migrations(version, applied_at, checksum) VALUES ($version, $at, $checksum)";
            record.Parameters.AddWithValue("$version", migration.Version);
            record.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
            record.Parameters.AddWithValue("$checksum", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(migration.Sql))));
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed class CoreDatabase
{
    private readonly StoragePaths _paths;
    private readonly MigrationRunner _migrations;

    public CoreDatabase(StoragePaths paths, MigrationRunner? migrations = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _migrations = migrations ?? new MigrationRunner();
    }

    public string DatabasePath => _paths.DatabasePath;
    private string ConnectionString => $"Data Source={DatabasePath};Pooling=False";

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await _migrations.ApplyAsync(connection, cancellationToken);
    }

    public async Task SetSettingAsync(string key, string valueJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A chave é obrigatória.", nameof(key));
        ArgumentNullException.ThrowIfNull(valueJson);
        await InitialiseAsync(cancellationToken);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO app_settings(key, value_json, updated_at) VALUES ($key, $value, $at) ON CONFLICT(key) DO UPDATE SET value_json = excluded.value_json, updated_at = excluded.updated_at";
        command.Parameters.AddWithValue("$key", key.Trim());
        command.Parameters.AddWithValue("$value", valueJson);
        command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A chave é obrigatória.", nameof(key));
        await InitialiseAsync(cancellationToken);
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value_json FROM app_settings WHERE key = $key";
        command.Parameters.AddWithValue("$key", key.Trim());
        return (await command.ExecuteScalarAsync(cancellationToken)) as string;
    }
}

public sealed class AtomicFileStore
{
    public async Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("O caminho do ficheiro é obrigatório.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("O caminho não tem diretório.");
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, content.ToArray(), cancellationToken);
        File.Move(temporaryPath, fullPath, true);
    }

    public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("O caminho do ficheiro é obrigatório.", nameof(path));
        return File.ReadAllBytesAsync(Path.GetFullPath(path), cancellationToken);
    }
}

public sealed record RecoverySession(string Id, string Module, string SourcePath, string SnapshotPath, string Status, DateTimeOffset CreatedAt);

public sealed class RecoveryStore
{
    private readonly StoragePaths _paths;
    private readonly CoreDatabase _database;
    private readonly AtomicFileStore _files = new();

    public RecoveryStore(StoragePaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _database = new CoreDatabase(paths);
    }

    public async Task<RecoverySession> SaveSnapshotAsync(string module, string sourcePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(module)) throw new ArgumentException("O módulo é obrigatório.", nameof(module));
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("O caminho de origem é obrigatório.", nameof(sourcePath));
        Directory.CreateDirectory(_paths.RecoveryDirectory);
        var id = Guid.NewGuid().ToString("N");
        var snapshotPath = Path.Combine(_paths.RecoveryDirectory, $"{id}.snapshot");
        await _files.WriteAsync(snapshotPath, content, cancellationToken);
        var session = new RecoverySession(id, module.Trim(), Path.GetFullPath(sourcePath), snapshotPath, "pending", DateTimeOffset.UtcNow);
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = new SqliteConnection($"Data Source={_database.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO recovery_sessions(id, module, source_path, snapshot_path, status, created_at) VALUES ($id, $module, $source, $snapshot, $status, $created)";
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$module", session.Module);
        command.Parameters.AddWithValue("$source", session.SourcePath);
        command.Parameters.AddWithValue("$snapshot", session.SnapshotPath);
        command.Parameters.AddWithValue("$status", session.Status);
        command.Parameters.AddWithValue("$created", session.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return session;
    }

    public Task<byte[]> ReadSnapshotAsync(RecoverySession session, CancellationToken cancellationToken = default)
        => _files.ReadAsync(session.SnapshotPath, cancellationToken);
}

public sealed class AutosaveService
{
    private readonly RecoveryStore _recovery;

    public AutosaveService(RecoveryStore recovery) => _recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));

    public Task<RecoverySession> SaveAsync(string module, string sourcePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        => _recovery.SaveSnapshotAsync(module, sourcePath, content, cancellationToken);
}
