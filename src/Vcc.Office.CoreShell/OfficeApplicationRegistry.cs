namespace Vcc.Office.CoreShell;

/// <summary>Identidade e disponibilidade reais dos módulos do Launcher.</summary>
public sealed record OfficeModule(string Id, string Name, bool IsAvailable);

public sealed class OfficeApplicationRegistry
{
    private static readonly string[] ModuleIds =
    [
        "sheets", "slides", "mail", "calendar", "notes", "pdf", "draw",
        "projects", "publisher", "planner", "images", "analytics", "forms",
        "contacts", "data", "mindmap", "whiteboard", "scan", "convert", "backup",
        "automate", "templates", "math", "archive", "time"
    ];

    private readonly Dictionary<string, OfficeModule> _modules;
    private readonly List<string> _externalIds = [];

    public OfficeApplicationRegistry(IEnumerable<OfficeModule>? registered = null, bool includeLegacyAvailability = true)
    {
        _modules = ModuleIds.ToDictionary(
            id => id,
            id => new OfficeModule(id, "VCC " + id, false),
            StringComparer.OrdinalIgnoreCase);

        // Compatibility only: the Launcher uses includeLegacyAvailability: false.
        if (includeLegacyAvailability)
            Register(new OfficeModule("data", "VCC Data", true));
        if (registered is not null)
            foreach (var module in registered) Register(module);
    }

    public IReadOnlyList<OfficeModule> GetAll() => ModuleIds.Concat(_externalIds).Select(id => _modules[id]).ToArray();

    public OfficeModule? Find(string? id) =>
        id is not null && _modules.TryGetValue(id.Trim(), out var module) ? module : null;

    /// <summary>Install a dynamically discovered plugin, including IDs not in the legacy catalogue.</summary>
    public void RegisterPlugin(OfficeModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (string.IsNullOrWhiteSpace(module.Id) || string.IsNullOrWhiteSpace(module.Name))
            throw new ArgumentException("O plugin deve fornecer ID e nome.", nameof(module));
        if (!_modules.ContainsKey(module.Id)) _externalIds.Add(module.Id);
        _modules[module.Id] = module;
    }

    public void Register(OfficeModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (string.IsNullOrWhiteSpace(module.Id) || !_modules.ContainsKey(module.Id) ||
            string.IsNullOrWhiteSpace(module.Name))
            throw new ArgumentException("Módulo desconhecido ou inválido.", nameof(module));
        _modules[module.Id] = module;
    }
}

/// <summary>
/// Encaminha pedidos de abertura sem atribuir disponibilidade a editores não implementados.
/// </summary>
public sealed class OfficeApplicationManager
{
    private readonly OfficeApplicationRegistry _registry;

    public OfficeApplicationManager(OfficeApplicationRegistry registry) =>
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public bool TryResolve(string? id, out OfficeModule? module)
    {
        module = _registry.Find(id);
        return module?.IsAvailable == true;
    }
}
