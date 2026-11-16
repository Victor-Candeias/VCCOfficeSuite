using Microsoft.Data.Sqlite;

namespace Vcc.Office.CoreShell;

public sealed record RecentDocument(
    string FullPath, string FileName, string Extension, string? ModuleId,
    DateTimeOffset OpenedAt, bool IsFavorite, bool Exists);

/// <summary>Catálogo local de ficheiros; não altera nem copia documentos do utilizador.</summary>
public sealed class RecentDocumentsService
{
    private readonly CoreDatabase _database;
    private const int MaxRecent = 200;

    public RecentDocumentsService(CoreDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    private static readonly IReadOnlyDictionary<string, string> Extensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".docx"] = "writer", [".doc"] = "writer", [".odt"] = "writer",
            [".rtf"] = "writer", [".txt"] = "writer", [".md"] = "notes",
            [".xlsx"] = "sheets", [".xls"] = "sheets", [".ods"] = "sheets", [".csv"] = "sheets",
            [".pptx"] = "slides", [".ppt"] = "slides", [".odp"] = "slides",
            [".pdf"] = "pdf", [".png"] = "images", [".jpg"] = "images",
            [".jpeg"] = "images", [".webp"] = "images",
            [".svg"] = "draw", [".eml"] = "mail", [".ics"] = "calendar",
            [".vcf"] = "contacts", [".db"] = "data", [".sqlite"] = "data"
        };

    public static string? ResolveModule(string filePath) =>
        Extensions.TryGetValue(Path.GetExtension(filePath), out var id) ? id : null;

    public async Task<RecentDocument> RegisterAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Indique o ficheiro.", nameof(path));
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("O ficheiro já não existe.", full);
        await _database.InitialiseAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO recent_documents(full_path, file_name, extension, module_id, opened_at, is_favorite)
            VALUES($path,$name,$ext,$module,$at,0)
            ON CONFLICT(full_path) DO UPDATE SET
              file_name=excluded.file_name,
              extension=excluded.extension,
              module_id=excluded.module_id,
              opened_at=excluded.opened_at;
            DELETE FROM recent_documents WHERE is_favorite=0 AND full_path NOT IN (
              SELECT full_path FROM recent_documents WHERE is_favorite=0 ORDER BY opened_at DESC LIMIT 200);
            """;
        command.Parameters.AddWithValue("$path", full);
        command.Parameters.AddWithValue("$name", Path.GetFileName(full));
        command.Parameters.AddWithValue("$ext", Path.GetExtension(full).ToLowerInvariant());
        command.Parameters.AddWithValue("$module", (object?)ResolveModule(full) ?? DBNull.Value);
        command.Parameters.AddWithValue("$at", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (await QueryAsync(search: full, cancellationToken: cancellationToken))
            .Single(item => string.Equals(item.FullPath, full, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SetFavoriteAsync(string path, bool favorite, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Indique o ficheiro.", nameof(path));
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE recent_documents SET is_favorite=$favorite WHERE full_path=$path";
        command.Parameters.AddWithValue("$path", Path.GetFullPath(path));
        command.Parameters.AddWithValue("$favorite", favorite ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecentDocument>> QueryAsync(string? search = null, string? moduleId = null,
        bool favoritesOnly = false, CancellationToken cancellationToken = default)
    {
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT full_path,file_name,extension,module_id,opened_at,is_favorite
            FROM recent_documents
            WHERE ($favorite=0 OR is_favorite=1)
              AND ($module IS NULL OR module_id=$module)
              AND ($search IS NULL OR instr(lower(file_name),lower($search))>0
                   OR instr(lower(full_path),lower($search))>0)
            ORDER BY is_favorite DESC, opened_at DESC LIMIT 200
            """;
        command.Parameters.AddWithValue("$favorite", favoritesOnly ? 1 : 0);
        command.Parameters.AddWithValue("$module", (object?)moduleId ?? DBNull.Value);
        command.Parameters.AddWithValue("$search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());
        var result = new List<RecentDocument>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var filePath = reader.GetString(0);
            result.Add(new RecentDocument(filePath, reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
                reader.GetInt64(5) != 0, File.Exists(filePath)));
        }
        return result;
    }

    public async Task RemoveAsync(string path, CancellationToken cancellationToken = default)
    {
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM recent_documents WHERE full_path=$path";
        command.Parameters.AddWithValue("$path", Path.GetFullPath(path));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_database.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
