using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>Launcher multiplataforma com três disposições distintas e catálogo partilhado.</summary>
public sealed partial class MainWindow : Window
{
    private readonly OfficeApplicationRegistry _registry = new();
    private readonly DashboardRegistry _dashboards = new();
    private readonly Grid _layout = new();
    private TextBox _search = new() { PlaceholderText = "Pesquisar aplicações...", MinWidth = 180 };
    private StackPanel? _body;
    private DataWindow? _dataWindow;
    private SheetsWindow? _sheetsWindow;
    private SlidesWindow? _slidesWindow;
    private WriterWindow? _writerWindow;
    private string _activeDashboard = DashboardIds.Modern;

    private bool Dark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    private bool HighContrast => _workspaceShell.State.HighContrast;
    private IBrush Surface => Brush.Parse(HighContrast ? "#000000" : Dark ? "#172439" : "#FFFFFF");
    private IBrush Page => Brush.Parse(HighContrast ? "#000000" : Dark ? "#0B1422" : "#F5F8FD");
    private IBrush ForegroundBrush => Brush.Parse(HighContrast ? "#FFFFFF" : Dark ? "#F2F6FF" : "#16253B");
    private IBrush Muted => Brush.Parse(HighContrast ? "#FFFF00" : Dark ? "#A7B7D1" : "#62718B");
    private IBrush LineBrush => Brush.Parse(HighContrast ? "#FFFFFF" : Dark ? "#33445F" : "#DDE4EF");

