using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.PluginHost;

/// <summary>Capability-based routing without application-specific dependencies.</summary>
public static class DocumentPluginRouter
{
    public static IReadOnlyList<IOfficeDocumentPlugin> GetCandidates(
        IEnumerable<IOfficePlugin> enabledPlugins, string filePath)
    {
        ArgumentNullException.ThrowIfNull(enabledPlugins);
        if (string.IsNullOrWhiteSpace(filePath)) return [];
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension)) return [];
        return enabledPlugins.OfType<IOfficeDocumentPlugin>()
            .Where(plugin => plugin.SupportedExtensions.Any(supported =>
                string.Equals(supported, extension, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(plugin => plugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
