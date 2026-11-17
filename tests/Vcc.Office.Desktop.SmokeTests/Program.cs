using System.Reflection;
using Avalonia;
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

Console.WriteLine("PASS Avalonia smoke: arranque, reconstrução, pesquisa, dashboards, temas e alinhamento visual");
return 0;