    public MainWindow()
    {
        Title = "VCC Office Suite";
        Icon = AppBranding.LoadWindowIcon();
        Width = 1280;
        Height = 820;
        MinWidth = 760;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = _layout;
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += (_, _) => Render();
        EnableWorkspaceShortcuts();
        Render();
        Opened += async (_, _) => await RestoreLauncherAsync();
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

    private const double ControlCornerRadius = 9;

    private TextBlock CenteredText(string value, double size, bool bold = false, IBrush? color = null)
    {
        var label = Text(value, size, bold, color);
        label.HorizontalAlignment = HorizontalAlignment.Stretch;
        label.TextAlignment = TextAlignment.Center;
        return label;
    }

    private Border Card(Control child, Thickness? padding = null) => new()
    {
        Background = Surface,
        BorderBrush = LineBrush,
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
            CornerRadius = new CornerRadius(ControlCornerRadius),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 40,
            Margin = new Thickness(0, 3),
            Padding = new Thickness(14, 8),
            Foreground = selected ? Brushes.White : ForegroundBrush,
            Background = selected ? Brush.Parse(HighContrast ? "#003399" : "#2158AD") : Brush.Parse(HighContrast ? "#000000" : Dark ? "#172439" : "#EEF3FA"),
            BorderThickness = new Thickness(0)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void Switch(string id)
    {
        if (!_dashboards.Contains(id)) return;
        _activeDashboard = id;
        _favoritesOnly = false;
        _launcherState = _launcherState with { DashboardId = id };
        _ = SaveLauncherAsync();
        _search.Text = "";
        Render();
    }

    private void Render()
    {
        // Each full rebuild creates a new header and therefore a new search TextBox.
        // Reusing a visual from a previous StackPanel throws when the launcher
        // re-renders (for example, after RestoreLauncherAsync or a theme switch).
        // Preserve the search value, but never reuse a control's visual parent.
        var previousSearch = _search.Text;
        _search = new TextBox
        {
            PlaceholderText = "Pesquisar aplicações...",
            MinWidth = 180,
            Text = previousSearch,
            CornerRadius = new CornerRadius(ControlCornerRadius)
        };
        _search.TextChanged += (_, _) => RefreshBody();

        _layout.Children.Clear();
        _layout.RowDefinitions = new RowDefinitions("68,*");
        _layout.ColumnDefinitions = new ColumnDefinitions("228,*");
        _layout.Background = Page;

        var header = new Border
        {
            Background = Surface,
            BorderBrush = LineBrush,
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
        var theme = new Button { Content = Dark ? "☀ Tema claro" : "☾ Tema escuro", MinWidth = 116, CornerRadius = new CornerRadius(ControlCornerRadius) };
        theme.Click += (_, _) =>
        {
            var nextTheme = Dark ? ThemeMode.Light : ThemeMode.Dark;
            _launcherState = _launcherState with { Theme = nextTheme };
            if (Application.Current is { } app)
                app.RequestedThemeVariant = nextTheme == ThemeMode.Dark
                    ? ThemeVariant.Dark : ThemeVariant.Light;
            _ = SaveLauncherAsync();
            Render();
        };
        tools.Children.Add(theme);
        var settings = new Button { Content = "⚙ Definições", MinWidth = 105, CornerRadius = new CornerRadius(ControlCornerRadius) };
        settings.Click += (_, _) => ShowLauncherSettings();
        tools.Children.Add(settings);
        DockPanel.SetDock(tools, Dock.Right);
        headerContent.Children.Add(tools);
        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        brand.Children.Add(AppBranding.HeaderIcon(30));
        brand.Children.Add(Text("VCC OFFICE SUITE", 23, true));
        headerContent.Children.Add(brand);
        header.Child = headerContent;
        Grid.SetColumnSpan(header, 2);
        _layout.Children.Add(header);

        var sidebar = new Border
        {
            Background = Brush.Parse(HighContrast ? "#000000" : Dark ? "#101B2D" : "#EEF3FA"),
            BorderBrush = LineBrush,
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
        navigation.Children.Add(Nav("◷   Ficheiros recentes", () => ShowRecentPage(false)));
        navigation.Children.Add(Nav("★   Favoritos", () => ShowRecentPage(true)));
        navigation.Children.Add(Nav("⚙   Definições", ShowLauncherSettings));
        navigation.Children.Add(Nav("▤   Ribbon", OpenRibbon));
        navigation.Children.Add(new Border { Height = 1, Background = LineBrush, Margin = new Thickness(0, 17, 0, 9) });
        navigation.Children.Add(Text("APLICAÇÕES", 11, true, Muted));
        foreach (var app in _registry.GetAll())
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            line.Children.Add(ModuleVisuals.Badge(app.Id, 25));
            line.Children.Add(Text(app.Name.Replace("VCC ", ""), 13));
            var selected = app.Id == _launcherState.LastSelectedModuleId;
            var item = new Button
            {
                Content = line,
                CornerRadius = new CornerRadius(ControlCornerRadius),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = selected ? Brush.Parse(Dark ? "#263C62" : "#DCE9FC") : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(7, 5),
                Margin = new Thickness(0, 2)
            };
            item.Click += (_, _) => OpenModule(app);
            navigation.Children.Add(item);
        }
        var personalized = ConfiguredItems(_search.Text);
        if (personalized.Count > 0)
        {
            navigation.Children.Add(new Border
            {
                Height = 1, Background = LineBrush, Margin = new Thickness(0, 15, 0, 8)
            });
            navigation.Children.Add(Text("PERSONALIZADOS", 11, true, Muted));
            foreach (var item in personalized)
                navigation.Children.Add(ConfiguredItemButton(item));
        }
        sidebar.Child = new ScrollViewer { Content = navigation };
        Grid.SetRow(sidebar, 1);
        _layout.Children.Add(sidebar);

        _body = new StackPanel { Spacing = 20, Margin = new Thickness(25, 22, 25, 28) };
        RefreshBody();
        var scroll = new ScrollViewer { Content = _body };
        Grid.SetRow(scroll, 1);
        Grid.SetColumn(scroll, 1);
        _layout.Children.Add(scroll);
    }

    private void RefreshBody()
    {
        if (_body is null) return;
        _body.Children.Clear();
        switch (_activeDashboard)
        {
            case DashboardIds.Classic:
                BuildClassic(_body);
                break;
            case DashboardIds.Grid:
                BuildGrid(_body);
                break;
            default:
                BuildModern(_body);
                break;
        }
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
                    new GradientStop(Color.Parse("#153A85"), 0),
                    new GradientStop(Color.Parse("#066DD0"), 1)
                }
            },
            CornerRadius = new CornerRadius(15),
            MinHeight = 150,
            Child = bannerContent
        });

        var shortcuts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        shortcuts.Children.Add(Nav("＋  Novo documento", () => NotifyUnavailable("writer")));
        shortcuts.Children.Add(Nav("▱  Abrir ficheiro", () => _ = ChooseLocalFileAsync()));
        shortcuts.Children.Add(Nav("▦  Modelos", () => NotifyUnavailable("templates")));
        root.Children.Add(shortcuts);

