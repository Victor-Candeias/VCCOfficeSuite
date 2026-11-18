using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Vcc.Office.CoreShell;

public enum OfficeNotificationSeverity { Info, Success, Warning, Error }
public enum OfficeNotificationCategory { System, Document, Plugin, Operation }

public sealed record OfficeNotification(
    string Id, OfficeNotificationCategory Category, OfficeNotificationSeverity Severity,
    string Title, string Message, string? Source, DateTimeOffset CreatedAt, bool IsRead);

/// <summary>Local notification inbox. Never stores document contents or access tokens.</summary>
public sealed class OfficeNotificationService
{
    private readonly CoreDatabase _database;
    private const int RetainedLimit = 500;

    public OfficeNotificationService(CoreDatabase database) =>
        _database = database ?? throw new ArgumentNullException(nameof(database));

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        await _database.InitialiseAsync(ct);
        var connection = new SqliteConnection($"Data Source={_database.DatabasePath};Pooling=False");
        await connection.OpenAsync(ct);
        return connection;
    }

    public async Task<OfficeNotification> PublishAsync(
        OfficeNotificationCategory category, OfficeNotificationSeverity severity,
        string title, string message, string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(category) || !Enum.IsDefined(severity))
            throw new ArgumentOutOfRangeException(nameof(category), "Tipo de notificação inválido.");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Título e mensagem são obrigatórios.");
        var item = new OfficeNotification(Guid.NewGuid().ToString("N"), category, severity,
            title.Trim()[..Math.Min(title.Trim().Length, 140)],
            message.Trim()[..Math.Min(message.Trim().Length, 2000)],
            string.IsNullOrWhiteSpace(source) ? null : source.Trim()[..Math.Min(source.Trim().Length, 100)],
            DateTimeOffset.UtcNow, false);
        await using var connection = await OpenAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO office_notifications
                    (id, category, severity, title, message, source, created_at, is_read)
                VALUES ($id, $category, $severity, $title, $message, $source, $created, 0);
                """;
            command.Parameters.AddWithValue("$id", item.Id);
            command.Parameters.AddWithValue("$category", item.Category.ToString());
            command.Parameters.AddWithValue("$severity", item.Severity.ToString());
            command.Parameters.AddWithValue("$title", item.Title);
            command.Parameters.AddWithValue("$message", item.Message);
            command.Parameters.AddWithValue("$source", (object?)item.Source ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        // Bound the local inbox without deleting unread notifications.
        await using (var prune = connection.CreateCommand())
        {
            prune.CommandText = """
                DELETE FROM office_notifications WHERE is_read=1 AND id NOT IN (
                    SELECT id FROM office_notifications ORDER BY created_at DESC LIMIT 500);
                """;
            await prune.ExecuteNonQueryAsync(cancellationToken);
        }
        return item;
    }

    public async Task<IReadOnlyList<OfficeNotification>> QueryAsync(
        bool unreadOnly = false, OfficeNotificationCategory? category = null,
        OfficeNotificationSeverity? severity = null, string? search = null,
        int limit = 250, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, category, severity, title, message, source, created_at, is_read
            FROM office_notifications
            WHERE ($unread=0 OR is_read=0)
              AND ($category IS NULL OR category=$category)
              AND ($severity IS NULL OR severity=$severity)
              AND ($search IS NULL OR instr(lower(title),lower($search))>0
                   OR instr(lower(message),lower($search))>0)
            ORDER BY created_at DESC, id DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$unread", unreadOnly ? 1 : 0);
        command.Parameters.AddWithValue("$category", category.HasValue ? category.ToString()! : DBNull.Value);
        command.Parameters.AddWithValue("$severity", severity.HasValue ? severity.ToString()! : DBNull.Value);
        command.Parameters.AddWithValue("$search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        var entries = new List<OfficeNotification>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            entries.Add(new OfficeNotification(reader.GetString(0),
                Enum.Parse<OfficeNotificationCategory>(reader.GetString(1)),
                Enum.Parse<OfficeNotificationSeverity>(reader.GetString(2)),
                reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
                reader.GetInt64(7) != 0));
        return entries;
    }

    public async Task<int> UnreadCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM office_notifications WHERE is_read=0";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    public Task MarkReadAsync(string id, bool read = true, CancellationToken cancellationToken = default) =>
        ExecuteAsync("UPDATE office_notifications SET is_read=$read WHERE id=$id",
            ("$id", id), ("$read", read ? 1 : 0), cancellationToken);

    public Task MarkAllReadAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync("UPDATE office_notifications SET is_read=1 WHERE is_read=0",
            ("$id", DBNull.Value), ("$read", 1), cancellationToken);

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default) =>
        ExecuteAsync("DELETE FROM office_notifications WHERE id=$id",
            ("$id", id), ("$read", 0), cancellationToken);

    public Task ClearReadAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync("DELETE FROM office_notifications WHERE is_read=1",
            ("$id", DBNull.Value), ("$read", 0), cancellationToken);

    private async Task ExecuteAsync(string sql, (string Key, object Value) id,
        (string Key, object Value) read, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(id.Key, id.Value);
        command.Parameters.AddWithValue(read.Key, read.Value);
        await command.ExecuteNonQueryAsync(ct);
    }
}
