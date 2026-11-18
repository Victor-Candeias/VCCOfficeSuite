using Avalonia.Controls;

namespace Vcc.Office.Plugin.Abstractions;

/// <summary>Runtime contract for self-describing, trusted Office plugins.</summary>
public interface IOfficePlugin
{
    string Id { get; }
    string DisplayName { get; }
    string ToolTip { get; }
    bool SupportsNewDocument { get; }

    /// <summary>The plugin supplies its own icon for launcher menus and dashboards.</summary>
    Control CreateMenuIcon(double size);

    /// <summary>The plugin supplies its own window icon; the host does not choose branding.</summary>
    WindowIcon CreateWindowIcon();

    /// <summary>Create an application window. The host owns its lifetime.</summary>
    Window CreateWindow(WindowIcon? icon = null);
}
