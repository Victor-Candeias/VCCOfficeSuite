using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.Slides;

/// <summary>Independent presentation editor, engine and plugin metadata.</summary>
public sealed class SlidesPlugin : IOfficeDocumentPlugin
{
    public string Id => "slides";
    public string DisplayName => "VCC Slides";
    public string ToolTip => "Criar, editar e exportar apresentações, objetos, notas e transições.";
    public bool SupportsNewDocument => true;
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".pptx"];

    public Task OpenDocumentAsync(Window window, string filePath) =>
        window is SlidesWindow editor ? editor.OpenDocumentAsync(filePath)
            : throw new ArgumentException("A janela não pertence ao plugin SlidesPlugin.", nameof(window));

    public Control CreateMenuIcon(double size)
    {
        if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
            throw new ArgumentOutOfRangeException(nameof(size));
        return new Border
        {
            Name = "SlidesPluginIcon", Width = size, Height = size,
            Background = Brush.Parse("#CD531E"), CornerRadius = new CornerRadius(size * 0.2),
            Child = new TextBlock
            {
                Text = "▶", Foreground = Brushes.White, FontSize = size * 0.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
    public WindowIcon CreateWindowIcon()
    {
        const string icon = "AAABAAEAICAAAAAAIAAqAQAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAAgAAAAIAgGAAAAc3p69AAAAPFJREFUeJztl70OgjAQx4/G6Z5Bw6orT6ErYfbhnNG1b8GqK+mD6GBqoKXaa69tSPyPpXC/3CdXXY/bJxTUxjxopUpq8HbauQFaqWDo6qQArVQzCJHTOADA0NUzL4ucxpcgxI+7yVUcwKoCrf3lzm7scT5YZ8U98AcgAyzFMSuAhuACiQoBBwhLDsRAsCVhqDfYq4AK4uyEoaJ2UBaAmLYdBcAxL4IAOAcVOQm5p+T6ZoEpRAREDH7fmQO+tdz0I+m+qfWHIFakMtTu9nnm+5svAN7r0rePc6vpx892VE2X0xwLytS4BaAhUspcTi2A3HoBgTtPaT2Y83kAAAAASUVORK5CYII=";
        using var stream = new MemoryStream(Convert.FromBase64String(icon));
        return new WindowIcon(stream);
    }
    public Window CreateWindow(WindowIcon? icon = null) => new SlidesWindow { Icon = icon ?? CreateWindowIcon() };
}
