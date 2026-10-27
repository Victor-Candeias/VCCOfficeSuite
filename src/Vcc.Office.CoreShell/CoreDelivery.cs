using System.Text.Json;

namespace Vcc.Office.CoreShell;

public enum DeliveryItemState { Draft, Ready, Running, Succeeded, Failed }

public sealed record CiCdDefinition(string Id, string Name, string Command, string WorkingDirectory, bool Enabled = true);
public sealed record InstallationDefinition(string Id, string Package, string Version, string TargetDirectory, bool IsPerMachine = false);
public sealed record UpdateDefinition(string Id, string Package, string CurrentVersion, string TargetVersion, string Source);
public sealed record TelemetryEvent(string Name, DateTimeOffset Timestamp, IReadOnlyDictionary<string, string> Properties);
public sealed record TestDefinition(string Id, string Name, string Command, string WorkingDirectory);
public sealed record LicenseGrant(string Id, string Product, string Subject, DateOnly? ExpiresOn, IReadOnlyList<string> Features);

public sealed record CiCdDefinitionDto(string Id, string Name, string Command, string WorkingDirectory, bool Enabled = true)
{
    public CiCdDefinition ToModel() => new(Id, Name, Command, WorkingDirectory, Enabled);
    public static CiCdDefinitionDto FromModel(CiCdDefinition value) => new(value.Id, value.Name, value.Command, value.WorkingDirectory, value.Enabled);
}

public sealed record InstallationDefinitionDto(string Id, string Package, string Version, string TargetDirectory, bool IsPerMachine = false)
{
    public InstallationDefinition ToModel() => new(Id, Package, Version, TargetDirectory, IsPerMachine);
    public static InstallationDefinitionDto FromModel(InstallationDefinition value) => new(value.Id, value.Package, value.Version, value.TargetDirectory, value.IsPerMachine);
}

public sealed record UpdateDefinitionDto(string Id, string Package, string CurrentVersion, string TargetVersion, string Source)
{
    public UpdateDefinition ToModel() => new(Id, Package, CurrentVersion, TargetVersion, Source);
    public static UpdateDefinitionDto FromModel(UpdateDefinition value) => new(value.Id, value.Package, value.CurrentVersion, value.TargetVersion, value.Source);
}

public sealed record TestDefinitionDto(string Id, string Name, string Command, string WorkingDirectory)
{
    public TestDefinition ToModel() => new(Id, Name, Command, WorkingDirectory);
    public static TestDefinitionDto FromModel(TestDefinition value) => new(value.Id, value.Name, value.Command, value.WorkingDirectory);
}

public sealed record LicenseGrantDto(string Id, string Product, string Subject, DateOnly? ExpiresOn, IReadOnlyList<string> Features)
{
    public LicenseGrant ToModel() => new(Id, Product, Subject, ExpiresOn, Features);
    public static LicenseGrantDto FromModel(LicenseGrant value) => new(value.Id, value.Product, value.Subject, value.ExpiresOn, value.Features);
}

public sealed record CoreDeliverySnapshot(
    IReadOnlyList<CiCdDefinition> Pipelines,
    IReadOnlyList<InstallationDefinition> Installations,
    IReadOnlyList<UpdateDefinition> Updates,
    IReadOnlyList<TestDefinition> Tests,
    IReadOnlyList<LicenseGrant> Licenses,
    IReadOnlyList<TelemetryEvent> Telemetry);

public static class CoreDeliveryPolicy
{
    public static ValidationResult Validate(CiCdDefinition value) => ValidateCommand(value.Id, value.Name, value.Command, value.WorkingDirectory, "pipeline CI/CD");
    public static ValidationResult Validate(InstallationDefinition value) => ValidateCommon(value.Id, value.Package, value.Version, value.TargetDirectory, "instalação");
    public static ValidationResult Validate(UpdateDefinition value) => ValidateCommon(value.Id, value.Package, value.CurrentVersion, value.TargetVersion, value.Source, "atualização");
    public static ValidationResult Validate(TestDefinition value) => ValidateCommand(value.Id, value.Name, value.Command, value.WorkingDirectory, "teste");
    public static ValidationResult Validate(LicenseGrant value)
    {
        var errors = ValidateCommon(value.Id, value.Product, value.Subject, "licença").Errors.ToList();
        if (value.ExpiresOn is not null && value.ExpiresOn < DateOnly.FromDateTime(DateTime.UtcNow)) errors.Add("A licença está expirada.");
        if (value.Features is null || value.Features.Count == 0 || value.Features.Any(string.IsNullOrWhiteSpace)) errors.Add("A licença requer pelo menos uma capacidade.");
        return new ValidationResult(errors);
    }

    private static ValidationResult ValidateCommand(string id, string name, string command, string directory, string kind)
    {
        var result = ValidateCommon(id, name, command, directory, kind).Errors.ToList();
        if (!Path.IsPathFullyQualified(directory)) result.Add($"O diretório do {kind} deve ser absoluto.");
        return new ValidationResult(result);
    }

