using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Vcc.Office.Desktop;

/// <summary>
/// Uses the existing VCC Office icon embedded from the Windows shell project.
/// The same .ico is also the Windows executable icon. No filesystem dependency.
/// </summary>
internal static class AppBranding
{
    private const string IconResource = "Vcc.Office.Desktop.AppIcon";

    internal static WindowIcon LoadWindowIcon()
    {
        using var stream = typeof(AppBranding).Assembly.GetManifestResourceStream(IconResource)
            ?? throw new InvalidOperationException("O ícone VCC Office não foi incorporado no executável.");
        return new WindowIcon(stream);
    }

    internal static Border HeaderIcon(double size = 33)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.20),
            Background = Brush.Parse("#2765D9"),
            Child = new TextBlock
            {
                Text = "V",
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = size * 0.60,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    internal static bool HasEmbeddedIcon() =>
        typeof(AppBranding).Assembly.GetManifestResourceInfo(IconResource) is not null;
}
