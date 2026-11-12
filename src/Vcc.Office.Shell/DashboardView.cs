using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Shell;

/// <summary>Vistas WPF independentes do dashboard clássico, sem dados fictícios.</summary>
public sealed class DashboardView : UserControl
{
    private readonly string _dashboardId;
    private readonly HomeScreenModel _model;
    private readonly bool _dark;
    private readonly Brush _foreground;
    private readonly Brush _surface;
    private readonly Brush _border;

    public DashboardView(string dashboardId, HomeScreenModel model, bool dark)
    {
        _dashboardId = dashboardId;
        _model = model;
        _dark = dark;
        _foreground = Brush(dark ? "#EEF4FF" : "#19263B");
        _surface = Brush(dark ? "#192A42" : "#FFFFFF");
        _border = Brush(dark ? "#344761" : "#DCE3ED");
        Content = dashboardId == DashboardIds.Grid ? BuildGridView() : BuildModernView();
    }

    private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;

    private TextBlock Text(string value, int size = 15, bool bold = false, string? color = null) =>
        new()
        {
            Text = value, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = color is null ? _foreground : Brush(color), TextWrapping = TextWrapping.Wrap
        };

    private Border Card(UIElement content, Thickness? padding = null)
        => new()
        {
            Background = _surface, BorderBrush = _border, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = padding ?? new Thickness(18),
            Child = content
        };

    private static StackPanel Stack(Orientation orientation = Orientation.Vertical, double spacing = 0)
        => new() { Orientation = orientation, Margin = new Thickness(0, 0, 0, spacing) };

    private ScrollViewer Scroll(UIElement child) => new()
    {
        Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Background = Brush(_dark ? "#0B1423" : "#F6F8FD")
    };

