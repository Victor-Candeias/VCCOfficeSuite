using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Shell;

/// <summary>
/// Launcher nativo WPF. As vistas apresentam apenas dados reais ou estados vazios.
/// Os atalhos dos editores não estão ligados enquanto os módulos não forem entregues.
/// </summary>
public sealed class DashboardView : UserControl
{
    private readonly HomeScreenModel _model;
    private readonly bool _dark;
    private readonly Action<string> _navigate;
    private readonly Action _openSettings;
    private readonly Brush _foreground;
    private readonly Brush _muted;
    private readonly Brush _surface;
    private readonly Brush _border;
    private readonly Brush _background;

    public DashboardView(string dashboardId, HomeScreenModel model, bool dark,
        Action<string> navigate, Action openSettings)
    {
        _model = model;
        _dark = dark;
        _navigate = navigate;
        _openSettings = openSettings;
        _foreground = Paint(dark ? "#F2F6FF" : "#16253B");
        _muted = Paint(dark ? "#A7B7D1" : "#62718B");
        _surface = Paint(dark ? "#172439" : "#FFFFFF");
        _border = Paint(dark ? "#33445F" : "#DDE4EF");
        _background = Paint(dark ? "#0B1422" : "#F5F8FD");
        Content = BuildFrame(dashboardId);
    }

    private static SolidColorBrush Paint(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    private TextBlock Label(string text, double size = 14, bool bold = false, Brush? color = null) =>
        new()
        {
            Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = color ?? _foreground, TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static StackPanel Column(double gapBottom = 0) =>
        new() { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, gapBottom) };

    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal };

