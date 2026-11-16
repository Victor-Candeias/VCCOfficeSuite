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

// VCC Writer opens without native display, supports multiline editing and preserves a single editor.
var writer = new WriterWindow();
if (writer.Icon is null || !writer.Title.StartsWith("VCC Writer", StringComparison.Ordinal))
    throw new Exception("A janela Writer não foi inicializada corretamente.");
var editorField = typeof(WriterWindow).GetField("_editor", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new Exception("Editor Writer não encontrado.");
var editor = (TextBox)editorField.GetValue(writer)!;
if (!editor.AcceptsReturn || !editor.AcceptsTab)
    throw new Exception("O Writer deve suportar edição de múltiplas linhas e tabulação.");
editor.Text = "Primeira linha\\nSegunda linha";
if (editor.Text != "Primeira linha\\nSegunda linha")
    throw new Exception("O conteúdo editado não foi preservado.");

Console.WriteLine("PASS Avalonia smoke: arranque, reconstrução, pesquisa, dashboards, temas e alinhamento visual");
return 0;