    private UIElement BuildGridView()
    {
        var stack = Stack();
        stack.Margin = new Thickness(28, 24, 28, 20);
        stack.Children.Add(Text("Todas as aplicações", 30, true));
        var subtitle = Text("Escolha uma das 26 aplicações do VCC Office. Os módulos ainda em desenvolvimento não estão disponíveis.", 14);
        subtitle.Margin = new Thickness(0, 8, 0, 18);
        stack.Children.Add(subtitle);

        var search = new TextBox
        {
            Height = 42, Padding = new Thickness(12, 8, 12, 8), FontSize = 15,
            Background = _surface, Foreground = _foreground,
            BorderBrush = _border, ToolTip = "Filtrar aplicações pelo nome",
            Margin = new Thickness(0, 0, 0, 22)
        };
        System.Windows.Automation.AutomationProperties.SetName(search, "Pesquisar aplicações");
        stack.Children.Add(search);

        var panel = new UniformGrid { Columns = 4 };
        var apps = _model.MainApplications.Concat(_model.MoreApplications).ToArray();
        void Populate(string filter)
        {
            panel.Children.Clear();
            foreach (var app in apps.Where(x => x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                panel.Children.Add(ApplicationCard(app));
            if (panel.Children.Count == 0)
                panel.Children.Add(Text("Não foram encontradas aplicações.", 16));
        }

        search.TextChanged += (_, _) => Populate(search.Text);
        Populate("");
        stack.Children.Add(panel);
        return Scroll(stack);
    }

    private UIElement ApplicationCard(HomeApplication app)
    {
        var content = Stack();
        content.HorizontalAlignment = HorizontalAlignment.Center;
        var badge = new Border
        {
            Width = 54, Height = 54, CornerRadius = new CornerRadius(12),
            Background = Brush(app.Color), Margin = new Thickness(0, 0, 0, 12)
        };
        badge.Child = new TextBlock
        {
            Text = string.IsNullOrEmpty(app.Symbol) ? "✦" : app.Symbol,
            Foreground = Brushes.White, FontSize = 27, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(badge);
        content.Children.Add(Text(app.Name, 14, true));

        var button = new Button
        {
            Content = content, Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(10, 18, 10, 18), Background = _surface,
            BorderBrush = _border, BorderThickness = new Thickness(1),
            IsEnabled = false, ToolTip = "Módulo ainda não disponível nesta versão"
        };
        System.Windows.Automation.AutomationProperties.SetName(button, app.Name);
        return button;
    }

    private UIElement BuildModernView()
    {
        var root = Stack();
        root.Margin = new Thickness(24, 18, 24, 24);

        var banner = new Border
        {
            CornerRadius = new CornerRadius(16), Padding = new Thickness(28),
            Margin = new Thickness(0, 0, 0, 22),
            Background = new LinearGradientBrush(
                Brush("#143C86").ColorOrDefault(), Brush("#382486").ColorOrDefault(), 0)
        };
        var welcome = Stack();
        welcome.Children.Add(Text("Bom dia — VCC Office", 32, true, "#FFFFFF"));
        var intro = Text("Bem-vindo à sua suite de produtividade.", 17, false, "#C9E3FF");
        intro.Margin = new Thickness(0, 9, 0, 0);
        welcome.Children.Add(intro);
        banner.Child = welcome;
        root.Children.Add(banner);

        var actions = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 20) };
        foreach (var (title, description, accent) in new[]
        {
            ("Novo documento", "Criar um novo ficheiro", "#1F5ED7"),
            ("Abrir ficheiro", "Procurar no computador", "#008DA6"),
            ("Modelos", "Usar modelos prontos", "#7547C6"),
            ("Importar", "Converter ficheiros externos", "#1565A7")
        })
        {
            var body = Stack();
            body.Children.Add(Text(title, 18, true, "#FFFFFF"));
            var line = Text(description, 12, false, "#E2EDFF");
            line.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(line);
            var action = new Button
            {
                Content = body, Background = Brush(accent), Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 12, 0), BorderThickness = new Thickness(0),
                IsEnabled = false, ToolTip = "Funcionalidade a implementar"
            };
            actions.Children.Add(action);
        }
        root.Children.Add(actions);

        var columns = new Grid { Margin = new Thickness(0, 0, 0, 22) };
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        var headings = new[] { "Ficheiros recentes", "Ficheiros fixos", "Partilhados comigo" };
        var descriptions = new[]
        {
            "Os ficheiros abertos aparecerão aqui.",
            "Fixe os seus ficheiros favoritos para acesso rápido.",
            "Os ficheiros partilhados aparecerão após configurar a colaboração."
        };
        for (var i = 0; i < headings.Length; i++)
        {
            var body = Stack();
            body.Children.Add(Text(headings[i], 18, true));
            var empty = Text(descriptions[i], 13);
            empty.Margin = new Thickness(0, 25, 0, 30);
            body.Children.Add(empty);
            var card = Card(body);
            card.Margin = new Thickness(0, 0, i == 2 ? 0 : 12, 0);
            Grid.SetColumn(card, i);
            columns.Children.Add(card);
        }
        root.Children.Add(columns);

        var shortcuts = new UniformGrid { Columns = 4 };
        foreach (var (title, subtitle) in new[]
        {
            ("Agenda", "Eventos e compromissos"), ("Tarefas", "Tarefas e projetos"),
            ("Modelos em destaque", "Documentos e apresentações"),
            ("Automatizações", "Fluxos de trabalho")
        })
        {
            var body = Stack();
            body.Children.Add(Text(title, 16, true));
            var label = Text(subtitle, 12);
            label.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(label);
            var card = Card(body);
            card.Margin = new Thickness(0, 0, 12, 0);
            shortcuts.Children.Add(card);
        }
        root.Children.Add(shortcuts);
        return Scroll(root);
    }
}

internal static class DashboardColorExtensions
{
    public static Color ColorOrDefault(this Brush brush) =>
        brush is SolidColorBrush solid ? solid.Color : Colors.SteelBlue;
}
