using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.Plugin.Abstractions;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Data;

/// <summary>Independent SQLite data editor with its own engine and visual identity.</summary>
public sealed class DataPlugin : IOfficePlugin
{
    public string Id => "data";
    public string DisplayName => "VCC Data";
    public string ToolTip => "Gerir bases SQLite, tabelas, consultas, formulários e relatórios.";
    public bool SupportsNewDocument => false;
    public Control CreateMenuIcon(double size)
    {
        if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
            throw new ArgumentOutOfRangeException(nameof(size));
        return new Border
        {
            Name = "DataPluginIcon", Width = size, Height = size,
            Background = Brush.Parse("#5A3BB7"), CornerRadius = new CornerRadius(size * 0.2),
            Child = new TextBlock
            {
                Text = "▤", Foreground = Brushes.White, FontSize = size * 0.60,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
    public WindowIcon CreateWindowIcon()
    {
        const string icon = "AAABAAEAICAAAAAAIAAeAQAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAAgAAAAIAgGAAAAc3p69AAAAOVJREFUeJztlzsSgyAYhFcn1/AI1rHgAPTGyvNREXsOQGGfI+QgSQWDL36MQBp2hkZx9nNhZ6B63J8f/FG39QM590kNh246BpBzj5GppABy7hcQdU5zABiZWqRc5zTfg9jsAUpC8yCDUAUBuKbv17n5FAwJIDQPMnXlzheaeyHqwzc/mu/B+JbNCwAATXsNgPqeBBiZQtPCjlBTMy7vAQNhFLsFZAKp5U3A/MnVGvoSKTUsNSw1LDUsNSw1PH0ojX169i5BLDOyBUM3BUUbS0JzezuyCeSCcM0BoFrfjnNfTjcAufUF2+OS5cP9JH0AAAAASUVORK5CYII=";
        using var stream = new MemoryStream(Convert.FromBase64String(icon));
        return new WindowIcon(stream);
    }
    public Window CreateWindow(WindowIcon? icon = null) => new DataWindow(
        new VccDataService(new CoreDatabase(StoragePaths.ForLocalApplication())))
        { Icon = icon ?? CreateWindowIcon() };
}
