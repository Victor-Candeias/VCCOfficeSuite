using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Vcc.Office.Desktop;

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
var sessionRegistryField = typeof(MainWindow).GetField("_sessionRegistry",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Registo da recuperação de sessões Avalonia em falta.");
var sessionRegistry = (Vcc.Office.CoreShell.OfficeApplicationRegistry)sessionRegistryField.GetValue(window)!;
var availableIds = sessionRegistry.GetAll().Where(item => item.IsAvailable).Select(item => item.Id).ToArray();
if (!availableIds.SequenceEqual(new[] { "writer", "sheets", "slides", "data" }))
    throw new Exception("A sessão Avalonia deve abrir apenas os quatro editores funcionais.");
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
var ribbonFocusField = typeof(MainWindow).GetField("_ribbonFocusButton",
    BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Foco inicial Ribbon não implementado.");
var launcherFocus = (Button?)launcherFocusField.GetValue(window);
var ribbonFocus = (Button?)ribbonFocusField.GetValue(window);
if (AutomationProperties.GetName(launcherFocus!) != "Abrir Launcher de aplicações" ||
    AutomationProperties.GetName(ribbonFocus!) != "Abrir Ribbon")
    throw new Exception("A navegação não expõe nomes acessíveis para Launcher e Ribbon.");

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

Console.WriteLine("PASS Avalonia smoke: arranque, reconstrução, pesquisa, dashboards, temas e alinhamento visual");
return 0;
