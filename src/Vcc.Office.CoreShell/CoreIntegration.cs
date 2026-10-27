using System.Text.Json;

namespace Vcc.Office.CoreShell;

public sealed record PluginContract(string Id, string Name, string Version, string EntryPoint, bool Enabled = true);
public sealed record EventDefinition(string Id, string Name, string PayloadSchema, bool Enabled = true);
public sealed record WebViewBridge(string Id, string Origin, IReadOnlyList<string> AllowedMethods);
public sealed record PermissionGrant(string Id, string Subject, string Resource, IReadOnlyList<string> Actions);
public sealed record WorkspaceDefinition(string Id, string Name, string RootPath);

public sealed record PluginContractDto(string Id, string Name, string Version, string EntryPoint, bool Enabled = true)
{
    public PluginContract ToModel() => new(Id, Name, Version, EntryPoint, Enabled);
    public static PluginContractDto FromModel(PluginContract value) => new(value.Id, value.Name, value.Version, value.EntryPoint, value.Enabled);
}

public sealed record EventDefinitionDto(string Id, string Name, string PayloadSchema, bool Enabled = true)
{
    public EventDefinition ToModel() => new(Id, Name, PayloadSchema, Enabled);
    public static EventDefinitionDto FromModel(EventDefinition value) => new(value.Id, value.Name, value.PayloadSchema, value.Enabled);
}

public sealed record WebViewBridgeDto(string Id, string Origin, IReadOnlyList<string> AllowedMethods)
{
    public WebViewBridge ToModel() => new(Id, Origin, AllowedMethods);
    public static WebViewBridgeDto FromModel(WebViewBridge value) => new(value.Id, value.Origin, value.AllowedMethods);
}

public sealed record PermissionGrantDto(string Id, string Subject, string Resource, IReadOnlyList<string> Actions)
{
    public PermissionGrant ToModel() => new(Id, Subject, Resource, Actions);
    public static PermissionGrantDto FromModel(PermissionGrant value) => new(value.Id, value.Subject, value.Resource, value.Actions);
}

public sealed record WorkspaceDefinitionDto(string Id, string Name, string RootPath)
{
    public WorkspaceDefinition ToModel() => new(Id, Name, RootPath);
    public static WorkspaceDefinitionDto FromModel(WorkspaceDefinition value) => new(value.Id, value.Name, value.RootPath);
}

public sealed record BridgeRequest(string BridgeId, string Origin, string Method, string Payload);
public sealed record BridgeResponse(bool Accepted, string? Error);
public enum IntegrationItemState { Draft, Ready, Invalid }

public sealed record CoreIntegrationSnapshot(
    IReadOnlyList<PluginContract> Plugins,
    IReadOnlyList<EventDefinition> Events,
    IReadOnlyList<WebViewBridge> Bridges,
    IReadOnlyList<PermissionGrant> Permissions,
    IReadOnlyList<WorkspaceDefinition> Workspaces);

public static class CoreIntegrationPolicy
{
    public static ValidationResult Validate(PluginContract value) => ValidateCommon(value.Id, value.Name, "contrato de plugin", value.Version, value.EntryPoint);
    public static ValidationResult Validate(EventDefinition value) => ValidateCommon(value.Id, value.Name, "evento", value.PayloadSchema);
    public static ValidationResult Validate(WebViewBridge value)
    {
        var errors = ValidateCommon(value.Id, value.Origin, "bridge").Errors.ToList();
        if (!Uri.TryCreate(value.Origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) errors.Add("A origem do bridge deve ser uma URI HTTP(S).");
        if (value.AllowedMethods is null || value.AllowedMethods.Count == 0 || value.AllowedMethods.Any(string.IsNullOrWhiteSpace)) errors.Add("O bridge requer pelo menos um método válido.");
        return new ValidationResult(errors);
    }
    public static ValidationResult Validate(PermissionGrant value)
    {
        var errors = ValidateCommon(value.Id, value.Subject, "permissão", value.Resource).Errors.ToList();
        if (value.Actions is null || value.Actions.Count == 0 || value.Actions.Any(string.IsNullOrWhiteSpace)) errors.Add("A permissão requer pelo menos uma ação.");
        return new ValidationResult(errors);
    }
    public static ValidationResult Validate(WorkspaceDefinition value)
    {
        var errors = ValidateCommon(value.Id, value.Name, "workspace", value.RootPath).Errors.ToList();
        if (!Path.IsPathFullyQualified(value.RootPath)) errors.Add("O caminho do workspace deve ser absoluto.");
        return new ValidationResult(errors);
    }

    public static ValidationResult Validate(PluginContractDto value) => Validate(value.ToModel());
    public static ValidationResult Validate(EventDefinitionDto value) => Validate(value.ToModel());
    public static ValidationResult Validate(WebViewBridgeDto value) => Validate(value.ToModel());
    public static ValidationResult Validate(PermissionGrantDto value) => Validate(value.ToModel());
    public static ValidationResult Validate(WorkspaceDefinitionDto value) => Validate(value.ToModel());

    private static ValidationResult ValidateCommon(string id, string name, string kind, params string[] values)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(id)) errors.Add($"O ID do {kind} é obrigatório.");
        if (string.IsNullOrWhiteSpace(name)) errors.Add($"O nome do {kind} é obrigatório.");
        if (values.Any(string.IsNullOrWhiteSpace)) errors.Add($"Os dados do {kind} não podem estar vazios.");
        return new ValidationResult(errors);
    }
}

