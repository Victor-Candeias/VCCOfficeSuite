using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vcc.Office.CoreShell;
using Vcc.Office.PluginHost;

namespace Vcc.Office.Desktop;

/// <summary>Launcher multiplataforma com três disposições distintas e catálogo partilhado.</summary>
public sealed partial class MainWindow : Window
{
    private readonly OfficeApplicationRegistry _registry = new(includeLegacyAvailability: false);
    private readonly DashboardRegistry _dashboards = new();
    private readonly Grid _layout = new();
    private TextBox _search = new() { PlaceholderText = "Pesquisar aplicações...", MinWidth = 180 };
    private StackPanel? _body;
    private StackPanel? _configuredSidebar = null;
    private readonly Dictionary<string, Window> _pluginWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly OfficePluginHost _plugins;
    private string _activeDashboard = DashboardIds.Modern;
    private bool _compactLayout;
    private string? _selectedSidebarModuleId;
    private readonly Dictionary<string, Button> _sidebarModuleButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _sidebarDashboardButtons = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<OfficeModule> InstalledApplications =>
        _plugins.Plugins.Select(plugin => _registry.Find(plugin.Id))
            .OfType<OfficeModule>().Where(module => module.IsAvailable).ToArray();

    private bool Dark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    private bool HighContrast => _workspaceShell.State.HighContrast;
    private IBrush Surface => Brush.Parse(HighContrast ? "#000000" : Dark ? "#172439" : "#FFFFFF");
    private IBrush Page => Brush.Parse(HighContrast ? "#000000" : Dark ? "#0B1422" : "#F5F8FD");
    private IBrush ForegroundBrush => Brush.Parse(HighContrast ? "#FFFFFF" : Dark ? "#F2F6FF" : "#16253B");
    private IBrush Muted => Brush.Parse(HighContrast ? "#FFFF00" : Dark ? "#A7B7D1" : "#62718B");
    private IBrush LineBrush => Brush.Parse(HighContrast ? "#FFFFFF" : Dark ? "#33445F" : "#DDE4EF");

    public MainWindow() : this(null) { }

    public MainWindow(string? pluginsRoot)
    {
        _plugins = OfficePluginHost.Discover(pluginsRoot);
        Title = "VCC Office Suite";
        Icon = AppBranding.LoadWindowIcon();
        Width = 1280;
        Height = 820;
        MinWidth = 760;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = _layout;
        // Only installed DLL plugins are exposed as Launcher applications.
        // Legacy editors stay in the codebase until their independent migration.
        // Installed plugins own the names shown by the launcher.
        foreach (var plugin in _plugins.Plugins)
            _registry.RegisterPlugin(new OfficeModule(plugin.Id, plugin.DisplayName, true));
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += (_, _) => Render();
        EnableWorkspaceShortcuts();
        ConfigureModuleSession();
        Closing += (_, _) => SaveWorkspaceWindow();
        SizeChanged += (_, _) =>
        {
            var compact = Bounds.Width > 0 && Bounds.Width < 1080;
            if (compact == _compactLayout) return;
            _compactLayout = compact;
            Render();
        };
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
            Content = _launcherState.SidebarCollapsed ? label.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? label : label,
            CornerRadius = new CornerRadius(ControlCornerRadius),
            HorizontalContentAlignment = _launcherState.SidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = _launcherState.VisualDensity == "compact" || _launcherState.CompactNavigation ? 33 : 40,
            Margin = new Thickness(0, _launcherState.CompactNavigation ? 1 : 3),
            Padding = new Thickness(14, _launcherState.CompactNavigation ? 5 : 8),
            Foreground = selected ? Brushes.White : ForegroundBrush,
            Background = selected ? Brush.Parse(HighContrast ? "#003399" : "#2158AD") : Brush.Parse(HighContrast ? "#000000" : Dark ? "#172439" : "#EEF3FA"),
            BorderThickness = new Thickness(0)
        };
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private void Switch(string id)
    {
        if (!_dashboards.Contains(id)) return;
        _selectedSidebarModuleId = null;
        _activeDashboard = id;
        _favoritesOnly = false;
        _documentsPage = false;
        _templatesPage = false;
        _notificationsPage = false;
        _diagnosticsPage = false;
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
            MinWidth = _compactLayout ? 135 : 180,
            Text = previousSearch,
            CornerRadius = new CornerRadius(ControlCornerRadius)
        };
        AutomationProperties.SetName(_search, "Pesquisar aplicações");
        _search.TextChanged += (_, _) => { RefreshBody(); RefreshConfiguredSidebar(); };

