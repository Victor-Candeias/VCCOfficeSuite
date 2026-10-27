namespace Vcc.Office.CoreShell;

/// <summary>
/// DTO estável para transportar o layout da janela entre o Core Shell e a UI.
/// </summary>
public sealed record WindowLayoutDto(
    double Width,
    double Height,
    double Left,
    double Top,
    bool IsMaximized)
{
    public WindowLayout ToModel() => new(Width, Height, Left, Top, IsMaximized);

    public static WindowLayoutDto FromModel(WindowLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return new(layout.Width, layout.Height, layout.Left, layout.Top, layout.IsMaximized);
    }
}