    private Border Panel(UIElement child, double radius = 13, Thickness? padding = null) =>
        new()
        {
            Background = _surface, BorderBrush = _border, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius), Padding = padding ?? new Thickness(17),
            Child = child
        };

    private Border ActionButton(string title, string description, string glyph, string color)
    {
        var content = new DockPanel { LastChildFill = true };
        var mark = new Border
        {
            Width = 54, Height = 54, CornerRadius = new CornerRadius(11),
            Background = Paint(color), Margin = new Thickness(0, 0, 12, 0)
        };
        mark.Child = Label(glyph, 28, true, Brushes.White);
        ((TextBlock)mark.Child).HorizontalAlignment = HorizontalAlignment.Center;
        DockPanel.SetDock(mark, Dock.Left);
        content.Children.Add(mark);
        var words = Column();
        words.Children.Add(Label(title, 17, true, Brushes.White));
        var line = Label(description, 12, false, Paint("#DFEAFF"));
        line.Margin = new Thickness(0, 6, 0, 0);
        words.Children.Add(line);
        content.Children.Add(words);
        return new Border
        {
            Child = content, Padding = new Thickness(15), Margin = new Thickness(0, 0, 12, 10),
            Background = new LinearGradientBrush(Paint(color).Color, Paint("#1B2C49").Color, 0),
            BorderBrush = Paint(color), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ToolTip = "Disponível quando o módulo correspondente estiver implementado",
            MinWidth = 184
        };
    }

    private UIElement BuildFrame(string id)
    {
        var layout = new Grid { Background = _background };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(218) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

        var sidebar = BuildSidebar(id);
        Grid.SetColumn(sidebar, 0);
        layout.Children.Add(sidebar);

        var content = new ScrollViewer
        {
            Background = _background,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            PanningMode = PanningMode.VerticalOnly,
            Content = id == DashboardIds.Grid ? BuildGrid() : BuildModern()
        };
        Grid.SetColumn(content, 1);
        layout.Children.Add(content);

        var status = new Border
        {
            Background = Paint(_dark ? "#0E192A" : "#EAF0F8"),
            BorderBrush = _border, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 0, 14, 0)
        };
        var statusItems = new DockPanel();
        statusItems.Children.Add(Label("●  VCC Office pronto", 11, false, Paint(_dark ? "#53D39C" : "#147B55")));
        var location = Label("Dados locais  ·  Offline-first", 11, false, _muted);
        DockPanel.SetDock(location, Dock.Right);
        statusItems.Children.Add(location);
        status.Child = statusItems;
        Grid.SetRow(status, 1);
        Grid.SetColumnSpan(status, 2);
        layout.Children.Add(status);

        return layout;
    }

    private UIElement BuildSidebar(string selectedId)
    {
        var dock = new DockPanel
        {
            Background = Paint(_dark ? "#101B2D" : "#EEF3FA"),
            LastChildFill = true
        };
        var settings = new Button
        {
            Content = "⚙   Definições", HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(12),
            Background = Brushes.Transparent, Foreground = _foreground,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
        };
        settings.Click += (_, _) => _openSettings();
        DockPanel.SetDock(settings, Dock.Bottom);
        dock.Children.Add(settings);

        var sidebar = Column();
        sidebar.Margin = new Thickness(11, 13, 10, 4);
        sidebar.Children.Add(NavButton("⌂", "Início", selectedId == DashboardIds.Modern,
            () => _navigate(DashboardIds.Modern)));
        sidebar.Children.Add(NavButton("▦", "Todas as aplicações", selectedId == DashboardIds.Grid,
            () => _navigate(DashboardIds.Grid)));
        sidebar.Children.Add(NavButton("▣", "Dashboard clássico", false,
            () => _navigate(DashboardIds.Classic)));
        var heading = Label("APLICAÇÕES", 11, true, _muted);
        heading.Margin = new Thickness(12, 23, 0, 9);
        sidebar.Children.Add(heading);

        var apps = _model.MainApplications.Concat(_model.MoreApplications).ToArray();
        foreach (var app in apps)
        {
            var line = Row();
            var badge = Badge(app, 26);
            badge.Margin = new Thickness(0, 0, 12, 0);
            line.Children.Add(badge);
            line.Children.Add(Label(app.Name.Replace("VCC ", ""), 13));
            sidebar.Children.Add(new Border
            {
                Child = line, Background = Brushes.Transparent,
                Padding = new Thickness(11, 5, 4, 5), MinHeight = 36,
                ToolTip = "Aplicação ainda não disponível no Launcher"
            });
        }

        dock.Children.Add(new ScrollViewer
        {
            Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        return dock;
    }

    private Button NavButton(string symbol, string name, bool selected, Action onClick)
    {
        var button = new Button
        {
            Content = $"{symbol}   {name}", Height = 39,
            Padding = new Thickness(13, 7, 8, 7),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 1, 0, 1),
            Background = selected ? Paint("#224A91") : Brushes.Transparent,
            Foreground = selected ? Brushes.White : _foreground,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
        };
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        button.Click += (_, _) => onClick();
        return button;
    }

    private Border Badge(HomeApplication app, double size)
    {
        var container = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.22),
            Background = Paint(app.Color)
        };
        var grid = new Grid();
        if (app.Icon != Geometry.Empty)
            grid.Children.Add(new Path
            {
                Data = app.Icon, Stroke = Brushes.White, StrokeThickness = 1.8,
                Stretch = Stretch.Uniform, Width = size * 0.62, Height = size * 0.62,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });
        else
            grid.Children.Add(new TextBlock
            {
                Text = app.Symbol, FontSize = size * 0.62, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        container.Child = grid;
        return container;
    }

    private UIElement BuildModern()
    {
        var root = Column();
        root.Margin = new Thickness(22, 18, 22, 30);

        var banner = new Border
        {
            Height = 158, CornerRadius = new CornerRadius(15),
            ClipToBounds = true, Margin = new Thickness(0, 0, 0, 19),
            Background = new LinearGradientBrush(Paint("#153A85").Color, Paint("#132355").Color, 0)
        };
        var art = new Grid { ClipToBounds = true };
        var wave = new Path
        {
            Data = Geometry.Parse("M0,130 C160,90 310,155 470,95 S780,36 1100,100 L1100,200 L0,200 Z"),
            Fill = new LinearGradientBrush(Paint("#146CBF").Color, Paint("#713ADD").Color, 0),
            Opacity = 0.55, Stretch = Stretch.Fill
        };
        art.Children.Add(wave);
        var intro = Column();
        intro.Margin = new Thickness(31, 24, 0, 0);
        intro.HorizontalAlignment = HorizontalAlignment.Left;
        intro.VerticalAlignment = VerticalAlignment.Top;
        var hour = DateTime.Now.Hour;
        intro.Children.Add(Label(hour < 12 ? "Bom dia" : hour < 19 ? "Boa tarde" : "Boa noite", 34, true, Brushes.White));
        var line = Label("Bem-vindo ao VCC Office, a sua suite completa de produtividade.", 16,
            false, Paint("#D1E1FF"));
        line.Margin = new Thickness(0, 10, 0, 0);
        intro.Children.Add(line);
        art.Children.Add(intro);

        var emblem = new Border
        {
            Width = 112, Height = 84, CornerRadius = new CornerRadius(12),
            Background = Paint("#1C4DC4"), BorderBrush = Paint("#5289F7"),
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 42, 0),
            RenderTransform = new RotateTransform(-7)
        };
        emblem.Child = Label("V", 61, true, Brushes.White);
        ((TextBlock)emblem.Child).HorizontalAlignment = HorizontalAlignment.Center;
        art.Children.Add(emblem);
        banner.Child = art;
        root.Children.Add(banner);

        var actions = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 15) };
        actions.Children.Add(ActionButton("Novo documento", "Criar um ficheiro", "＋", "#1C64E7"));
        actions.Children.Add(ActionButton("Abrir ficheiro", "No computador", "▱", "#027F9A"));
        actions.Children.Add(ActionButton("Modelos", "Modelos prontos", "▦", "#6D43CB"));
        actions.Children.Add(ActionButton("Importar", "Ficheiros externos", "⇧", "#2856A3"));
        root.Children.Add(actions);

        var panels = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 18) };
        var titles = new[] { "◷  Ficheiros recentes", "★  Ficheiros fixos", "♧  Partilhados comigo" };
        var empties = new[]
        {
            "Ainda não há ficheiros recentes.\nOs ficheiros que abrir aparecerão aqui.",
            "Ainda não existem ficheiros fixos.\nOs seus favoritos aparecerão aqui.",
            "Sem ficheiros partilhados.\nÉ necessária uma fonte de colaboração configurada."
        };
        for (var i = 0; i < titles.Length; i++)
        {
            var body = Column();
            body.Children.Add(Label(titles[i], 17, true));
            var separator = new Border
            {
                Height = 1, Background = _border, Margin = new Thickness(0, 16, 0, 0)
            };
            body.Children.Add(separator);
            var text = Label(empties[i], 13, false, _muted);
            text.Margin = new Thickness(4, 26, 4, 34);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.TextAlignment = TextAlignment.Center;
            body.Children.Add(text);
            var card = Panel(body);
            card.Margin = new Thickness(0, 0, i == 2 ? 0 : 12, 0);
            panels.Children.Add(card);
        }
        root.Children.Add(panels);

        var shortcuts = new UniformGrid { Columns = 4 };
        foreach (var (symbol, title, subtitle) in new[]
        {
            ("▣", "Agenda", "Eventos e compromissos"),
            ("✓", "Tarefas", "Gerir tarefas e projetos"),
            ("▦", "Modelos em destaque", "Documentos e apresentações"),
            ("⌁", "Automatizações", "Fluxos de trabalho")
        })
        {
            var body = Row();
            var mark = Label(symbol, 29, true, Paint("#7EBBFF"));
            mark.Margin = new Thickness(0, 0, 13, 0);
            body.Children.Add(mark);
            var words = Column();
            words.Children.Add(Label(title, 15, true));
            var subtitleText = Label(subtitle, 12, false, _muted);
            subtitleText.Margin = new Thickness(0, 5, 0, 0);
            words.Children.Add(subtitleText);
            body.Children.Add(words);
            var box = Panel(body, padding: new Thickness(14));
            box.Margin = new Thickness(0, 0, 12, 0);
            shortcuts.Children.Add(box);
        }
        root.Children.Add(shortcuts);

        var scroll = new Grid();
        scroll.Children.Add(root);
        scroll.SizeChanged += (_, args) =>
        {
            // O layout mantém-se legível em janelas compactas.
            var width = args.NewSize.Width;
            var cols = width < 740 ? 2 : 4;
            if (actions.Columns != cols) actions.Columns = cols;
            if (shortcuts.Columns != cols) shortcuts.Columns = cols;
            panels.Columns = width < 740 ? 1 : 3;
        };
        return scroll;
    }

    private UIElement BuildGrid()
    {
        var root = Column();
        root.Margin = new Thickness(27, 22, 27, 28);
        root.Children.Add(Label("Todas as aplicações", 30, true));
        var sub = Label("Explore os 26 módulos do VCC Office.", 14, false, _muted);
        sub.Margin = new Thickness(0, 7, 0, 20);
        root.Children.Add(sub);
        var filter = new TextBox
        {
            Height = 42, FontSize = 15, Padding = new Thickness(13, 9, 13, 9),
            Background = _surface, Foreground = _foreground, BorderBrush = _border,
            Margin = new Thickness(0, 0, 0, 21)
        };
        System.Windows.Automation.AutomationProperties.SetName(filter, "Pesquisar aplicações");
        root.Children.Add(filter);
        var grid = new UniformGrid { Columns = 4 };
        var apps = _model.MainApplications.Concat(_model.MoreApplications).ToArray();

        void Refresh()
        {
            grid.Children.Clear();
            foreach (var app in apps.Where(a => a.Name.Contains(filter.Text, StringComparison.OrdinalIgnoreCase)))
            {
                var body = Column();
                body.HorizontalAlignment = HorizontalAlignment.Center;
                var icon = Badge(app, 54);
                icon.Margin = new Thickness(0, 0, 0, 12);
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                body.Children.Add(icon);
                body.Children.Add(Label(app.Name, 14, true));
                var item = Panel(body, padding: new Thickness(12, 21, 12, 21));
                item.Margin = new Thickness(0, 0, 12, 12);
                item.ToolTip = "Módulo não disponível nesta versão";
                System.Windows.Automation.AutomationProperties.SetName(item, app.Name);
                grid.Children.Add(item);
            }
            if (grid.Children.Count == 0)
                grid.Children.Add(Label("Nenhuma aplicação encontrada.", 15, false, _muted));
        }

        filter.TextChanged += (_, _) => Refresh();
        Refresh();
        root.Children.Add(grid);
        var container = new Grid();
        container.Children.Add(root);
        container.SizeChanged += (_, args) =>
        {
            grid.Columns = args.NewSize.Width < 700 ? 2 : args.NewSize.Width < 1050 ? 3 : 4;
        };
        return container;
    }
}