        var panels = new WrapPanel { Orientation = Orientation.Horizontal };
        var recent = DocumentPanel("◷  Ficheiros recentes", false, 5);
        var favorites = DocumentPanel("★  Favoritos", true, 5);
        var shared = Card(Text("♧  Partilhados comigo: fonte de colaboração não configurada.", 14, false, Muted));
        foreach (var panel in new[] { recent, favorites, shared })
        {
            panel.Width = 285;
            panel.MinHeight = 178;
            panel.Margin = new Thickness(0, 0, 14, 10);
            panels.Children.Add(panel);
        }
        root.Children.Add(panels);
        if (_registry.Find(_launcherState.LastSelectedModuleId) is { } previous)
            root.Children.Add(Text("Última aplicação selecionada: " + previous.Name, 13, false, Muted));
        root.Children.Add(Text("Aplicações", 20, true));
        AddApplicationCards(root, 12);
        AddConfiguredCards(root);
        var openCatalog = Nav("Ver todas as 26 aplicações", () => Switch(DashboardIds.Grid));
        root.Children.Add(openCatalog);
    }

    private void BuildClassic(StackPanel root)
    {
        root.Children.Add(Text("VCC Office — Dashboard clássico", 28, true));
        root.Children.Add(Text("Crie documentos e aceda aos módulos da suite.", 14, false, Muted));
        if (!string.IsNullOrWhiteSpace(_launcherState.LastOpenedDocumentPath))
            root.Children.Add(Text("Último documento: " +
                System.IO.Path.GetFileName(_launcherState.LastOpenedDocumentPath), 13, false, Muted));
        var creation = new WrapPanel();
        foreach (var app in _registry.GetAll().Where(m => m.Id is "writer" or "sheets" or "slides" or "data"))
        {
            var tile = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
            var tileIcon = ModuleVisuals.Badge(app.Id, 52);
            tileIcon.HorizontalAlignment = HorizontalAlignment.Center;
            tile.Children.Add(tileIcon);
            tile.Children.Add(CenteredText(app.Name, 15, true));
            var action = new Button
            {
                Content = tile, Width = 174, Height = 122,
                CornerRadius = new CornerRadius(ControlCornerRadius),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 12, 12),
                Background = Surface
            };
            action.Click += (_, _) => OpenModule(app);
            creation.Children.Add(action);
        }
        root.Children.Add(Card(creation));
        AddApplicationCards(root, 15);
        AddConfiguredCards(root);
        root.Children.Add(Nav("▱  Abrir ficheiro local", () => _ = ChooseLocalFileAsync()));
        root.Children.Add(DocumentPanel(_favoritesOnly ? "★  Favoritos" : "◷  Ficheiros recentes", _favoritesOnly, 30));
    }

    private void BuildGrid(StackPanel root)
    {
        root.Children.Add(Text("Todas as aplicações", 30, true));
        root.Children.Add(Text("Explore os 26 módulos do VCC Office.", 14, false, Muted));
        AddApplicationCards(root, int.MaxValue);
        AddConfiguredCards(root);
        root.Children.Add(Nav("▱  Abrir ficheiro local", () => _ = ChooseLocalFileAsync()));
        root.Children.Add(DocumentPanel("◷  Ficheiros recentes", false, 8));
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
            var box = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
            var moduleIcon = ModuleVisuals.Badge(module.Id, _activeDashboard == DashboardIds.Grid ? 52 : 38);
            moduleIcon.HorizontalAlignment = HorizontalAlignment.Center;
            box.Children.Add(moduleIcon);
            box.Children.Add(CenteredText(module.Name, 16, true));
            box.Children.Add(CenteredText(module.Id == "data" ? "Disponível · offline" : module.Id is "writer" or "sheets" or "slides" ? "Editor inicial · offline" : "Em migração", 12, false, Muted));
            var card = new Button
            {
                Content = Card(box, new Thickness(18)),
                CornerRadius = new CornerRadius(ControlCornerRadius),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Width = _activeDashboard == DashboardIds.Classic ? 250 : 206,
                Height = _activeDashboard == DashboardIds.Grid ? 158 : 143,
                Margin = new Thickness(0, 0, 12, 12),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            card.Click += (_, _) => OpenModule(module);
            cards.Children.Add(card);
        }
        if (list.Length == 0)
            root.Children.Add(Text("Nenhuma aplicação encontrada.", 14, false, Muted));
        root.Children.Add(cards);
    }

    private void AddConfiguredCards(StackPanel root)
    {
        var items = ConfiguredItems(_search.Text);
        if (items.Count == 0) return;
        root.Children.Add(Text("Módulos personalizados", 20, true));
        root.Children.Add(Text(
            "Entradas configuradas no Workspace. A criação de uma entrada não instala uma aplicação.",
            13, false, Muted));
        var cards = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var item in items)
        {
            var details = new StackPanel { Spacing = 7 };
            details.Children.Add(CenteredText(item.Glyph, 25, true));
            details.Children.Add(CenteredText(item.Title, 15, true));
            details.Children.Add(CenteredText(item.Description, 12, false, Muted));
            details.Children.Add(CenteredText("Sem aplicação associada", 11, false, Muted));
            var button = new Button
            {
                Content = Card(details, new Thickness(14)),
                CornerRadius = new CornerRadius(ControlCornerRadius),
                Width = 206, Height = 154,
                Margin = new Thickness(0, 0, 12, 12),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            button.Click += (_, _) => _ = OpenConfiguredAsync(item);
            cards.Children.Add(button);
        }
        root.Children.Add(cards);
    }

    private void NotifyUnavailable(string id) =>
        OpenModule(_registry.Find(id));

    private void OpenModule(OfficeModule? module)
    {
        if (module?.Id == "writer")
        {
            _launcherState = _launcherState with { LastSelectedModuleId = module.Id };
            _ = SaveLauncherAsync();
            if (_writerWindow is not null)
            {
                if (_writerWindow.WindowState == WindowState.Minimized)
                    _writerWindow.WindowState = WindowState.Normal;
                _writerWindow.Activate();
                return;
            }
            var writer = new WriterWindow();
            writer.Closed += (_, _) => _writerWindow = null;
            _writerWindow = writer;
            writer.Show(this);
            return;
        }
        if (module?.Id == "sheets")
        {
            _launcherState = _launcherState with { LastSelectedModuleId = module.Id };
            _ = SaveLauncherAsync();
            if (_sheetsWindow is not null)
            {
                if (_sheetsWindow.WindowState == WindowState.Minimized)
                    _sheetsWindow.WindowState = WindowState.Normal;
                _sheetsWindow.Activate();
                return;
            }
            var sheets = new SheetsWindow();
            sheets.Closed += (_, _) => _sheetsWindow = null;
            _sheetsWindow = sheets;
            sheets.Show(this);
            return;
        }
        if (module?.Id == "slides")
        {
            _launcherState = _launcherState with { LastSelectedModuleId = module.Id };
            _ = SaveLauncherAsync();
            if (_slidesWindow is not null)
            {
                if (_slidesWindow.WindowState == WindowState.Minimized)
                    _slidesWindow.WindowState = WindowState.Normal;
                _slidesWindow.Activate();
                return;
            }
            var slides = new SlidesWindow();
            slides.Closed += (_, _) => _slidesWindow = null;
            _slidesWindow = slides;
            slides.Show(this);
            return;
        }
        if (module?.Id == "data")
        {
            _launcherState = _launcherState with { LastSelectedModuleId = module.Id };
            _ = SaveLauncherAsync();
            if (_dataWindow is not null)
            {
                if (_dataWindow.WindowState == WindowState.Minimized)
                    _dataWindow.WindowState = WindowState.Normal;
                _dataWindow.Activate();
                return;
            }

            var data = new DataWindow(new VccDataService(_documentsDatabase));
            data.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            data.Closed += (_, _) => _dataWindow = null;
            _dataWindow = data;
            data.Show(this);
            return;
        }
        NotifyUnavailable(module);
    }

    private void NotifyUnavailable(OfficeModule? module)
    {
        if (module is not null)
        {
            _launcherState = _launcherState with { LastSelectedModuleId = module.Id };
            _ = SaveLauncherAsync();
        }
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
