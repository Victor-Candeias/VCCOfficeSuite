using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// Primeiro shell Avalonia: usa o catálogo real do Core e deixa explícitos os
/// módulos cuja interface multiplataforma ainda não existe.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly OfficeApplicationRegistry _registry = new();
    private readonly DashboardRegistry _dashboards = new();
    private readonly StackPanel _content = new() { Spacing = 14 };
    private const string SearchHint = "Pesquisar aplicações...";
    private string _activeDashboard = DashboardIds.Modern;

    public MainWindow()
    {
        Title = "VCC Office Suite";
        Width = 1280;
        Height = 820;
        MinWidth = 760;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new DockPanel();
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(20, 16)
        };
        header.Children.Add(new TextBlock
        {
            Text = "VCC Office Suite",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });

        foreach (var dashboard in _dashboards.GetAll())
        {
            var button = new Button { Content = dashboard.Name, MinWidth = 92 };
            button.Click += (_, _) => ShowDashboard(dashboard.Id);
            header.Children.Add(button);
        }

        var themeButton = new Button { Content = "Alternar tema" };
        themeButton.Click += (_, _) =>
        {
            if (Application.Current is { } app)
                app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
                    ? ThemeVariant.Light : ThemeVariant.Dark;
        };
        header.Children.Add(themeButton);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var scroller = new ScrollViewer
        {
            Content = _content
        };
        _content.Margin = new Thickness(26, 16);
        root.Children.Add(scroller);
        Content = root;
        ShowDashboard(_activeDashboard);
    }

    private void ShowDashboard(string dashboardId)
    {
        if (!_dashboards.Contains(dashboardId)) dashboardId = DashboardIds.Modern;
        _activeDashboard = dashboardId;
        _content.Children.Clear();
        var title = dashboardId switch
        {
            DashboardIds.Classic => "Dashboard clássico",
            DashboardIds.Grid => "Todas as aplicações",
            _ => "Bem-vindo ao VCC Office Suite"
        };
        _content.Children.Add(new TextBlock
        {
            Text = title, FontSize = 29, FontWeight = FontWeight.Bold
        });
        _content.Children.Add(new TextBlock
        {
            Text = "Versão Avalonia multiplataforma em migração. O Core .NET 10 é partilhado com a versão Windows.",
            TextWrapping = TextWrapping.Wrap
        });

        var search = new TextBox { PlaceholderText = SearchHint, MinWidth = 280 };
        _content.Children.Add(search);
        var modules = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = dashboardId == DashboardIds.Classic ? 270 : 205,
            ItemHeight = 86
        };
        _content.Children.Add(modules);

        void Render(string? query)
        {
            modules.Children.Clear();
            foreach (var module in _registry.GetAll().Where(m =>
                string.IsNullOrWhiteSpace(query) ||
                m.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                var card = new Button
                {
                    Content = module.Name,
                    Width = dashboardId == DashboardIds.Classic ? 245 : 185,
                    Height = 65,
                    Margin = new Thickness(5),
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                card.Click += (_, _) =>
                {
                    // Data tem uma interface WPF, ainda não transportada para Avalonia.
                    // Não declarar nenhum editor como funcional nesta fase.
                    var message = new Window
                    {
                        Title = module.Name,
                        Width = 410,
                        Height = 180,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Content = new TextBlock
                        {
                            Text = "Este módulo ainda não possui uma interface Avalonia. A versão Windows WPF permanece disponível.",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(24),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    message.ShowDialog(this);
                };
                modules.Children.Add(card);
            }
        }
        search.TextChanged += (_, _) => Render(search.Text);
        Render(null);
    }
}
