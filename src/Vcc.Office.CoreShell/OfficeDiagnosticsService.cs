using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Vcc.Office.CoreShell;

public enum DiagnosticStatus { Healthy, Warning, Error }
public sealed record DiagnosticResult(string Id, string Title, DiagnosticStatus Status, string Details);
public sealed record DiagnosticReport(DateTimeOffset CheckedAt, IReadOnlyList<DiagnosticResult> Checks)
{
    public bool HasErrors => Checks.Any(check => check.Status == DiagnosticStatus.Error);
}

/// <summary>Read-only local diagnostics; repair is never performed during a scan.</summary>
public sealed class OfficeDiagnosticsService
{
    private readonly StoragePaths _paths;
    private readonly string _shellPath;
    private readonly string _sessionPath;

    public OfficeDiagnosticsService(StoragePaths paths, string shellPath, string sessionPath)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _shellPath = Path.GetFullPath(shellPath);
        _sessionPath = Path.GetFullPath(sessionPath);
    }

    public async Task<DiagnosticReport> ScanAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<DiagnosticResult>();
        checks.Add(new DiagnosticResult("storage", "Diretório de dados",
            Directory.Exists(_paths.DataDirectory) ? DiagnosticStatus.Healthy : DiagnosticStatus.Warning,
            Directory.Exists(_paths.DataDirectory) ? "Diretório disponível." : "Será criado quando necessário."));
        checks.Add(await CheckDatabaseAsync(cancellationToken));
        checks.Add(await CheckJsonAsync("shell", "Estado da interface", _shellPath, cancellationToken));
        checks.Add(await CheckJsonAsync("session", "Sessão das aplicações", _sessionPath, cancellationToken));
        checks.Add(await CheckSnapshotsAsync(cancellationToken));
        return new DiagnosticReport(DateTimeOffset.UtcNow, checks);
    }

    private async Task<DiagnosticResult> CheckDatabaseAsync(CancellationToken ct)
    {
        if (!File.Exists(_paths.DatabasePath))
            return new("database", "Base de dados local", DiagnosticStatus.Warning, "Ainda não existe base de dados.");
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _paths.DatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            await using var connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            var result = (string?)await command.ExecuteScalarAsync(ct);
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)
                ? new("database", "Base de dados local", DiagnosticStatus.Healthy, "Integridade SQLite validada.")
                : new("database", "Base de dados local", DiagnosticStatus.Error, "A verificação SQLite detetou problemas.");
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new("database", "Base de dados local", DiagnosticStatus.Error, "Não foi possível verificar a base de dados: " + ex.Message);
        }
    }

    private static async Task<DiagnosticResult> CheckJsonAsync(string id, string title, string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return new(id, title, DiagnosticStatus.Warning, "Não existe estado guardado.");
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new(id, title, DiagnosticStatus.Error, "O ficheiro não contém um objeto JSON.");
            return new(id, title, DiagnosticStatus.Healthy, "Ficheiro JSON legível.");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new(id, title, DiagnosticStatus.Error,
                "Estado inválido ou inacessível. " + (File.Exists(path + ".bak")
                    ? "Existe um backup potencialmente recuperável." : "Não existe backup."));
        }
    }

    private async Task<DiagnosticResult> CheckSnapshotsAsync(CancellationToken ct)
    {
        if (!File.Exists(_paths.DatabasePath))
            return new("snapshots", "Cópias de recuperação", DiagnosticStatus.Warning, "Sem histórico de recuperação.");
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _paths.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            };
            await using var connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM recovery_sessions WHERE status='pending'";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            return new("snapshots", "Cópias de recuperação", DiagnosticStatus.Healthy,
                count == 0 ? "Sem cópias pendentes." : count + " cópia(s) pendente(s) de recuperação.");
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new("snapshots", "Cópias de recuperação", DiagnosticStatus.Warning,
                "Não foi possível consultar o histórico: " + ex.Message);
        }
    }

    /// <summary>Restores only a validated shell backup, preserving the current file as an independent recovery copy.</summary>
    public async Task RestoreShellBackupAsync(CancellationToken cancellationToken = default)
    {
        var backup = _shellPath + ".bak";
        if (!File.Exists(backup)) throw new FileNotFoundException("Não existe backup do estado.", backup);
        // LoadAsync falls back to defaults; validate the exact backup to avoid silently restoring defaults.
        await using (var stream = File.OpenRead(backup))
        {
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("O backup do estado não é válido.");
            var loaded = await System.Text.Json.JsonSerializer.DeserializeAsync<ShellState>(
                new MemoryStream(await File.ReadAllBytesAsync(backup, cancellationToken)),
                new JsonSerializerOptions(JsonSerializerDefaults.Web) {
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }, cancellationToken);
            if (loaded is null || !ShellStateValidator.Validate(loaded).IsValid)
                throw new InvalidDataException("O backup do estado não passou na validação.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(_shellPath)!);
        if (File.Exists(_shellPath))
            File.Copy(_shellPath, _shellPath + ".before-recovery-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), false);
        // Do not use ShellStateStore.SaveAsync here: it would overwrite the validated .bak with a corrupt current file.
        await new AtomicFileStore().WriteAsync(_shellPath,
            await File.ReadAllBytesAsync(backup, cancellationToken), cancellationToken);
    }
}
