using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Vcc.Office.Writer;

/// <summary>Writer's independent visual identity, compiled directly into its plugin DLL.</summary>
internal static class WriterBranding
{
    // A dedicated 32x32 blue Writer icon with a white W, stored in this assembly.
    // This is not the host application's logo.
    private const string IconIcoBase64 =
        "AAABAAEAICAAAAAAIADoAAAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAAgAAAAIAgGAAAAc3p69AAAAK9JREFUeJztl9sNQEAQRa34pQPtUZP26EADfBFZ89yxVmLmh8TsvWceIUI7zFtVMOqS5g7gAA7wfYB16h8xoXSaXKaUbjcu5z07AisQd/4GcKXLEbF+8hLGlaV2SgSAmWFXDRQIoBmDpnJIVz0CzlA7CjGAVliajwJA7YJELXkkwFthAjiqsrw7SABKOH6myRUDvBFJAFhFKaNgAazfBu68uAPShdMuZvA/Iwf4PcAOxZdFQZs3OrkAAAAASUVORK5CYII=";

    public static WindowIcon CreateWindowIcon()
    {
        using var stream = new MemoryStream(Convert.FromBase64String(IconIcoBase64));
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
