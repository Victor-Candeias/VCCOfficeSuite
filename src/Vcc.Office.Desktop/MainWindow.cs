using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>Launcher multiplataforma com três disposições distintas e catálogo partilhado.</summary>
public sealed class MainWindow : Window
{
    private readonly OfficeApplicationRegistry _registry = new();
    private readonly DashboardRegistry _dashboards = new();
    private readonly Grid _layout = new();
    private readonly TextBox _search = new() { PlaceholderText = "Pesquisar aplicações...", MinWidth = 180 };
    private string _activeDashboard = DashboardIds.Modern;

    private bool Dark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    private IBrush Surface => Brush.Parse(Dark ? "#172439" : "#FFFFFF");
    private IBrush Page => Brush.Parse(Dark ? "#0B1422" : "#F5F8FD");
    private IBrush ForegroundBrush => Brush.Parse(Dark ? "#F2F6FF" : "#16253B");
    private IBrush Muted => Brush.Parse(Dark ? "#A7B7D1" : "#62718B");
    private IBrush BorderBrush => Brush.Parse(Dark ? "#33445F" : "#DDE4EF");

    public MainWindow()
    {
        Title = "VCC Office Suite";
        Width = 1280;
        Height = 820;
        MinWidth = 760;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = _layout;
        _search.TextChanged += (_, _) => Render();
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += (_, _) => Render();
        Render();
    }

