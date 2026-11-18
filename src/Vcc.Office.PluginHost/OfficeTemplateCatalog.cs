using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.PluginHost;

public sealed record OfficeTemplateEntry(IOfficeTemplatePlugin Plugin, OfficeDocumentTemplate Template)
{
    public string Key => Plugin.Id + ":" + Template.Id;
}

/// <summary>Aggregates templates from installed and enabled plugins, without editor dependencies.</summary>
public static class OfficeTemplateCatalog
{
    public static IReadOnlyList<OfficeTemplateEntry> Discover(IEnumerable<IOfficePlugin> enabledPlugins)
    {
        ArgumentNullException.ThrowIfNull(enabledPlugins);
        var items = new List<OfficeTemplateEntry>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in enabledPlugins.OfType<IOfficeTemplatePlugin>())
        {
            foreach (var template in plugin.Templates)
            {
                if (string.IsNullOrWhiteSpace(plugin.Id) || string.IsNullOrWhiteSpace(template.Id) ||
                    string.IsNullOrWhiteSpace(template.Title) || string.IsNullOrWhiteSpace(template.Category))
                    continue;
                var key = plugin.Id + ":" + template.Id;
                if (keys.Add(key)) items.Add(new OfficeTemplateEntry(plugin, template));
            }
        }
        return items.OrderBy(item => item.Plugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Template.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Template.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
