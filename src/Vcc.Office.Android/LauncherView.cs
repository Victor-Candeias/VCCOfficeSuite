using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Android;

/// <summary>
/// First Android Launcher surface, backed by the same catalog as the desktop launcher.
/// Plugin loading is intentionally deferred to VCC-Android-02.
/// </summary>
public sealed class LauncherView : UserControl
{
    private readonly OfficeApplicationRegistry _registry = new(includeLegacyAvailability: false);

    public LauncherView()
    {
        var root = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        root.Children.Add(new TextBlock
        {
            Text = "VCC OFFICE SUITE",
            FontSize = 27,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });

        root.Children.Add(new TextBlock
        {
            Text = "Launcher Android",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        });

        root.Children.Add(new TextBlock
        {
            Text = "A mesma base de catálogo de aplicações do Desktop. " +
                   "O carregamento de plugins Android será implementado na etapa VCC-Android-02.",
            TextWrapping = TextWrapping.Wrap
        });

        var modules = _registry.GetAll();
        root.Children.Add(new TextBlock
        {
            Text = $"Catálogo partilhado: {modules.Count} aplicações registadas",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        // A registry entry is not an installed plugin. No editor is presented as runnable.
        foreach (var module in modules)
        {
            var tile = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#EEF3FA")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12),
                Child = new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = module.Name,
                            Foreground = new SolidColorBrush(Color.Parse("#16253B")),
                            FontSize = 16,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "Ainda não disponível no Android",
                            Foreground = new SolidColorBrush(Color.Parse("#52657E")),
                            FontSize = 12
                        }
                    }
                }
            };
            root.Children.Add(tile);
        }

        Content = new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
    }
}