    private static ValidationResult ValidateCommon(string id, string name, params string[] values)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(id)) errors.Add("O ID é obrigatório.");
        if (string.IsNullOrWhiteSpace(name)) errors.Add("O nome/pacote é obrigatório.");
        if (values.Any(string.IsNullOrWhiteSpace)) errors.Add("Os dados obrigatórios não podem estar vazios.");
        return new ValidationResult(errors);
    }
}

public sealed class CoreDeliveryService
{
    private readonly AtomicFileStore _files = new();
    private readonly string? _statePath;
    private readonly List<CiCdDefinition> _pipelines = [];
    private readonly List<InstallationDefinition> _installations = [];
    private readonly List<UpdateDefinition> _updates = [];
    private readonly List<TestDefinition> _tests = [];
    private readonly List<LicenseGrant> _licenses = [];
    private readonly List<TelemetryEvent> _telemetry = [];

    public CoreDeliveryService(string? statePath = null) => _statePath = statePath;
    public CoreDeliverySnapshot Snapshot => new(_pipelines.ToArray(), _installations.ToArray(), _updates.ToArray(), _tests.ToArray(), _licenses.ToArray(), _telemetry.ToArray());

    public Task<CiCdDefinition> ConfigureCiCdAsync(CiCdDefinitionDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _pipelines, CoreDeliveryPolicy.Validate, cancellationToken);
    public Task<InstallationDefinition> ConfigureInstallationAsync(InstallationDefinitionDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _installations, CoreDeliveryPolicy.Validate, cancellationToken);
    public Task<UpdateDefinition> ConfigureUpdateAsync(UpdateDefinitionDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _updates, CoreDeliveryPolicy.Validate, cancellationToken);
    public Task<TestDefinition> ConfigureTestAsync(TestDefinitionDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _tests, CoreDeliveryPolicy.Validate, cancellationToken);
    public Task<LicenseGrant> ConfigureLicenseAsync(LicenseGrantDto value, CancellationToken cancellationToken = default) => AddAsync(value.ToModel(), _licenses, CoreDeliveryPolicy.Validate, cancellationToken);
    public IReadOnlyList<T> Query<T>(string? text = null) where T : class
    {
        var source = typeof(T) == typeof(CiCdDefinition) ? _pipelines.Cast<T>() : typeof(T) == typeof(InstallationDefinition) ? _installations.Cast<T>() : typeof(T) == typeof(UpdateDefinition) ? _updates.Cast<T>() : typeof(T) == typeof(TestDefinition) ? _tests.Cast<T>() : typeof(T) == typeof(LicenseGrant) ? _licenses.Cast<T>() : throw new ArgumentException("Tipo de Core Delivery não suportado.", nameof(T));
        return string.IsNullOrWhiteSpace(text) ? source.ToArray() : source.Where(value => value.ToString()!.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    public bool HasLicense(string product, string subject, string feature) => _licenses.Any(value => value.Product.Equals(product, StringComparison.OrdinalIgnoreCase) && value.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase) && (value.ExpiresOn is null || value.ExpiresOn >= DateOnly.FromDateTime(DateTime.UtcNow)) && value.Features.Any(item => item.Equals(feature, StringComparison.OrdinalIgnoreCase)));

    public async Task RecordTelemetryAsync(string name, IReadOnlyDictionary<string, string>? properties = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("O nome do evento é obrigatório.", nameof(name));
        var safe = (properties ?? new Dictionary<string, string>()).Where(pair => !pair.Key.Contains("password", StringComparison.OrdinalIgnoreCase) && !pair.Key.Contains("token", StringComparison.OrdinalIgnoreCase) && !pair.Key.Contains("secret", StringComparison.OrdinalIgnoreCase)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _telemetry.Add(new TelemetryEvent(name.Trim(), DateTimeOffset.UtcNow, safe));
        if (!string.IsNullOrWhiteSpace(_statePath)) await SaveAsync(cancellationToken);
    }

    public DeliveryItemState GetState(string id) => string.IsNullOrWhiteSpace(id) ? DeliveryItemState.Failed : Snapshot.Pipelines.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) || Snapshot.Installations.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) || Snapshot.Updates.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) || Snapshot.Tests.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ? DeliveryItemState.Ready : DeliveryItemState.Draft;

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado.");
        var json = JsonSerializer.Serialize(Snapshot, new JsonSerializerOptions { WriteIndented = true });
        await _files.WriteAsync(_statePath, System.Text.Encoding.UTF8.GetBytes(json), cancellationToken);
    }

    private async Task<T> AddAsync<T>(T value, ICollection<T> target, Func<T, ValidationResult> validate, CancellationToken cancellationToken)
    {
        var validation = validate(value);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors));
        var id = value!.GetType().GetProperty("Id")!.GetValue(value)!.ToString()!;
        if (Snapshot.Pipelines.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) || Snapshot.Installations.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) || Snapshot.Updates.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) || Snapshot.Tests.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) || Snapshot.Licenses.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um item com esse ID.");
        target.Add(value);
        if (!string.IsNullOrWhiteSpace(_statePath)) await SaveAsync(cancellationToken);
        return value;
    }
}
