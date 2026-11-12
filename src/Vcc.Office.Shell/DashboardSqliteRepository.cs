using System.IO;
using Microsoft.Data.Sqlite;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Shell;

/// <summary>Preferências por perfil Windows; não altera o shell-state.json existente.</summary>
public sealed class DashboardSqliteRepository
{
    private readonly string _databasePath;

    public DashboardSqliteRepository(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
    }

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath, Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS DashboardSettings (
                UserId TEXT PRIMARY KEY,
                PreferredDashboardId TEXT NOT NULL DEFAULT 'modern',
                LastDashboardId TEXT,
                StartupMode TEXT NOT NULL DEFAULT 'last',
                Theme TEXT NOT NULL DEFAULT 'system',
                UpdatedAt TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        return connection;
    }

    public DashboardPreferences Load(string userId, IDashboardRegistry registry)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PreferredDashboardId, LastDashboardId, StartupMode, Theme
            FROM DashboardSettings WHERE UserId = $user;
            """;
        command.Parameters.AddWithValue("$user", userId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return DashboardPreferences.Default;
        var startup = Enum.TryParse<DashboardStartupMode>(reader.GetString(2), true, out var parsedStartup)
            ? parsedStartup : DashboardStartupMode.Last;
        var theme = Enum.TryParse<ThemeMode>(reader.GetString(3), true, out var parsedTheme)
            ? parsedTheme : ThemeMode.System;
        return DashboardPolicy.ValidateOrDefault(new DashboardPreferences(
            reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
            startup, theme), registry);
    }

    public void Save(string userId, DashboardPreferences preferences)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DashboardSettings
                (UserId, PreferredDashboardId, LastDashboardId, StartupMode, Theme, UpdatedAt)
            VALUES ($user, $preferred, $last, $startup, $theme, $updated)
            ON CONFLICT(UserId) DO UPDATE SET
                PreferredDashboardId=excluded.PreferredDashboardId,
                LastDashboardId=excluded.LastDashboardId,
                StartupMode=excluded.StartupMode,
                Theme=excluded.Theme,
                UpdatedAt=excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$user", userId);
        command.Parameters.AddWithValue("$preferred", preferences.PreferredDashboardId);
        command.Parameters.AddWithValue("$last", (object?)preferences.LastDashboardId ?? DBNull.Value);
        command.Parameters.AddWithValue("$startup", preferences.StartupMode.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$theme", preferences.Theme.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
