using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Vcc.Office.Desktop;

/// <summary>Janela de arranque nativa do Avalonia, sem dependências WPF ou Win32.</summary>
public sealed class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        Title = "VCC Office Suite — A iniciar";
        Icon = AppBranding.LoadWindowIcon();
        Width = 540;
        Height = 320;
        CanResize = false;
        ShowInTaskbar = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Transparent;
        ExtendClientAreaToDecorationsHint = false;

        var root = new Border
        {
            Background = Brush.Parse("#101C35"),
            BorderBrush = Brush.Parse("#4264A4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(35, 28),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto")
            }
        };
        var grid = (Grid)root.Child!;
        var branding = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var logo = new Border
        {
            Width = 88,
            Height = 88,
            CornerRadius = new CornerRadius(19),
            Background = Brush.Parse("#2765D9"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = "V",
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = 57,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        branding.Children.Add(logo);
        branding.Children.Add(new TextBlock
        {
            Text = "VCC OFFICE SUITE",
            FontSize = 29,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#F6F9FF"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        branding.Children.Add(new TextBlock
        {
            Text = "A sua produtividade num só lugar",
            Foreground = Brush.Parse("#B7CAED"),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        grid.Children.Add(branding);
        var footer = new StackPanel { Spacing = 10 };
        footer.Children.Add(new TextBlock
        {
            Text = "A iniciar a aplicação...",
            Foreground = Brush.Parse("#D7E5FF"),
            FontSize = 12
        });
        footer.Children.Add(new ProgressBar
        {
            Height = 4,
            IsIndeterminate = true,
            Foreground = Brush.Parse("#5E9BFF"),
            Background = Brush.Parse("#263A60")
        });
        Grid.SetRow(footer, 1);
        grid.Children.Add(footer);
        Content = root;
    }
}
