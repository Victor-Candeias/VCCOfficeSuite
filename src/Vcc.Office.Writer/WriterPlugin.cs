using Avalonia.Controls;
using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.Writer;

/// <summary>Self-describing Writer module: all visual metadata comes from this DLL.</summary>
public sealed class WriterPlugin : IOfficePlugin
{
    public string Id => "writer";
    public string DisplayName => "VCC Writer";
    public string ToolTip => "Criar, editar, abrir e guardar documentos de texto e DOCX.";
    public bool SupportsNewDocument => true;

    public Control CreateMenuIcon(double size) => WriterBranding.CreateMenuIcon(size);
    public WindowIcon CreateWindowIcon() => WriterBranding.CreateWindowIcon();
    public Window CreateWindow(WindowIcon? icon = null) =>
        new WriterWindow(icon ?? CreateWindowIcon());
}
