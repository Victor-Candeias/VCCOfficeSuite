using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia.Controls;
using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.PluginHost;

/// <summary>Describes a loaded plugin and its direct managed assembly references.</summary>
public sealed record OfficePluginInventoryItem(string Id, string Name, string Version,
    bool Enabled, IReadOnlyList<string> Dependencies)
{
    public IReadOnlyList<string> RequiredPlugins { get; init; } = [];
    public string? LoadFailure { get; init; }
}

/// <summary>Loads trusted, separately compiled modules from Plugins/<id>/plugin.json.</summary>
public sealed class OfficePluginHost
{
    private readonly Dictionary<string, IOfficePlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginPackageMetadata> _manifests = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _errors = [];
    private readonly Dictionary<string, string> _failedPlugins = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _settingsPath;

    private OfficePluginHost(string rootDirectory, string settingsPath)
    {
        RootDirectory = rootDirectory;
        _settingsPath = settingsPath;
        try
        {
            if (File.Exists(_settingsPath))
                _disabled.UnionWith(JsonSerializer.Deserialize<string[]>(File.ReadAllText(_settingsPath)) ?? []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _errors.Add("Estado dos plugins: " + ex.Message);
        }
    }

    public string RootDirectory { get; }
    public IReadOnlyCollection<IOfficePlugin> Plugins => _plugins.Values.Where(p => !_disabled.Contains(p.Id)).ToArray();
    public IReadOnlyCollection<IOfficePlugin> InstalledPlugins => _plugins.Values.ToArray();
    public IReadOnlyList<string> Errors => _errors;

    public IReadOnlyList<OfficePluginInventoryItem> GetInventory() =>
        _plugins.Values.Select(p => new OfficePluginInventoryItem(p.Id, p.DisplayName,
                p.GetType().Assembly.GetName().Version?.ToString() ?? "0.0",
                !_disabled.Contains(p.Id),
                p.GetType().Assembly.GetReferencedAssemblies()
                    .Select(reference => reference.Name ?? "")
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray())
            {
                RequiredPlugins = _manifests.TryGetValue(p.Id, out var manifest)
                    ? (manifest.Requires ?? []).Select(dep => dep.Id).ToArray()
                    : []
            })
            .Concat(_failedPlugins.Where(pair => !_plugins.ContainsKey(pair.Key))
                .Select(pair => new OfficePluginInventoryItem(pair.Key, pair.Key, "indisponível",
                    false, []) { LoadFailure = pair.Value }))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool IsEnabled(string id) => _plugins.ContainsKey(id) && !_disabled.Contains(id);

    /// <summary>Persist controlled activation; assemblies unload when the process exits.</summary>
    public void SetEnabled(string id, bool enabled)
    {
        if (!_plugins.ContainsKey(id)) throw new ArgumentException("Plugin não instalado.", nameof(id));
        if (!enabled)
        {
            var dependent = _manifests.Values.FirstOrDefault(manifest =>
                !_disabled.Contains(manifest.Id) &&
                (manifest.Requires ?? []).Any(dep => dep.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
            if (dependent is not null)
                throw new InvalidOperationException("O plugin " + dependent.Id + " depende de " + id + ".");
        }
        var next = new HashSet<string>(_disabled, StringComparer.OrdinalIgnoreCase);
        if (enabled) next.Remove(id);
        else next.Add(id);
        var folder = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(folder);
        var temp = _settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(next.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
            File.Move(temp, _settingsPath, overwrite: true);
            _disabled.Clear();
            _disabled.UnionWith(next);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static OfficePluginHost Discover(string? root = null, string? settingsPath = null)
    {
        var directory = root ?? Path.Combine(AppContext.BaseDirectory, "Plugins");
        var state = settingsPath ?? (root is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VCC Office", "plugins-state.json")
            : Path.Combine(directory, ".plugins-state.json"));
        var host = new OfficePluginHost(directory, state);
        if (Directory.Exists(directory))
        {
            try
            {
                foreach (var error in new OfficePluginPackageManager(directory).ApplyPending())
                    host._errors.Add("Atualização pendente: " + error);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                       ArgumentException or JsonException)
            {
                host._errors.Add("Instalação de plugins: " + ex.Message);
            }
        }
        if (!Directory.Exists(directory))
            return host;

        var pending = Directory.EnumerateFiles(directory, "plugin.json", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(directory, path).Split(Path.DirectorySeparatorChar)
                .Contains(".vcc-updates", StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();

        // Resolve dependencies before loading their dependants, independent of folder name.
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var manifestPath in pending.ToArray())
            {
                try
                {
                    var manifest = PluginCompatibilityPolicy.Read(File.ReadAllText(manifestPath));
                    if ((manifest.Requires ?? []).Any(dep => !host._plugins.ContainsKey(dep.Id)))
                        continue;
                    host.Register(manifestPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                           JsonException or InvalidDataException or InvalidOperationException or
                                           ArgumentException or FileLoadException or BadImageFormatException or
                                           TypeLoadException or TargetInvocationException)
                {
                    var name = Path.GetFileName(Path.GetDirectoryName(manifestPath));
                    host._errors.Add(name + ": " + ex.Message);
                    if (!string.IsNullOrWhiteSpace(name))
                        host._failedPlugins[name.ToLowerInvariant()] = ex.Message;
                }
                pending.Remove(manifestPath);
                progressed = true;
            }
            if (progressed) continue;
            foreach (var remaining in pending)
            {
                var name = Path.GetFileName(Path.GetDirectoryName(remaining));
                var error = "Dependências de plugin ausentes ou circulares.";
                host._errors.Add(name + ": " + error);
                if (!string.IsNullOrWhiteSpace(name)) host._failedPlugins[name.ToLowerInvariant()] = error;
            }
            break;
        }
        return host;
    }

    /// <summary>Templates only from currently installed and enabled DLL plugins.</summary>
    public IReadOnlyList<OfficeTemplateEntry> Templates => OfficeTemplateCatalog.Discover(Plugins);

    /// <summary>Only enabled plugins with an actual document import capability.</summary>
    public IReadOnlyList<IOfficeDocumentPlugin> DocumentHandlers(string filePath) =>
        DocumentPluginRouter.GetCandidates(Plugins, filePath);

    public bool TryGet(string id, out IOfficePlugin? plugin)
    {
        if (_disabled.Contains(id)) { plugin = null; return false; }
        return _plugins.TryGetValue(id, out plugin);
    }

    public bool TryCreateWindow(string id, WindowIcon? icon, out Window? window)
    {
        window = null;
        if (!TryGet(id, out var plugin) || plugin is null)
            return false;
        try
        {
            window = plugin.CreateWindow(icon);
            return window is not null;
        }
        catch (Exception ex)
        {
            _errors.Add(id + ": " + ex.Message);
            return false;
        }
    }

    private void Register(string manifestPath)
    {
        var manifest = PluginCompatibilityPolicy.Read(File.ReadAllText(manifestPath));
        var installedVersions = _plugins.ToDictionary(
            item => item.Key,
            item => item.Value.GetType().Assembly.GetName().Version ?? new Version(0, 0),
            StringComparer.OrdinalIgnoreCase);
        PluginCompatibilityPolicy.EnsureCompatible(manifest,
            typeof(OfficePluginHost).Assembly.GetName().Version ?? new Version(1, 0), installedVersions);
        if (manifest.ApiVersion != 1 ||
            string.IsNullOrWhiteSpace(manifest.Id) ||
            string.IsNullOrWhiteSpace(manifest.Assembly) ||
            string.IsNullOrWhiteSpace(manifest.EntryType))
            throw new InvalidDataException("Manifesto inválido ou versão incompatível.");
        if (Path.GetFileName(manifest.Assembly) != manifest.Assembly ||
            !manifest.Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O manifesto deve indicar apenas o nome da DLL.");
        if (_plugins.ContainsKey(manifest.Id))
            throw new InvalidDataException("ID de plugin duplicado: " + manifest.Id);

        var assemblyPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.Assembly));
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException("DLL do plugin não encontrada.", assemblyPath);
        var context = new OfficePluginLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);
        var type = assembly.GetType(manifest.EntryType, throwOnError: true)
            ?? throw new TypeLoadException(manifest.EntryType);
        if (!typeof(IOfficePlugin).IsAssignableFrom(type) ||
            Activator.CreateInstance(type) is not IOfficePlugin plugin ||
            !string.Equals(plugin.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Implementação IOfficePlugin incompatível com manifesto.");
        _plugins.Add(plugin.Id, plugin);
        _manifests.Add(plugin.Id, manifest);
    }

    private sealed class OfficePluginLoadContext(string pluginDllPath) : AssemblyLoadContext(isCollectible: false)
    {
        private readonly AssemblyDependencyResolver _resolver = new(pluginDllPath);

        protected override Assembly? Load(AssemblyName name)
        {
            var assemblyName = name.Name ?? "";
            // Avalonia, the core state model and plugin interfaces are shared with the host.
            // They must never be loaded as duplicate type identities.
            if (assemblyName.StartsWith("Avalonia", StringComparison.Ordinal) ||
                assemblyName is "Vcc.Office.Plugin.Abstractions" or "Vcc.Office.CoreShell")
                return null;
            var path = _resolver.ResolveAssemblyToPath(name);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
