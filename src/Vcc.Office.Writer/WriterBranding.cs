using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Vcc.Office.Writer;

/// <summary>Writer's independent visual identity, compiled directly into its plugin DLL.</summary>
internal static class WriterBranding
{
    // The icon is an actual .ico embedded in Vcc.Office.Writer.dll.
    public static WindowIcon CreateWindowIcon()
    {
        using var stream = typeof(WriterBranding).Assembly
            .GetManifestResourceStream("Vcc.Office.Writer.Icon")
            ?? throw new InvalidOperationException("O ícone do plugin Writer não foi incorporado na DLL.");
        return new WindowIcon(stream);
    }

    public static Control CreateMenuIcon(double size)
    {
        if (double.IsNaN(size) || double.IsInfinity(size) || size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        return new Border
        {
            Name = "WriterPluginIcon",
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.20),
            Background = Brush.Parse("#0D66E2"),
            Child = new TextBlock
            {
                Text = "W",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Arial"),
                FontWeight = FontWeight.Bold,
                FontSize = size * 0.59,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
}
