using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Styling;
using Vcc.Office.Desktop;
using Vcc.Office.Writer;
using Vcc.Office.PluginHost;

AppBuilder.Configure<Application>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
    .SetupWithoutStarting();

var window = new MainWindow();
var render = typeof(MainWindow).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException("MainWindow.Render não encontrado.");
var switchDashboard = typeof(MainWindow).GetMethod("Switch", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException("MainWindow.Switch não encontrado.");
var searchField = typeof(MainWindow).GetField("_search", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException("Campo de pesquisa não encontrado.");

// Every window must have the original VCC Office icon, embedded in the portable assembly.
if (window.Icon is null ||
    typeof(MainWindow).Assembly.GetManifestResourceStream("Vcc.Office.Desktop.AppIcon") is null)
    throw new Exception("O ícone VCC Office não foi incorporado/atribuído à janela.");

// Navigation options must fill the sidebar instead of sizing to their text.
var nav = typeof(MainWindow).GetMethod("Nav", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException("MainWindow.Nav não encontrado.");
var navButton = (Button)nav.Invoke(window, ["Verificação", (Action)(() => { }), false])!;
if (navButton.HorizontalAlignment != Avalonia.Layout.HorizontalAlignment.Stretch)
    throw new Exception("Os botões do menu não ocupam toda a largura da barra lateral.");

// Regression: all sidebar entries must stretch within the scroll viewport,
// with a reserved right gutter so backgrounds never overlap the scrollbar.
var sidebar = ((Grid)window.Content!).Children.OfType<Border>()
    .FirstOrDefault(border => border.Child is ScrollViewer viewer
        && viewer.Content is StackPanel panel
        && panel.Children.OfType<Button>().Any(button =>
            button.Content?.ToString()?.Contains("Início", StringComparison.Ordinal) == true))
    ?? throw new Exception("A barra lateral com scroll não foi encontrada.");
var sidebarScroll = (ScrollViewer)sidebar.Child!;
var sidebarPanel = (StackPanel)sidebarScroll.Content!;
if (sidebarScroll.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled ||
    sidebarScroll.VerticalScrollBarVisibility != ScrollBarVisibility.Auto ||
    sidebarPanel.Margin.Right < 10 ||
    sidebarPanel.HorizontalAlignment != Avalonia.Layout.HorizontalAlignment.Stretch)
    throw new Exception("O menu lateral não reserva espaço antes da scrollbar.");
var sidebarButtons = sidebarPanel.Children.OfType<Button>().ToArray();
if (sidebarButtons.Length < 8 ||
    sidebarButtons.Any(button => button.HorizontalAlignment != Avalonia.Layout.HorizontalAlignment.Stretch))
    throw new Exception("Os botões de navegação e aplicações devem ocupar a mesma largura disponível.");

// Active module highlight must move to the clicked app, not stay on the saved app.
var selectSidebarModule = typeof(MainWindow).GetMethod("SelectSidebarModule",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Atualização da seleção do menu lateral não encontrada.");
var sidebarModulesField = typeof(MainWindow).GetField("_sidebarModuleButtons",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Botões das aplicações não encontrados.");
var sidebarDashboardsField = typeof(MainWindow).GetField("_sidebarDashboardButtons",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Botões dos dashboards não encontrados.");
var moduleButtons = (Dictionary<string, Button>)sidebarModulesField.GetValue(window)!;
var dashboardButtons = (Dictionary<string, Button>)sidebarDashboardsField.GetValue(window)!;
var selectedDashboardBackground = dashboardButtons["modern"].Background?.ToString();
selectSidebarModule.Invoke(window, ["writer"]);
var writerBackground = moduleButtons["writer"].Background?.ToString();
if (writerBackground == moduleButtons["sheets"].Background?.ToString() ||
    dashboardButtons["modern"].Background?.ToString() == selectedDashboardBackground)
    throw new Exception("O Writer não ficou selecionado de forma exclusiva.");
selectSidebarModule.Invoke(window, ["sheets"]);
if (moduleButtons["sheets"].Background?.ToString() != writerBackground ||
    moduleButtons["writer"].Background?.ToString() == writerBackground)
    throw new Exception("A seleção não passou de Writer para Sheets.");
render.Invoke(window, null);
moduleButtons = (Dictionary<string, Button>)sidebarModulesField.GetValue(window)!;
if (moduleButtons["sheets"].Background?.ToString() != writerBackground)
    throw new Exception("A seleção da aplicação perdeu-se ao reconstruir o menu.");
switchDashboard.Invoke(window, ["classic"]);
moduleButtons = (Dictionary<string, Button>)sidebarModulesField.GetValue(window)!;
dashboardButtons = (Dictionary<string, Button>)sidebarDashboardsField.GetValue(window)!;
if (moduleButtons["sheets"].Background?.ToString() == writerBackground ||
    dashboardButtons["classic"].Background?.ToString() != selectedDashboardBackground)
    throw new Exception("Selecionar um dashboard deve remover a seleção da aplicação.");

// The previous crash occurred on the second Render() call after restoring session.
// This tests repeated visual-tree rebuilds without reparenting the search box.
TextBox? lastSearch = null;
for (var i = 0; i < 6; i++)
{
    render.Invoke(window, null);
    var search = (TextBox)searchField.GetValue(window)!;
    if (ReferenceEquals(search, lastSearch))
        throw new Exception("A pesquisa foi reutilizada entre reconstruções; risco de visual parent duplicado.");
    lastSearch = search;
    search.Text = i % 2 == 0 ? "data" : "writer";
}

// Dashboard navigation must remain stable across all three layouts.
foreach (var id in new[] { "modern", "classic", "grid", "modern" })
{
    switchDashboard.Invoke(window, [id]);
    render.Invoke(window, null);
}

// Force both theme branches to rebuild the window.
if (Application.Current is { } app)
{
    app.RequestedThemeVariant = ThemeVariant.Dark;
    render.Invoke(window, null);
    app.RequestedThemeVariant = ThemeVariant.Light;
    render.Invoke(window, null);
}

// Regression: launcher controls should use consistent rounded corners.
if (navButton.CornerRadius.TopLeft < 8)
    throw new Exception("Os botões de navegação não têm cantos arredondados.");
render.Invoke(window, null);
var roundedSearch = (TextBox)searchField.GetValue(window)!;
if (roundedSearch.CornerRadius.TopLeft < 8)
    throw new Exception("A caixa de pesquisa não tem cantos arredondados.");

// Regression: card titles and subtitles must sit centered below their icons.
var centeredText = typeof(MainWindow).GetMethod("CenteredText", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException("Helper CenteredText não encontrado.");
var title = (TextBlock)centeredText.Invoke(window, ["VCC writer", 16.0, true, null])!;
if (title.TextAlignment != Avalonia.Media.TextAlignment.Center ||
    title.HorizontalAlignment != Avalonia.Layout.HorizontalAlignment.Stretch)
    throw new Exception("O texto dos cartões não está centrado.");

var cardsMethod = typeof(MainWindow).GetMethod("AddApplicationCards", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException("AddApplicationCards não encontrado.");
var root = new StackPanel();
cardsMethod.Invoke(window, [root, 4]);
var cards = root.Children.OfType<WrapPanel>().FirstOrDefault()
    ?? throw new Exception("Grelha de cartões não encontrada.");
foreach (var cardButton in cards.Children.OfType<Button>())
{
    if (cardButton.Content is not Border cardBorder || cardBorder.Child is not StackPanel stack)
        throw new Exception("Estrutura do cartão inesperada.");
    if (cardButton.CornerRadius.TopLeft < 8 ||
        stack.Children.Count < 3 ||
        stack.Children[0].HorizontalAlignment != Avalonia.Layout.HorizontalAlignment.Center ||
        stack.Children.OfType<TextBlock>().Any(t => t.TextAlignment != Avalonia.Media.TextAlignment.Center))
        throw new Exception("Ícone ou textos do cartão não estão centrados, ou cantos por arredondar.");
}

// Compact dashboard: shortcuts fit their content without excess vertical space.
var buildClassic = typeof(MainWindow).GetMethod("BuildClassic", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Dashboard clássico não encontrado.");
var classicRoot = new StackPanel();
buildClassic.Invoke(window, [classicRoot]);
var quickPanel = classicRoot.Children.OfType<Border>()
    .FirstOrDefault(border => border.Child is WrapPanel wrap &&
        wrap.Children.OfType<Button>().Any(button => button.Width == 160));
if (quickPanel?.Child is not WrapPanel quickApps || quickPanel.Padding.Top > 12 ||
    quickPanel.Padding.Bottom > 12 || quickApps.Children.OfType<Button>().Count() != 4 ||
    quickApps.Children.OfType<Button>().Any(button => button.Height > 100))
    throw new Exception("Os quatro atalhos do dashboard clássico devem ter altura compacta.");
var classicCards = classicRoot.Children.OfType<WrapPanel>()
    .FirstOrDefault(panel => panel.Children.OfType<Button>().Any());
if (classicCards is null || classicCards.Children.OfType<Button>()
    .Any(button => button.Height > 125 || button.Width > 225))
    throw new Exception("Cartões do dashboard clássico demasiado grandes.");

switchDashboard.Invoke(window, ["grid"]);
var gridRoot = new StackPanel();
cardsMethod.Invoke(window, [gridRoot, 6]);
var gridCards = gridRoot.Children.OfType<WrapPanel>().Single();
if (gridCards.Children.OfType<Button>().Any(button =>
    button.Height > 135 || button.Width > 195))
    throw new Exception("Cartões do catálogo não respeitam dimensões compactas.");
switchDashboard.Invoke(window, ["modern"]);

// New Avalonia Sheets and Slides surfaces must initialise and expose usable editors.
var sheetsWindow = new SheetsWindow();
if (sheetsWindow.Icon is null || sheetsWindow.Title?.Contains("Sheets", StringComparison.Ordinal) != true)
    throw new Exception("VCC Sheets Avalonia não foi inicializado.");
var sheetsAddress = typeof(SheetsWindow).GetField("_address", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Editor de célula Sheets não encontrado.");
if (((TextBox)sheetsAddress.GetValue(sheetsWindow)!).Text != "A1")
    throw new Exception("Sheets deve iniciar na célula A1.");

var slidesWindow = new SlidesWindow();
if (slidesWindow.Icon is null || slidesWindow.Title?.Contains("Slides", StringComparison.Ordinal) != true)
    throw new Exception("VCC Slides Avalonia não foi inicializado.");
var slidesNotes = typeof(SlidesWindow).GetField("_notes", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Editor de notas Slides não encontrado.");
if (!((TextBox)slidesNotes.GetValue(slidesWindow)!).AcceptsReturn)
    throw new Exception("Slides deve permitir notas multilinha.");

// VCC Writer opens without native display, supports multiline editing and preserves a single editor.
var writer = new WriterWindow();
if (writer.Icon is null || writer.Title?.StartsWith("VCC Writer", StringComparison.Ordinal) != true)
    throw new Exception("A janela Writer não foi inicializada corretamente.");
var editorField = typeof(WriterWindow).GetField("_editor", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Editor Writer não encontrado.");
var editor = (TextBox)editorField.GetValue(writer)!;
if (!editor.AcceptsReturn || !editor.AcceptsTab)
    throw new Exception("O Writer deve suportar edição de múltiplas linhas e tabulação.");
editor.Text = "Primeira linha\\nSegunda linha";
if (editor.Text != "Primeira linha\\nSegunda linha")
    throw new Exception("O conteúdo editado não foi preservado.");

// Legacy Workspace settings are available without any WPF runtime.
var stateFile = Path.Combine(Path.GetTempPath(), "vcc-wpf-parity-" + Guid.NewGuid().ToString("N") + ".json");
var stateService = new Vcc.Office.CoreShell.CoreShellService(
    new Vcc.Office.CoreShell.ShellStateStore(stateFile));
var workspaceSettings = new WorkspaceSettingsWindow(stateService);
if (workspaceSettings.Icon is null || workspaceSettings.StatePath != Path.GetFullPath(stateFile))
    throw new Exception("A janela de configuração não reutiliza o estado do CoreShell.");
var sectionsField = typeof(WorkspaceSettingsWindow).GetField("_sections", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Separadores de configuração não encontrados.");
var tabs = (TabControl)sectionsField.GetValue(workspaceSettings)!;
var labels = (tabs.ItemsSource as IEnumerable<TabItem>)?.Select(tab => tab.Header?.ToString()).ToArray()
    ?? throw new Exception("Separadores de configuração indisponíveis.");
foreach (var label in new[] { "Launcher", "Ribbon", "Atalhos", "Acessibilidade", "Navegação", "Janela" })
    if (!labels.Contains(label)) throw new Exception("Área WPF sem migração: " + label);

// Ribbon visual surface must exist without WPF references.
var ribbonService = new Vcc.Office.CoreShell.CoreShellService(
    new Vcc.Office.CoreShell.ShellStateStore(
        Path.Combine(Path.GetTempPath(), "vcc-ribbon-" + Guid.NewGuid().ToString("N") + ".json")));
var ribbonWindow = new RibbonWindow(ribbonService);
if (ribbonWindow.Icon is null || ribbonWindow.Title?.Contains("Ribbon", StringComparison.Ordinal) != true)
    throw new Exception("A interface Ribbon Avalonia não foi inicializada.");
var ribbonSearchField = typeof(RibbonWindow).GetField("_search", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("A pesquisa Ribbon está em falta.");
if (((TextBox)ribbonSearchField.GetValue(ribbonWindow)!).PlaceholderText is null)
    throw new Exception("A pesquisa Ribbon não tem indicação acessível.");

// Customized Launcher button must have a label and an explicit description.
var customButtonFactory = typeof(MainWindow).GetMethod("ConfiguredItemButton",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Personalização visual do Launcher não integrada.");
var customButton = (Button)customButtonFactory.Invoke(window,
    [new Vcc.Office.CoreShell.LauncherItem("custom-test", "Módulo Teste", "Ainda sem app", "★")])!;
if (customButton.Content?.ToString()?.Contains("Módulo Teste", StringComparison.Ordinal) != true ||
    ToolTip.GetTip(customButton)?.ToString()?.Contains("sem aplicação associada", StringComparison.Ordinal) != true)
    throw new Exception("Módulo personalizado sem título ou aviso de disponibilidade.");

// The Avalonia launcher must reuse the existing session contract for exactly
// the editors that it can actually instantiate (not all catalog placeholders).
var sessionRegistryProperty = typeof(MainWindow).GetProperty("SessionRegistry",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Registo da recuperação de sessões Avalonia em falta.");
var sessionRegistry = (Vcc.Office.CoreShell.OfficeApplicationRegistry)sessionRegistryProperty.GetValue(window)!;
var availableIds = sessionRegistry.GetAll().Where(item => item.IsAvailable).Select(item => item.Id)
    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
if (!availableIds.SequenceEqual(new[] { "data", "sheets", "slides", "writer" }))
    throw new Exception("A sessão Avalonia deve reconhecer apenas os editores disponíveis, incluindo plugins.");
foreach (var name in new[] { "RestoreModuleSession", "TrackModuleOpened", "TrackModuleClosed" })
    if (typeof(MainWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance) is null)
        throw new Exception("Fluxo de sessão não integrado: " + name);
var sessionStoreField = typeof(MainWindow).GetField("_sessionStore",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Persistência de sessão Avalonia em falta.");
var sessionStore = (Vcc.Office.CoreShell.OfficeSessionStore)sessionStoreField.GetValue(window)!;
if (!sessionStore.FilePath.EndsWith("avalonia-application-session.json", StringComparison.OrdinalIgnoreCase))
    throw new Exception("A sessão Avalonia não pode sobrescrever a sessão WPF.");

// VCC-0820.9: focus policy and accessibility metadata must be present
// on every freshly created visual tree, not only before the first Render().
var layoutField = typeof(MainWindow).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Árvore de navegação Avalonia em falta.");
var layout = (Grid)layoutField.GetValue(window)!;
if (KeyboardNavigation.GetTabNavigation(layout) != KeyboardNavigationMode.Continue)
    throw new Exception("A navegação predefinida deve continuar sem ciclo de Tab.");

var launcherFocusField = typeof(MainWindow).GetField("_launcherFocusButton",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Foco inicial Launcher não implementado.");
var launcherFocus = (Button?)launcherFocusField.GetValue(window);
if (AutomationProperties.GetName(launcherFocus!) != "Abrir Launcher de aplicações")
    throw new Exception("A navegação não expõe o nome acessível do Launcher.");
if (typeof(MainWindow).GetField("_ribbonFocusButton", BindingFlags.NonPublic | BindingFlags.Instance) is not null ||
    typeof(MainWindow).GetMethod("OpenRibbon", BindingFlags.NonPublic | BindingFlags.Instance) is not null)
    throw new Exception("A interface principal ainda expõe a janela Ribbon.");
var menu = (StackPanel)sidebarScroll.Content!;
if (menu.Children.OfType<Button>().Any(button =>
        button.Content?.ToString()?.Contains("Ribbon", StringComparison.OrdinalIgnoreCase) == true))
    throw new Exception("A entrada Ribbon não deve aparecer no menu lateral.");

var navigationMethod = typeof(MainWindow).GetMethod("ApplyWorkspaceNavigation",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Preferências de navegação WPF não aplicadas em Avalonia.");
navigationMethod.Invoke(window, null);
if (KeyboardNavigation.GetTabNavigation(layout) != KeyboardNavigationMode.Continue)
    throw new Exception("As preferências de navegação alteraram o comportamento predefinido.");

var searchAfterRender = (TextBox)searchField.GetValue(window)!;
if (AutomationProperties.GetName(searchAfterRender) != "Pesquisar aplicações")
    throw new Exception("A caixa de pesquisa do Launcher não tem nome acessível.");

// VCC-0820.9: verify both configurations, not only the default state.
var adapterGrid = new Grid();
var firstTarget = new Button { Content = "Launcher" };
var lastTarget = new Button { Content = "Ribbon" };
adapterGrid.Children.Add(firstTarget);
adapterGrid.Children.Add(lastTarget);
var wrapSetting = new Vcc.Office.CoreShell.NavigationSettings(
    Vcc.Office.CoreShell.NavigationFocusTarget.Ribbon, true);
WorkspaceNavigationAdapter.Apply(adapterGrid, wrapSetting, reducedMotion: true);
if (KeyboardNavigation.GetTabNavigation(adapterGrid) != KeyboardNavigationMode.Cycle ||
    WorkspaceNavigationAdapter.InitialFocusTarget(wrapSetting, firstTarget, lastTarget) != lastTarget)
    throw new Exception("O ciclo de Tab e foco inicial Ribbon não foram aplicados.");
if (WorkspaceNavigationAdapter.InitialFocusTarget(
        new Vcc.Office.CoreShell.NavigationSettings(
            Vcc.Office.CoreShell.NavigationFocusTarget.Launcher, false),
        firstTarget, lastTarget) != firstTarget)
    throw new Exception("O foco inicial Launcher não foi respeitado.");
WorkspaceNavigationAdapter.Apply(adapterGrid,
    new Vcc.Office.CoreShell.NavigationSettings(
        Vcc.Office.CoreShell.NavigationFocusTarget.Launcher, false),
    reducedMotion: false);
if (KeyboardNavigation.GetTabNavigation(adapterGrid) != KeyboardNavigationMode.Continue)
    throw new Exception("Não foi possível desativar o ciclo de Tab.");

// VCC-0820: concrete WPF parity for window geometry, Ctrl+K,
// document-module filtering and responsive layout.
foreach (var name in new[] { "RestoreWorkspaceWindow", "SaveWorkspaceWindow" })
    if (typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) is null)
        throw new Exception("A paridade de geometria WPF não está implementada: " + name);

var filterMethod = typeof(MainWindow).GetMethod("SetRecentModuleFilter",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new Exception("Falta o filtro de recentes por módulo.");
filterMethod.Invoke(window, ["writer"]);
var moduleField = typeof(MainWindow).GetField("_recentModuleFilter",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new Exception("Estado de filtro de módulo ausente.");
if ((string?)moduleField.GetValue(window) != "writer")
    throw new Exception("O filtro de recentes não preservou o módulo escolhido.");
filterMethod.Invoke(window, [null]);
if (moduleField.GetValue(window) is not null)
    throw new Exception("Não foi possível limpar o filtro de recentes.");

var compactField = typeof(MainWindow).GetField("_compactLayout",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new Exception("O dashboard não suporta layout compacto.");
compactField.SetValue(window, true);
render.Invoke(window, null);
var columns = (Grid)layoutField.GetValue(window)!;
if (columns.ColumnDefinitions[0].Width.Value != 175)
    throw new Exception("Largura compacta da navegação não aplicada.");
compactField.SetValue(window, false);
render.Invoke(window, null);
if (columns.ColumnDefinitions[0].Width.Value != 228)
    throw new Exception("Layout padrão não foi recuperado.");
if (typeof(MainWindow).GetMethod("WorkspaceKeyDown",
        BindingFlags.Instance | BindingFlags.NonPublic) is null)
    throw new Exception("Atalho de pesquisa global não está implementado.");

// Writer is a real DLL plugin: discovery must work from the staged Plugins/Writer
// folder, while an absent plugin must not prevent the shell from starting.
var emptyPluginsRoot = Path.Combine(Path.GetTempPath(), "vcc-office-plugin-empty-" + Guid.NewGuid().ToString("N"));
if (OfficePluginHost.Discover(emptyPluginsRoot).Plugins.Count != 0)
    throw new Exception("Um launcher sem plugins deve arrancar com catálogo vazio.");

var pluginHost = OfficePluginHost.Discover();
if (pluginHost.Errors.Count != 0 ||
    !pluginHost.TryGet("writer", out var writerPlugin) || writerPlugin is null ||
    writerPlugin.DisplayName != "VCC Writer" ||
    !writerPlugin.ToolTip.Contains("documentos", StringComparison.OrdinalIgnoreCase))
    throw new Exception("O Writer não forneceu nome e tooltip no contrato do plugin.");
if (writerPlugin.CreateMenuIcon(32) is not Border writerMenuIcon ||
    writerMenuIcon.Name != "WriterPluginIcon" ||
    writerMenuIcon.Width != 32 || writerMenuIcon.Height != 32 ||
    writerPlugin.CreateWindowIcon() is null)
    throw new Exception("O Writer não forneceu os seus ícones próprios a partir da DLL.");
if (!pluginHost.TryCreateWindow("writer", null, out var pluginWriterWindow) ||
    pluginWriterWindow is null ||
    pluginWriterWindow.GetType().FullName != typeof(WriterWindow).FullName ||
    pluginWriterWindow.Icon is null)
    throw new Exception("A DLL externa do Writer não criou janela com ícone próprio.");
pluginWriterWindow.Close();

var launcherRegistryField = typeof(MainWindow).GetField("_registry",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new Exception("O catálogo de aplicações do Launcher não foi encontrado.");
var launcherRegistry = (Vcc.Office.CoreShell.OfficeApplicationRegistry)launcherRegistryField.GetValue(window)!;
if (launcherRegistry.Find("writer") is not { IsAvailable: true, Name: "VCC Writer" })
    throw new Exception("O Launcher não utiliza o nome e disponibilidade fornecidos pela DLL Writer.");
if (!writerPlugin.SupportsNewDocument)
    throw new Exception("O plugin Writer deve anunciar a capacidade Novo documento.");
if (launcherRegistry.GetAll().Count != 26)
    throw new Exception("Os 25 módulos base devem ser complementados pelo plugin Writer, sem duplicações.");
var pluginWindowsField = typeof(MainWindow).GetField("_pluginWindows",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("O Launcher não tem gestão genérica de janelas de plugins.");
var pluginWindows = (Dictionary<string, Window>)pluginWindowsField.GetValue(window)!;
if (pluginWindows.Count != 0 || typeof(MainWindow).GetField("_writerWindow",
    BindingFlags.NonPublic | BindingFlags.Instance) is not null)
    throw new Exception("O Host mantém estado específico do Writer.");

var sideButtonsField = typeof(MainWindow).GetField("_sidebarModuleButtons",
    BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new Exception("O menu lateral de módulos não foi encontrado.");
var sideButtons = (Dictionary<string, Button>)sideButtonsField.GetValue(window)!;
if (!sideButtons.TryGetValue("writer", out var writerSideButton) ||
    ToolTip.GetTip(writerSideButton)?.ToString() != writerPlugin.ToolTip ||
    writerSideButton.Content is not StackPanel writerSideContent ||
    writerSideContent.Children[0] is not Border sideWriterIcon ||
    sideWriterIcon.Name != "WriterPluginIcon")
    throw new Exception("O menu lateral não apresenta o tooltip e ícone fornecidos pelo plugin.");


// Writer visual regression: keep Ribbon, A4 page, rulers and editable text intact.
var writerVisualWindow = new WriterWindow();
var writerRoot = writerVisualWindow.Content as DockPanel
    ?? throw new Exception("O Writer deixou de utilizar o layout principal.");
var writerRibbon = writerRoot.Children.OfType<Border>()
    .FirstOrDefault(item => item.Name == "WriterRibbon")
    ?? throw new Exception("Falta o Ribbon do Writer.");
var writerTabs = writerRibbon.Child as TabControl
    ?? throw new Exception("O Ribbon não contém separadores.");
if (writerTabs.Items.Count != 3 ||
    writerTabs.Items.OfType<TabItem>().Select(tab => tab.Header?.ToString())
        .SequenceEqual(new[] { "Ficheiro", "Base", "Ver" }) == false)
    throw new Exception("Os separadores Ficheiro, Base e Ver não estão disponíveis.");

var writerScroll = writerRoot.Children.OfType<ScrollViewer>().Single();
if (writerScroll.HorizontalScrollBarVisibility != ScrollBarVisibility.Auto ||
    writerScroll.VerticalScrollBarVisibility != ScrollBarVisibility.Auto)
    throw new Exception("O Writer não suporta scroll numa página A4 maior do que a janela.");

var writerWorkspace = writerScroll.Content as StackPanel
    ?? throw new Exception("O espaço de edição do Writer não foi encontrado.");
var writerTop = (StackPanel)writerWorkspace.Children[0];
var writerPageRow = (StackPanel)writerWorkspace.Children[1];
var writerBottom = (StackPanel)writerWorkspace.Children[2];
var writerTopRuler = (Border)writerTop.Children[1];
var writerBottomRuler = (Border)writerBottom.Children[1];
var writerVerticalRuler = (Border)writerPageRow.Children[0];
var writerPage = (Border)writerPageRow.Children[1];
var writerEditor = writerPage.Child as TextBox
    ?? throw new Exception("A folha A4 não contém uma área de texto editável.");
if (writerTopRuler.Name != "WriterTopRuler" ||
    writerBottomRuler.Name != "WriterBottomRuler" ||
    writerVerticalRuler.Name != "WriterVerticalRuler" ||
    writerPage.Name != "WriterPage" ||
    writerEditor.Name != "WriterEditor" ||
    writerPage.Width <= 0 || writerPage.Height <= 0 ||
    writerTopRuler.Width != writerPage.Width ||
    writerBottomRuler.Width != writerPage.Width ||
    writerVerticalRuler.Height != writerPage.Height ||
    writerPage.Background?.ToString() != Avalonia.Media.Brushes.White.ToString() ||
    writerPage.Padding.Left <= 0 || writerPage.Padding.Left >= writerPage.Width / 2)
    throw new Exception("Dimensões, cor ou réguas da página A4 incorretas.");

// Attach the window to the headless visual tree before simulating user editing.
// Avalonia TextBox notifications can be deferred until the control is attached.
writerVisualWindow.Show();
writerEditor.Text = "Primeira página de teste";
if (writerEditor.Text != "Primeira página de teste" || writerVisualWindow.Title?.Contains('*') != true)
    throw new Exception("A folha do Writer não permite editar e sinalizar alterações.");

var toggleWriterRulers = typeof(WriterWindow).GetMethod("ToggleRulers",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Não é possível alternar a visualização das réguas.");
toggleWriterRulers.Invoke(writerVisualWindow, null);
if (writerTopRuler.IsVisible || writerBottomRuler.IsVisible || writerVerticalRuler.IsVisible)
    throw new Exception("Ocultar réguas não alterou as três réguas.");
toggleWriterRulers.Invoke(writerVisualWindow, null);
if (!writerTopRuler.IsVisible || !writerBottomRuler.IsVisible || !writerVerticalRuler.IsVisible)
    throw new Exception("Mostrar réguas não restaurou as três réguas.");

Console.WriteLine("PASS Avalonia smoke: arranque, reconstrução, pesquisa, dashboards, temas e alinhamento visual");
return 0;
