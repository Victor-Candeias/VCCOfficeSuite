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

Console.WriteLine("PASS Avalonia smoke: arranque, reconstrução, pesquisa, dashboards e temas");
return 0;