        _layout.Children.Clear();
        _layout.RowDefinitions = new RowDefinitions("68,*");
        _layout.ColumnDefinitions = new ColumnDefinitions(_launcherState.SidebarCollapsed ? "68,*" : _compactLayout ? "175,*" : "228,*");
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
        _notificationButton = new Button { MinWidth = 98, CornerRadius = new CornerRadius(ControlCornerRadius) };
        UpdateNotificationsCounter();
        _notificationButton.Click += (_, _) => ShowNotificationsPage();
        tools.Children.Add(_notificationButton);
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
        brand.Children.Add(Text(_compactLayout ? "VCC OFFICE" : "VCC OFFICE SUITE", _compactLayout ? 17 : 23, true));
        headerContent.Children.Add(brand);
        header.Child = headerContent;
        Grid.SetColumnSpan(header, 2);
        _layout.Children.Add(header);

        var sidebar = new Border
        {
            Background = Brush.Parse(HighContrast ? "#000000" : Dark ? "#101B2D" : "#EEF3FA"),
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(_launcherState.SidebarCollapsed ? 5 : 13)
        };
        _sidebarDashboardButtons.Clear();
        _sidebarModuleButtons.Clear();
        var navigation = new StackPanel
        {
            Spacing = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            // Keep every menu entry inside the usable width, clear of the vertical scrollbar.
            Margin = new Thickness(0, 0, _launcherState.SidebarCollapsed ? 4 : 12, 0)
        };
        var collapse = Nav(_launcherState.SidebarCollapsed ? "»   Expandir menu" : "«   Recolher menu", () =>
        {
            _launcherState = _launcherState with { SidebarCollapsed = !_launcherState.SidebarCollapsed };
            _ = SaveLauncherAsync();
            Render();
        });
        navigation.Children.Add(collapse);
        var homeButton = Nav("⌂   Início", () => Switch(DashboardIds.Modern));
        _sidebarDashboardButtons[DashboardIds.Modern] = homeButton;
        navigation.Children.Add(homeButton);
        _launcherFocusButton = Nav("▦   Todas as aplicações", () => Switch(DashboardIds.Grid),
            _activeDashboard == DashboardIds.Grid);
        SetAccessibleName(_launcherFocusButton, "Abrir Launcher de aplicações");
        _sidebarDashboardButtons[DashboardIds.Grid] = _launcherFocusButton;
        navigation.Children.Add(_launcherFocusButton);
        var classicButton = Nav("▣   Dashboard clássico", () => Switch(DashboardIds.Classic));
        _sidebarDashboardButtons[DashboardIds.Classic] = classicButton;
        navigation.Children.Add(classicButton);
        navigation.Children.Add(Nav("◷   Ficheiros recentes", () => ShowRecentPage(false)));
        navigation.Children.Add(Nav("★   Favoritos", () => ShowRecentPage(true)));
        navigation.Children.Add(Nav("▦   Modelos", ShowTemplatesCatalog));
        navigation.Children.Add(Nav("♧   Notificações", ShowNotificationsPage));
        navigation.Children.Add(Nav("⚕   Diagnóstico", ShowDiagnosticsPage));
        navigation.Children.Add(Nav("♙   Perfis do Launcher", ShowLauncherProfiles));
        navigation.Children.Add(Nav("⚙   Definições", ShowLauncherSettings));
        navigation.Children.Add(Nav("↻   Atualizações", ShowSuiteUpdates));
        navigation.Children.Add(Nav("☁   Cloud", ShowCloudFolder));
        navigation.Children.Add(Nav("◫   Plugins", ShowPluginManager));
        navigation.Children.Add(new Border { Height = 1, Background = LineBrush, Margin = new Thickness(0, 17, 0, 9) });
        var favorites = OrderedApplications()
            .Where(app => _launcherState.FavoriteApplications.Contains(app.Id,
                StringComparer.OrdinalIgnoreCase)).ToArray();
        if (favorites.Length > 0)
        {
            if (!_launcherState.SidebarCollapsed)
                navigation.Children.Add(Text("FAVORITOS", 11, true, Muted));
            foreach (var app in favorites)
            {
                var favorite = Nav("★  " + app.Name, () => OpenModule(app));
                AttachApplicationMenu(favorite, app);
                navigation.Children.Add(favorite);
            }
        }
        if (!_launcherState.SidebarCollapsed)
            navigation.Children.Add(Text("APLICAÇÕES", 11, true, Muted));
        foreach (var app in OrderedApplications())
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            line.Children.Add(ModuleIcon(app.Id, 25));
            if (!_launcherState.SidebarCollapsed) line.Children.Add(Text(app.Name, 13));
            var selected = app.Id == _selectedSidebarModuleId;
            var item = new Button
            {
                Content = line,
                CornerRadius = new CornerRadius(ControlCornerRadius),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = selected ? Brush.Parse(Dark ? "#263C62" : "#DCE9FC") : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(_launcherState.SidebarCollapsed ? 4 : 7, 5),
                Margin = new Thickness(0, 2)
            };
            AutomationProperties.SetName(item, "Abrir " + app.Name);
            ToolTip.SetTip(item, ModuleTooltip(app));
            item.Click += (_, _) => OpenModule(app);
            AttachApplicationMenu(item, app);
            _sidebarModuleButtons[app.Id] = item;
            navigation.Children.Add(item);
        }
        // Configured shortcuts are not installed DLL plugins and are not applications.
        RefreshSidebarSelection();
        sidebar.Child = new ScrollViewer
        {
            Content = navigation,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(sidebar, 1);
        _layout.Children.Add(sidebar);

        _body = new StackPanel { Spacing = _launcherState.VisualDensity == "compact" ? 12 : 20,
            Margin = _launcherState.VisualDensity == "compact" ? new Thickness(14) : new Thickness(25, 22, 25, 28) };
        RefreshBody();
        var scroll = new ScrollViewer { Content = _body };
        Grid.SetRow(scroll, 1);
        Grid.SetColumn(scroll, 1);
        _layout.Children.Add(scroll);
        ApplyWorkspaceNavigation();
    }

    private void RefreshBody()
    {
        if (_body is null) return;
        _body.Children.Clear();
        if (_diagnosticsPage)
        {
            BuildDiagnosticsPage(_body);
            return;
        }
        if (_notificationsPage)
        {
            BuildNotificationsPage(_body);
            return;
        }
        if (_templatesPage)
        {
            BuildTemplatesCatalog(_body);
            return;
        }
        if (_documentsPage)
        {
            BuildDocumentsPage(_body);
            return;
        }
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
        if (_launcherState.ShowWelcomeBanner && _launcherState.DashboardStyle != "minimal")
        {
        var bannerContent = new StackPanel { Spacing = 5, Margin = new Thickness(25, 12) };
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
            MinHeight = 106,
            Child = bannerContent
        });

        }

        var shortcuts = new WrapPanel { Orientation = Orientation.Horizontal };
        shortcuts.Children.Add(Nav("＋  Novo documento", () => OpenDefaultDocumentEditor()));
        shortcuts.Children.Add(Nav("▱  Abrir ficheiro", () => _ = ChooseLocalFileAsync()));
        shortcuts.Children.Add(Nav("▦  Modelos", ShowTemplatesCatalog));
        root.Children.Add(shortcuts);

        var panels = new WrapPanel { Orientation = Orientation.Horizontal };
        var recent = DocumentPanel("◷  Ficheiros recentes", false, 5);
        var favorites = DocumentPanel("★  Favoritos", true, 5);
        var shared = Card(Text("♧  Partilhados comigo: fonte de colaboração não configurada.", 14, false, Muted));
        foreach (var panel in new[] { recent, favorites, shared })
        {
            panel.Width = _compactLayout ? 240 : 285;
            panel.MinHeight = 178;
            panel.Margin = new Thickness(0, 0, 14, 10);
            panels.Children.Add(panel);
        }
        if (_launcherState.DashboardStyle != "minimal") root.Children.Add(panels);
        if (InstalledApplications.FirstOrDefault(m => m.Id == _launcherState.LastSelectedModuleId) is { } previous)
            root.Children.Add(Text("Última aplicação selecionada: " + previous.Name, 13, false, Muted));
        root.Children.Add(Text("Aplicações", 20, true));
        AddApplicationCards(root, 12);
        var openCatalog = Nav("Ver todas as aplicações", () => Switch(DashboardIds.Grid));
        root.Children.Add(openCatalog);
    }

    private void BuildClassic(StackPanel root)
    {
        root.Children.Add(Text("VCC Office — Dashboard clássico", 28, true));
        root.Children.Add(Text("Crie documentos e aceda aos módulos da suite.", 14, false, Muted));
        if (!string.IsNullOrWhiteSpace(_launcherState.LastOpenedDocumentPath))
            root.Children.Add(Text("Último documento: " +
                System.IO.Path.GetFileName(_launcherState.LastOpenedDocumentPath), 13, false, Muted));
        var creation = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var app in OrderedApplications())
        {
            var tile = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
            var tileIcon = ModuleIcon(app.Id, 40);
            tileIcon.HorizontalAlignment = HorizontalAlignment.Center;
            tile.Children.Add(tileIcon);
            tile.Children.Add(CenteredText(app.Name, 15, true));
            var action = new Button
            {
                Content = tile, Width = 160, Height = 94,
                CornerRadius = new CornerRadius(ControlCornerRadius),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 10, 0),
                Background = Surface
            };
            ToolTip.SetTip(action, ModuleTooltip(app));
            action.Click += (_, _) => OpenModule(app);
            AttachApplicationMenu(action, app);
            creation.Children.Add(action);
        }
        root.Children.Add(Card(creation, new Thickness(12, 10)));
        AddApplicationCards(root, 15);
        AddConfiguredCards(root);
        root.Children.Add(Nav("▱  Abrir ficheiro local", () => _ = ChooseLocalFileAsync()));
        root.Children.Add(Nav("▦  Modelos", ShowTemplatesCatalog));
        root.Children.Add(DocumentPanel(_favoritesOnly ? "★  Favoritos" : "◷  Ficheiros recentes", _favoritesOnly, 30));
    }

    private void BuildGrid(StackPanel root)
    {
        root.Children.Add(Text("Todas as aplicações", 30, true));
        root.Children.Add(Text("Explore os módulos instalados e disponíveis do VCC Office.", 14, false, Muted));
        AddApplicationCards(root, int.MaxValue);
        AddConfiguredCards(root);
        root.Children.Add(Nav("▱  Abrir ficheiro local", () => _ = ChooseLocalFileAsync()));
        root.Children.Add(Nav("▦  Modelos", ShowTemplatesCatalog));
        root.Children.Add(DocumentPanel("◷  Ficheiros recentes", false, 8));
    }

    private void AddApplicationCards(StackPanel root, int maximum)
    {
        var list = OrderedApplications(includeHidden: !string.IsNullOrWhiteSpace(_search.Text))
            .Where(module => string.IsNullOrWhiteSpace(_search.Text) ||
                module.Name.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(maximum)
            .ToArray();
        var cards = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var module in list)
        {
            var box = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
            var moduleIcon = ModuleIcon(module.Id, _activeDashboard == DashboardIds.Grid ? 42 : 34);
            moduleIcon.HorizontalAlignment = HorizontalAlignment.Center;
            box.Children.Add(moduleIcon);
            box.Children.Add(CenteredText(module.Name, 16, true));
            if (_launcherState.DashboardStyle != "minimal")
                box.Children.Add(CenteredText(module.IsAvailable ? "Disponível · offline" : "Em migração", 12, false, Muted));
            var card = new Button
            {
                Content = Card(box, new Thickness(12)),
                CornerRadius = new CornerRadius(ControlCornerRadius),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Width = _launcherState.CardSize switch
                {
                    "small" => 146,
                    "large" => _compactLayout ? 212 : 252,
                    _ => _compactLayout ? 168 : _activeDashboard == DashboardIds.Classic ? 218 : 190
                },
                Height = _launcherState.CardSize switch
                {
                    "small" => 105,
                    "large" => 166,
                    _ => _activeDashboard == DashboardIds.Grid ? 132 : 120
                },
                Margin = new Thickness(0, 0, 10, 10),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            ToolTip.SetTip(card, ModuleTooltip(module));
            card.Click += (_, _) => OpenModule(module);
            AttachApplicationMenu(card, module);
            cards.Children.Add(card);
        }
        if (list.Length == 0)
            root.Children.Add(Text("Nenhuma aplicação encontrada.", 14, false, Muted));
        root.Children.Add(cards);
    }

    private void RefreshConfiguredSidebar()
    {
        if (_configuredSidebar is null) return;
        _configuredSidebar.Children.Clear();
        var personalized = ConfiguredItems(_search.Text);
        if (personalized.Count == 0) return;
        _configuredSidebar.Children.Add(new Border
        {
            Height = 1, Background = LineBrush, Margin = new Thickness(0, 15, 0, 8)
        });
        _configuredSidebar.Children.Add(Text("PERSONALIZADOS", 11, true, Muted));
        foreach (var item in personalized)
            _configuredSidebar.Children.Add(ConfiguredItemButton(item));
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

    // Highlight the module that was actually opened rather than the last one
    // saved in preferences. Updating existing controls preserves sidebar scroll position.
    private void SelectSidebarModule(string moduleId)
    {
        _selectedSidebarModuleId = moduleId;
        RefreshSidebarSelection();
    }

    private void RefreshSidebarSelection()
    {
        foreach (var (dashboardId, button) in _sidebarDashboardButtons)
        {
            var selected = _selectedSidebarModuleId is null && _activeDashboard == dashboardId;
            button.Foreground = selected ? Brushes.White : ForegroundBrush;
            button.Background = selected
                ? Brush.Parse(HighContrast ? "#003399" : "#2158AD")
                : Brush.Parse(HighContrast ? "#000000" : Dark ? "#172439" : "#EEF3FA");
        }

        foreach (var (moduleId, button) in _sidebarModuleButtons)
        {
            var selected = moduleId == _selectedSidebarModuleId;
            button.Background = selected
                ? Brush.Parse(Dark ? "#263C62" : "#DCE9FC")
                : Brushes.Transparent;
        }
    }

    private Control ModuleIcon(string id, double size) =>
        _plugins.TryGet(id, out var plugin) && plugin is not null
            ? plugin.CreateMenuIcon(size)
            : ModuleVisuals.Badge(id, size);

    private string ModuleTooltip(OfficeModule module) =>
        _plugins.TryGet(module.Id, out var plugin) && plugin is not null
            ? plugin.ToolTip
            : module.Name;

    private void OpenDefaultDocumentEditor()
    {
        // Find a suitable installed editor through plugin capabilities.
        var plugin = _plugins.Plugins.FirstOrDefault(module => module.SupportsNewDocument);
        if (plugin is null)
        {
            NotifyFeature("Não existem aplicações instaladas");
            return;
        }
        OpenModule(_registry.Find(plugin.Id));
    }

    private void OpenPlugin(OfficeModule module)
    {
        _launcherState = _launcherState with { LastSelectedModuleId = module.Id };
        _ = SaveLauncherAsync();

        if (_pluginWindows.TryGetValue(module.Id, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        if (!_plugins.TryCreateWindow(module.Id, null, out var pluginWindow) || pluginWindow is null)
        {
            NotifyFeature(module.Name + ": não foi possível iniciar o plugin");
            return;
        }
        pluginWindow.Closed += (_, _) =>
        {
            _pluginWindows.Remove(module.Id);
            TrackModuleClosed(module.Id);
        };
        _pluginWindows.Add(module.Id, pluginWindow);
        pluginWindow.Show(this);
        TrackModuleOpened(module.Id);
    }

    private void OpenModule(OfficeModule? module)
    {
        if (module is null || !_plugins.TryGet(module.Id, out var plugin) || plugin is null)
        {
            NotifyFeature(module?.Name ?? "Aplicação não instalada");
            return;
        }
        SelectSidebarModule(module.Id);
        OpenPlugin(module);
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
                Text = title + ". Verifique se o plugin está instalado e ativo.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        window.ShowDialog(this);
    }
}
