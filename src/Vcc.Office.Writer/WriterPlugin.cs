using Avalonia.Controls;
using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.Writer;

/// <summary>Entry point loaded by Vcc.Office.PluginHost, not linked into the launcher.</summary>
public sealed class WriterPlugin : IOfficePlugin
{
    public string Id => "writer";
    public string DisplayName => "VCC Writer";

    public Window CreateWindow(WindowIcon? icon = null) => new WriterWindow(icon);
}
