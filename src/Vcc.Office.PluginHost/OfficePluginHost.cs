using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia.Controls;
using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.PluginHost;

/// <summary>Loads trusted, separately compiled modules from Plugins/<id>/plugin.json.</summary>
public sealed class OfficePluginHost
{
    private readonly Dictionary<string, IOfficePlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _errors = [];

    public IReadOnlyCollection<IOfficePlugin> Plugins => _plugins.Values;
    public IReadOnlyList<string> Errors => _errors;

    public static OfficePluginHost Discover(string? root = null)
    {
        var host = new OfficePluginHost();
        var directory = root ?? Path.Combine(AppContext.BaseDirectory, "Plugins");
        if (!Directory.Exists(directory))
            return host;

        foreach (var manifestPath in Directory.EnumerateFiles(directory, "plugin.json", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                host.Register(manifestPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                       JsonException or InvalidDataException or InvalidOperationException or
                                       ArgumentException or FileLoadException or
                                       BadImageFormatException or TypeLoadException or
                                       TargetInvocationException)
            {
                host._errors.Add(Path.GetFileName(Path.GetDirectoryName(manifestPath)) + ": " + ex.Message);
            }
        }
        return host;
    }

    public bool TryGet(string id, out IOfficePlugin? plugin) =>
        _plugins.TryGetValue(id, out plugin);

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
        var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Manifesto vazio.");
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
    }

    private sealed record PluginManifest(string Id, string Assembly, string EntryType, int ApiVersion);

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
