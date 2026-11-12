namespace Vcc.Office.CoreShell;

/// <summary>Identificadores estáveis para as três vistas internas do Launcher.</summary>
public static class DashboardIds
{
    public const string Modern = "modern";
    public const string Classic = "classic";
    public const string Grid = "grid";
}

public enum DashboardStartupMode { Last, Preferred }

public sealed record DashboardDefinition(string Id, string Name, string Description);

public sealed record DashboardPreferences(
    string PreferredDashboardId,
    string? LastDashboardId,
    DashboardStartupMode StartupMode,
    ThemeMode Theme)
{
    public static DashboardPreferences Default { get; } =
        new(DashboardIds.Modern, null, DashboardStartupMode.Last, ThemeMode.System);
}

public interface IDashboardRegistry
{
    IReadOnlyList<DashboardDefinition> GetAll();
    bool Contains(string? id);
}

public sealed class DashboardRegistry : IDashboardRegistry
{
    private readonly Dictionary<string, DashboardDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public DashboardRegistry(IEnumerable<DashboardDefinition>? legacy = null)
    {
        Register(new(DashboardIds.Modern, "Moderno", "Painel de produtividade"));
        Register(new(DashboardIds.Classic, "Clássico", "Interface original do VCC Office"));
        Register(new(DashboardIds.Grid, "Aplicações", "Grelha das 26 aplicações"));
        if (legacy is not null)
            foreach (var dashboard in legacy) Register(dashboard);
    }

    public void Register(DashboardDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id) ||
            (definition.Id != DashboardIds.Modern &&
             definition.Id != DashboardIds.Classic &&
             definition.Id != DashboardIds.Grid &&
             !definition.Id.StartsWith("legacy:", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Identificador de dashboard inválido.", nameof(definition));
        _definitions[definition.Id] = definition;
    }

    public IReadOnlyList<DashboardDefinition> GetAll() => _definitions.Values.ToArray();
    public bool Contains(string? id) => id is not null && _definitions.ContainsKey(id);
}

public static class DashboardPolicy
{
    public static string ResolveStartup(DashboardPreferences? settings, IDashboardRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        settings ??= DashboardPreferences.Default;
        var requested = settings.StartupMode == DashboardStartupMode.Last
            ? settings.LastDashboardId
            : settings.PreferredDashboardId;
        if (registry.Contains(requested)) return requested!;
        if (registry.Contains(settings.PreferredDashboardId)) return settings.PreferredDashboardId;
        if (registry.Contains(DashboardIds.Modern)) return DashboardIds.Modern;
        return registry.GetAll().FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("Não existem dashboards disponíveis.");
    }

    public static DashboardPreferences Select(DashboardPreferences settings, string dashboardId, IDashboardRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.Contains(dashboardId))
            throw new ArgumentException("Dashboard inexistente ou desativado.", nameof(dashboardId));
        return settings with { LastDashboardId = dashboardId };
    }

    public static DashboardPreferences ValidateOrDefault(DashboardPreferences? candidate, IDashboardRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        candidate ??= DashboardPreferences.Default;
        var defaults = DashboardPreferences.Default;
        return candidate with
        {
            PreferredDashboardId = registry.Contains(candidate.PreferredDashboardId)
                ? candidate.PreferredDashboardId : defaults.PreferredDashboardId,
            LastDashboardId = registry.Contains(candidate.LastDashboardId)
                ? candidate.LastDashboardId : null,
            StartupMode = Enum.IsDefined(candidate.StartupMode) ? candidate.StartupMode : defaults.StartupMode,
            Theme = Enum.IsDefined(candidate.Theme) ? candidate.Theme : defaults.Theme
        };
    }
}
