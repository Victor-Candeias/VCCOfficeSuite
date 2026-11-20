using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Vcc.Office.CoreShell;

/// <summary>
/// Estado da sessão do Launcher multiplataforma. Não representa editores abertos:
/// os editores Avalonia ainda estão em migração.
/// </summary>
public sealed record DesktopLauncherState(
    string DashboardId,
    ThemeMode Theme,
    string? LastSelectedModuleId,
    string? LastOpenedDocumentPath)
{
    public string PreferredDashboardId { get; init; } = DashboardIds.Modern;
    public DashboardStartupMode StartupMode { get; init; } = DashboardStartupMode.Last;
    public bool CompactNavigation { get; init; }
    public bool ShowWelcomeBanner { get; init; } = true;
    public string[] ApplicationOrder { get; init; } = [];
    public string[] FavoriteApplications { get; init; } = [];
    public string[] HiddenApplications { get; init; } = [];
    public string CardSize { get; init; } = "medium";
    public string VisualDensity { get; init; } = "comfortable";
    public bool SidebarCollapsed { get; init; };
    public string DashboardStyle { get; init; } = "standard";
    public string? ActiveProfileName { get; init; }

    public static DesktopLauncherState Default { get; } =
        new(DashboardIds.Modern, ThemeMode.System, null, null);
}

/// <summary>Normaliza identificadores antes de os apresentar ou persistir.</summary>
public static class DesktopLauncherStatePolicy
{
    /// <summary>Resolves last-vs-preferred startup consistently with the original WPF policy.</summary>
    public static string ResolveStartup(DesktopLauncherState state, IDashboardRegistry dashboards) =>
        DashboardPolicy.ResolveStartup(new DashboardPreferences(
            state.PreferredDashboardId, state.DashboardId, state.StartupMode, state.Theme), dashboards);

    public static DesktopLauncherState Normalize(
        DesktopLauncherState? state,
        IDashboardRegistry dashboards,
        OfficeApplicationRegistry modules)
    {
        ArgumentNullException.ThrowIfNull(dashboards);
        ArgumentNullException.ThrowIfNull(modules);
        state ??= DesktopLauncherState.Default;
        return new DesktopLauncherState(
            dashboards.Contains(state.DashboardId) ? state.DashboardId : DashboardIds.Modern,
            Enum.IsDefined(state.Theme) ? state.Theme : ThemeMode.System,
            modules.Find(state.LastSelectedModuleId)?.Id,
            string.IsNullOrWhiteSpace(state.LastOpenedDocumentPath)
                ? null : state.LastOpenedDocumentPath)
        {
            PreferredDashboardId = dashboards.Contains(state.PreferredDashboardId)
                ? state.PreferredDashboardId : DashboardIds.Modern,
            StartupMode = Enum.IsDefined(state.StartupMode) ? state.StartupMode : DashboardStartupMode.Last,
            CompactNavigation = state.CompactNavigation,
            ShowWelcomeBanner = state.ShowWelcomeBanner,
            ApplicationOrder = LauncherCustomizationPolicy.NormalizeIds(state.ApplicationOrder),
            FavoriteApplications = LauncherCustomizationPolicy.NormalizeIds(state.FavoriteApplications),
            HiddenApplications = LauncherCustomizationPolicy.NormalizeIds(state.HiddenApplications),
            CardSize = state.CardSize is "small" or "medium" or "large" ? state.CardSize : "medium",
            VisualDensity = state.VisualDensity is "comfortable" or "compact" ? state.VisualDensity : "comfortable",
            SidebarCollapsed = state.SidebarCollapsed,
            DashboardStyle = state.DashboardStyle is "standard" or "minimal" ? state.DashboardStyle : "standard",
            ActiveProfileName = LauncherProfilePolicy.IsValidName(state.ActiveProfileName) ? state.ActiveProfileName : null
        };
    }
}

/// <summary>
/// Persiste preferências do Launcher na base SQLite partilhada com RecentDocumentsService.
/// A chave é distinta de settings de WPF e não modifica ficheiros originais.
/// </summary>
public sealed class DesktopLauncherStateStore
{
    private const string SettingsKey = "avalonia.launcher.session.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CoreDatabase _database;
    private readonly IDashboardRegistry _dashboards;
    private readonly OfficeApplicationRegistry _modules;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _legacyPath;
    private readonly string _legacyUser;

    public DesktopLauncherStateStore(CoreDatabase database, IDashboardRegistry dashboards,
        OfficeApplicationRegistry modules, string? legacyPath = null, string? legacyUser = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dashboards = dashboards ?? throw new ArgumentNullException(nameof(dashboards));
        _modules = modules ?? throw new ArgumentNullException(nameof(modules));
        _legacyPath = legacyPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCC Office", "dashboards.sqlite");
        _legacyUser = legacyUser ?? Environment.UserName;
    }

    public async Task<DesktopLauncherState> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _database.GetSettingAsync(SettingsKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(stored))
        {
            var legacy = LegacyDashboardPreferencesReader.TryRead(_legacyPath, _legacyUser, _dashboards);
            return legacy is null ? DesktopLauncherState.Default : new DesktopLauncherState(
                legacy.LastDashboardId ?? DashboardPolicy.ResolveStartup(legacy, _dashboards), legacy.Theme, null, null)
            {
                PreferredDashboardId = legacy.PreferredDashboardId,
                StartupMode = legacy.StartupMode
            };
        }
        try
        {
            return DesktopLauncherStatePolicy.Normalize(
                JsonSerializer.Deserialize<DesktopLauncherState>(stored, JsonOptions), _dashboards, _modules);
        }
        catch (JsonException)
        {
            return DesktopLauncherState.Default;
        }
    }

    public async Task SaveAsync(DesktopLauncherState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var normalized = DesktopLauncherStatePolicy.Normalize(state, _dashboards, _modules);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await _database.SetSettingAsync(
                SettingsKey, JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// Read-only WPF dashboard settings import. Never creates or modifies the legacy DB.
/// Avalonia-specific preferences take precedence if they already exist.
/// </summary>
public static class LegacyDashboardPreferencesReader
{
    public static DashboardPreferences? TryRead(string path, string userId, IDashboardRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(userId) || !File.Exists(path))
            return null;
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(path),
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT PreferredDashboardId, LastDashboardId, StartupMode, Theme FROM DashboardSettings WHERE UserId = $user LIMIT 1";
            command.Parameters.AddWithValue("$user", userId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            var startup = Enum.TryParse<DashboardStartupMode>(reader.GetString(2), true, out var selectedStartup)
                ? selectedStartup : DashboardStartupMode.Last;
            var theme = Enum.TryParse<ThemeMode>(reader.GetString(3), true, out var selectedTheme)
                ? selectedTheme : ThemeMode.System;
            return DashboardPolicy.ValidateOrDefault(new DashboardPreferences(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                startup, theme), registry);
        }
        catch (SqliteException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