public sealed class CoreIntegrationService
{
    private readonly AtomicFileStore _files = new();
    private readonly string? _statePath;
    private readonly List<PluginContract> _plugins = [];
    private readonly List<EventDefinition> _events = [];
    private readonly List<WebViewBridge> _bridges = [];
    private readonly List<PermissionGrant> _permissions = [];
    private readonly List<WorkspaceDefinition> _workspaces = [];

    public CoreIntegrationService(string? statePath = null) => _statePath = statePath;

    public CoreIntegrationSnapshot Snapshot => new(_plugins.ToArray(), _events.ToArray(), _bridges.ToArray(), _permissions.ToArray(), _workspaces.ToArray());

    public IntegrationItemState GetState(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return IntegrationItemState.Invalid;
        return Snapshot.Plugins.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ||
               Snapshot.Events.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ||
               Snapshot.Bridges.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ||
               Snapshot.Permissions.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ||
               Snapshot.Workspaces.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ? IntegrationItemState.Ready : IntegrationItemState.Draft;
    }

    public Task<PluginContract> ConfigurePluginAsync(PluginContractDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _plugins, CoreIntegrationPolicy.Validate, cancellationToken);
    public Task<EventDefinition> ConfigureEventAsync(EventDefinitionDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _events, CoreIntegrationPolicy.Validate, cancellationToken);
    public Task<WebViewBridge> ConfigureBridgeAsync(WebViewBridgeDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _bridges, CoreIntegrationPolicy.Validate, cancellationToken);
    public Task<PermissionGrant> ConfigurePermissionAsync(PermissionGrantDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _permissions, CoreIntegrationPolicy.Validate, cancellationToken);
    public Task<WorkspaceDefinition> ConfigureWorkspaceAsync(WorkspaceDefinitionDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _workspaces, CoreIntegrationPolicy.Validate, cancellationToken);

    public IReadOnlyList<T> Query<T>(string? text = null) where T : class
    {
        var source = typeof(T) == typeof(PluginContract) ? _plugins.Cast<T>() : typeof(T) == typeof(EventDefinition) ? _events.Cast<T>() : typeof(T) == typeof(WebViewBridge) ? _bridges.Cast<T>() : typeof(T) == typeof(PermissionGrant) ? _permissions.Cast<T>() : typeof(T) == typeof(WorkspaceDefinition) ? _workspaces.Cast<T>() : throw new ArgumentException("Tipo de Core Integration não suportado.", nameof(T));
        return string.IsNullOrWhiteSpace(text) ? source.ToArray() : source.Where(value => value.ToString()!.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public BridgeResponse HandleBridgeRequest(BridgeRequest request)
    {
        var bridge = _bridges.FirstOrDefault(item => item.Id.Equals(request.BridgeId, StringComparison.OrdinalIgnoreCase));
        if (bridge is null) return new(false, "O bridge não existe.");
        if (!bridge.Origin.Equals(request.Origin, StringComparison.OrdinalIgnoreCase)) return new(false, "A origem não está autorizada.");
        if (!bridge.AllowedMethods.Any(method => method.Equals(request.Method, StringComparison.OrdinalIgnoreCase))) return new(false, "O método não está autorizado.");
        return new(true, null);
    }

    public bool HasPermission(string subject, string resource, string action) => _permissions.Any(value => value.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase) && value.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) && value.Actions.Any(item => item.Equals(action, StringComparison.OrdinalIgnoreCase)));

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado.");
        var json = JsonSerializer.Serialize(Snapshot, new JsonSerializerOptions { WriteIndented = true });
        await _files.WriteAsync(_statePath, System.Text.Encoding.UTF8.GetBytes(json), cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado.");
        if (!File.Exists(_statePath)) return;
        var json = await File.ReadAllTextAsync(_statePath, cancellationToken);
        var snapshot = JsonSerializer.Deserialize<CoreIntegrationSnapshot>(json) ?? throw new InvalidDataException("O estado de Core Integration está vazio ou inválido.");
        _plugins.Clear(); _events.Clear(); _bridges.Clear(); _permissions.Clear(); _workspaces.Clear();
        foreach (var item in snapshot.Plugins) AddLoaded(item, _plugins, CoreIntegrationPolicy.Validate);
        foreach (var item in snapshot.Events) AddLoaded(item, _events, CoreIntegrationPolicy.Validate);
        foreach (var item in snapshot.Bridges) AddLoaded(item, _bridges, CoreIntegrationPolicy.Validate);
        foreach (var item in snapshot.Permissions) AddLoaded(item, _permissions, CoreIntegrationPolicy.Validate);
        foreach (var item in snapshot.Workspaces) AddLoaded(item, _workspaces, CoreIntegrationPolicy.Validate);
    }

    private async Task<T> AddAsync<T>(T value, ICollection<T> target, Func<T, ValidationResult> validate, CancellationToken cancellationToken)
    {
        var validation = validate(value);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors));
        var id = value!.GetType().GetProperty("Id")!.GetValue(value)!.ToString()!;
        if (target.Any(item => item!.GetType().GetProperty("Id")!.GetValue(item)!.ToString()!.Equals(id, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um item com esse ID.");
        target.Add(value);
        if (!string.IsNullOrWhiteSpace(_statePath)) await SaveAsync(cancellationToken);
        return value;
    }

    private static void AddLoaded<T>(T value, ICollection<T> target, Func<T, ValidationResult> validate)
    {
        var validation = validate(value);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
        target.Add(value);
    }
}
