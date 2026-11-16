using System.Text.Json;

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
    public static DesktopLauncherState Default { get; } =
        new(DashboardIds.Modern, ThemeMode.System, null, null);
}

/// <summary>Normaliza identificadores antes de os apresentar ou persistir.</summary>
public static class DesktopLauncherStatePolicy
{
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
                ? null : state.LastOpenedDocumentPath);
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

    public DesktopLauncherStateStore(CoreDatabase database, IDashboardRegistry dashboards,
        OfficeApplicationRegistry modules)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dashboards = dashboards ?? throw new ArgumentNullException(nameof(dashboards));
        _modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public async Task<DesktopLauncherState> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _database.GetSettingAsync(SettingsKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(stored)) return DesktopLauncherState.Default;
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
