using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// Native Avalonia vector badges ported from the existing WPF HomeScreenModel.
/// The icon catalogue contains no WPF dependencies and does not rely on font glyphs.
/// </summary>
internal static class ModuleVisuals
{
    private sealed record Style(string Color, string Symbol, string? Path = null);

    private const string Document = "M5,2 L14,2 L20,8 L20,22 L5,22 Z M14,2 L14,8 L20,8 M8,12 L16,12 M8,16 L16,16";
    private const string Calendar = "M3,5 L21,5 L21,21 L3,21 Z M3,10 L21,10 M7,2 L7,7 M17,2 L17,7 M7,14 L10,14 M14,14 L17,14 M7,18 L10,18";
    private const string Nodes = "M12,4 L4,17 L20,17 Z M12,1 A3,3 0 1 0 12,7 A3,3 0 1 0 12,1 M4,14 A3,3 0 1 0 4,20 A3,3 0 1 0 4,14 M20,14 A3,3 0 1 0 20,20 A3,3 0 1 0 20,14";
    private const string Bars = "M4,20 L4,12 L8,12 L8,20 Z M10,20 L10,5 L14,5 L14,20 Z M16,20 L16,9 L20,9 L20,20 Z";
    private const string List = "M8,5 L21,5 M8,12 L21,12 M8,19 L21,19 M3,4 L4,4 L4,6 L3,6 Z M3,11 L4,11 L4,13 L3,13 Z M3,18 L4,18 L4,20 L3,20 Z";
    private static readonly Dictionary<string, Style> Styles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["writer"] = new("#0869E8", "W"),
        ["sheets"] = new("#00A465", "S"),
        ["slides"] = new("#F28706", "P"),
        ["mail"] = new("#128CEB", "", "M2,5 L22,5 L22,20 L2,20 Z M2,5 L12,13 L22,5 M2,20 L8,13 M22,20 L16,13"),
        ["calendar"] = new("#804BE4", "", Calendar),
        ["notes"] = new("#E3AC00", "", "M4,3 L20,3 L20,21 L4,21 Z M8,8 L16,8 M8,12 L16,12 M8,16 L13,16"),
        ["pdf"] = new("#E31D39", "", "M8,3 C16,2 7,21 3,19 C0,16 23,11 21,16 C18,22 6,4 8,3 Z"),
        ["draw"] = new("#068DEE", "", Nodes),
        ["projects"] = new("#096ACC", "", "M4,5 L4,20 M2,9 L20,9 M7,4 L15,4 L15,7 L7,7 Z M10,12 L21,12 L21,15 L10,15 Z M7,17 L16,17 L16,20 L7,20 Z"),
        ["publisher"] = new("#DC42A1", "", "M3,3 L21,3 L21,21 L3,21 Z M3,8 L21,8 M10,8 L10,21 M13,12 L18,12 M13,16 L18,16 M6,12 L7,12 M6,16 L7,16"),
        ["planner"] = new("#7455DB", "", "M3,7 L9,13 L19,3 M4,17 L20,17 M13,9 L21,9 M4,21 L17,21"),
        ["images"] = new("#0896C4", "", "M3,3 L21,3 L21,21 L3,21 Z M3,19 L10,11 L15,16 L18,13 L21,17 M17,6 A2,2 0 1 0 17,10 A2,2 0 1 0 17,6"),
        ["analytics"] = new("#6B3BE5", "", Bars),
        ["forms"] = new("#A35FE8", "", List),
        ["contacts"] = new("#126FDB", "", "M12,3 A4,4 0 1 0 12,11 A4,4 0 1 0 12,3 M4,21 L4,18 C4,11 20,11 20,18 L20,21 Z"),
        ["data"] = new("#9346DE", "", "M3,6 C3,1 21,1 21,6 C21,11 3,11 3,6 Z M3,6 L3,18 C3,23 21,23 21,18 L21,6 M3,12 C3,17 21,17 21,12"),
        ["mindmap"] = new("#0064DE", "", "M12,8 L12,2 M8,12 L2,12 M16,12 L22,12 M12,16 L12,22 M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 M10,1 L14,1 L14,4 L10,4 Z M1,10 L4,10 L4,14 L1,14 Z M20,10 L23,10 L23,14 L20,14 Z M10,20 L14,20 L14,23 L10,23 Z"),
        ["whiteboard"] = new("#00AEB4", "", "M3,18 L4,12 L16,2 L22,8 L10,20 L3,21 Z M13,5 L19,11 M4,13 L10,19 M14,21 L22,21"),
        ["scan"] = new("#168AA8", "", "M6,10 L6,2 L18,2 L18,10 M3,10 L21,10 L22,20 L2,20 Z M6,16 L18,16 M8,5 L16,5"),
        ["convert"] = new("#E07724", "", "M3,7 L21,7 M16,2 L21,7 L16,12 M21,17 L3,17 M8,12 L3,17 L8,22"),
        ["backup"] = new("#307D9B", "", "M5,8 C0,13 3,21 10,21 C17,22 23,17 21,11 C20,5 13,2 8,5 M3,2 L3,9 L10,9 M12,9 L12,14 L16,16"),
        ["automate"] = new("#E0A218", "", "M13,1 L4,14 L11,14 L10,23 L21,9 L14,9 Z"),
        ["templates"] = new("#5D7FE4", "", "M3,3 L10,3 L10,10 L3,10 Z M14,3 L21,3 L21,10 L14,10 Z M3,14 L10,14 L10,21 L3,21 Z M14,14 L21,14 L21,21 L14,21 Z"),
        ["math"] = new("#945CD2", "∑"),
        ["archive"] = new("#71829D", "", "M2,3 L22,3 L22,8 L2,8 Z M4,8 L20,8 L20,22 L4,22 Z M9,12 L15,12"),
        ["time"] = new("#0B9996", "", "M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M12,6 L12,12 L17,15")
    };

    internal static Border Badge(string moduleId, double size = 46)
    {
        if (!Styles.TryGetValue(moduleId, out var style))
            style = new Style("#506782", "V");
        var badge = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.22),
            Background = Brush.Parse(style.Color)
        };
        if (style.Path is not null)
        {
            badge.Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(style.Path),
                Stroke = Brushes.White,
                StrokeThickness = 1.8,
                Stretch = Stretch.Uniform,
                Width = size * 0.62,
                Height = size * 0.62,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            badge.Child = new TextBlock
            {
                Text = style.Symbol,
                FontSize = size * 0.55,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        return badge;
    }
}