    private TextBlock Text(string value, double size = 14, bool bold = false, IBrush? color = null) =>
        new()
        {
            Text = value,
            FontSize = size,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = color ?? ForegroundBrush,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

    private Border Card(Control child, Thickness? padding = null) => new()
    {
        Background = Surface,
        BorderBrush = BorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(13),
        Padding = padding ?? new Thickness(18),
        Child = child
    };

    private Button Nav(string label, Action action, bool selected = false)
    {
        var button = new Button
        {
            Content = label,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Height = 40,
            Margin = new Thickness(0, 3),
            Padding = new Thickness(14, 8),
            Foreground = selected ? Brushes.White : ForegroundBrush,
            Background = selected ? Brush.Parse("#2158AD") : Brush.Parse(Dark ? "#172439" : "#EEF3FA"),
            BorderThickness = new Thickness(0)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void Switch(string id)
    {
        if (!_dashboards.Contains(id)) return;
        _activeDashboard = id;
        _search.Text = "";
        Render();
    }

    private void Render()
    {
        _layout.Children.Clear();
        _layout.RowDefinitions = new RowDefinitions("68,*");
        _layout.ColumnDefinitions = new ColumnDefinitions("228,*");
        _layout.Background = Page;

        var header = new Border
        {
            Background = Surface,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 10)
        };
        var headerContent = new DockPanel { LastChildFill = true };
        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 10
        };
        tools.Children.Add(_search);
        var theme = new Button { Content = Dark ? "☀ Tema claro" : "☾ Tema escuro", MinWidth = 116 };
        theme.Click += (_, _) =>
        {
            if (Application.Current is { } app)
                app.RequestedThemeVariant = Dark ? ThemeVariant.Light : ThemeVariant.Dark;
            Render();
        };
        tools.Children.Add(theme);
        DockPanel.SetDock(tools, Dock.Right);
        headerContent.Children.Add(tools);
        headerContent.Children.Add(Text("▣  VCC OFFICE SUITE", 23, true));
        header.Child = headerContent;
        Grid.SetColumnSpan(header, 2);
        _layout.Children.Add(header);

        var sidebar = new Border
        {
            Background = Brush.Parse(Dark ? "#101B2D" : "#EEF3FA"),
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(13)
        };
        var navigation = new StackPanel { Spacing = 3 };
        navigation.Children.Add(Nav("⌂   Início", () => Switch(DashboardIds.Modern),
            _activeDashboard == DashboardIds.Modern));
        navigation.Children.Add(Nav("▦   Todas as aplicações", () => Switch(DashboardIds.Grid),
            _activeDashboard == DashboardIds.Grid));
        navigation.Children.Add(Nav("▣   Dashboard clássico", () => Switch(DashboardIds.Classic),
            _activeDashboard == DashboardIds.Classic));
        navigation.Children.Add(new Border { Height = 1, Background = BorderBrush, Margin = new Thickness(0, 17, 0, 9) });
        navigation.Children.Add(Text("APLICAÇÕES", 11, true, Muted));
        foreach (var app in _registry.GetAll())
            navigation.Children.Add(Nav(app.Name.Replace("VCC ", ""), () => NotifyUnavailable(app)));
        sidebar.Child = new ScrollViewer { Content = navigation };
        Grid.SetRow(sidebar, 1);
        _layout.Children.Add(sidebar);

        var body = new StackPanel { Spacing = 20, Margin = new Thickness(25, 22, 25, 28) };
        switch (_activeDashboard)
        {
            case DashboardIds.Classic:
                BuildClassic(body);
                break;
            case DashboardIds.Grid:
                BuildGrid(body);
                break;
            default:
                BuildModern(body);
                break;
        }
        var scroll = new ScrollViewer { Content = body };
        Grid.SetRow(scroll, 1);
        Grid.SetColumn(scroll, 1);
        _layout.Children.Add(scroll);
    }

    private void BuildModern(StackPanel root)
    {
        var bannerContent = new StackPanel { Spacing = 8, Margin = new Thickness(25, 21) };
        var hour = DateTime.Now.Hour;
        bannerContent.Children.Add(Text(hour < 12 ? "Bom dia" : hour < 19 ? "Boa tarde" : "Boa noite",
            32, true, Brushes.White));
        bannerContent.Children.Add(Text("Bem-vindo ao VCC Office, a sua suite completa de produtividade.",
            15, false, Brush.Parse("#DFEAFF")));
        root.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Brush.Parse("#153A85").Color, 0),
                    new GradientStop(Brush.Parse("#066DD0").Color, 1)
                }
            },
            CornerRadius = new CornerRadius(15),
            MinHeight = 150,
            Child = bannerContent
        });

        var shortcuts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        shortcuts.Children.Add(Nav("＋  Novo documento", () => NotifyUnavailable("writer")));
        shortcuts.Children.Add(Nav("▱  Abrir ficheiro", () => NotifyFeature("Abertura multiplataforma de ficheiros")));
        shortcuts.Children.Add(Nav("▦  Modelos", () => NotifyUnavailable("templates")));
        root.Children.Add(shortcuts);

        var panels = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (title, message) in new[]
        {
            ("◷  Ficheiros recentes", "Ainda não há ficheiros recentes nesta interface."),
            ("★  Ficheiros fixos", "Os favoritos serão integrados nesta fase de migração."),
            ("♧  Partilhados comigo", "É necessária uma fonte de colaboração configurada.")
        })
        {
            var inner = new StackPanel { Spacing = 15 };
            inner.Children.Add(Text(title, 17, true));
            inner.Children.Add(new Border { Background = BorderBrush, Height = 1 });
            inner.Children.Add(Text(message, 13, false, Muted));
            var panel = Card(inner);
            panel.Width = 270;
            panel.MinHeight = 178;
            panel.Margin = new Thickness(0, 0, 14, 10);
            panels.Children.Add(panel);
        }
        root.Children.Add(panels);
        root.Children.Add(Text("Aplicações", 20, true));
        AddApplicationCards(root, 12);
    }

    private void BuildClassic(StackPanel root)
    {
        root.Children.Add(Text("VCC Office — Dashboard clássico", 28, true));
        root.Children.Add(Text("Crie documentos e aceda aos módulos da suite.", 14, false, Muted));
        root.Children.Add(Card(Text("Criar novo — Writer · Sheets · Slides · Data", 17, true)));
        AddApplicationCards(root, 15);
        root.Children.Add(Card(Text("Ficheiros recentes e favoritos: integração multiplataforma pendente.", 14, false, Muted)));
    }

    private void BuildGrid(StackPanel root)
    {
        root.Children.Add(Text("Todas as aplicações", 30, true));
        root.Children.Add(Text("Explore os 26 módulos do VCC Office.", 14, false, Muted));
        AddApplicationCards(root, int.MaxValue);
    }

    private void AddApplicationCards(StackPanel root, int maximum)
    {
        var list = _registry.GetAll()
            .Where(module => string.IsNullOrWhiteSpace(_search.Text) ||
                module.Name.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(maximum)
            .ToArray();
        var cards = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var module in list)
        {
            var box = new StackPanel { Spacing = 6 };
            box.Children.Add(Text(module.Name, 16, true));
            box.Children.Add(Text("Em migração", 12, false, Muted));
            var card = new Button
            {
                Content = Card(box, new Thickness(18)),
                Width = _activeDashboard == DashboardIds.Classic ? 250 : 206,
                Height = 100,
                Margin = new Thickness(0, 0, 12, 12),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            card.Click += (_, _) => NotifyUnavailable(module);
            cards.Children.Add(card);
        }
        if (list.Length == 0)
            root.Children.Add(Text("Nenhuma aplicação encontrada.", 14, false, Muted));
        root.Children.Add(cards);
    }

    private void NotifyUnavailable(string id) =>
        NotifyUnavailable(_registry.Find(id));

    private void NotifyUnavailable(OfficeModule? module)
    {
        NotifyFeature(module is null ? "Módulo desconhecido" :
            module.Name + " ainda não tem uma interface Avalonia disponível");
    }

    private void NotifyFeature(string title)
    {
        var window = new Window
        {
            Title = "VCC Office Suite",
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Text = title + ". A versão Windows WPF continua disponível durante a migração.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        window.ShowDialog(this);
    }
}
