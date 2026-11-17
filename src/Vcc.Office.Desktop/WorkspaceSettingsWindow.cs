using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// Replacement for the WPF Workspace configuration surface.
/// Uses the SAME shell-state.json and CoreShellService contracts as WPF.
/// No user settings are copied or converted on first run.
/// </summary>
public sealed class WorkspaceSettingsWindow : Window
{
    private readonly CoreShellService _shell;
    private readonly TabControl _sections = new();
    private readonly TextBlock _status = new() { Text = "A carregar definições..." };
    private readonly StackPanel _launcher = new() { Spacing = 8 };
    private readonly StackPanel _ribbon = new() { Spacing = 8 };
    private readonly StackPanel _shortcuts = new() { Spacing = 8 };
    private readonly StackPanel _accessibility = new() { Spacing = 8 };
    private readonly StackPanel _navigation = new() { Spacing = 8 };
    private readonly StackPanel _window = new() { Spacing = 8 };
    private bool _loaded;

    public WorkspaceSettingsWindow(CoreShellService? shell = null)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCC Office", "shell-state.json");
        _shell = shell ?? new CoreShellService(new ShellStateStore(path));
        Title = "VCC Office — Configuração avançada";
        Icon = AppBranding.LoadWindowIcon();
        Width = 850;
        Height = 720;
        MinWidth = 620;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        var head = new StackPanel { Margin = new Thickness(20, 16, 20, 8), Spacing = 5 };
        head.Children.Add(new TextBlock { Text = "Configuração avançada", FontSize = 25, FontWeight = FontWeight.SemiBold });
        head.Children.Add(new TextBlock { Text = "Compatibilidade de definições WPF / Avalonia · dados locais" });
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);
        var footer = new Border { Padding = new Thickness(20, 9), Child = _status };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        _sections.ItemsSource = new[]
        {
            Section("Launcher", _launcher), Section("Ribbon", _ribbon),
            Section("Atalhos", _shortcuts), Section("Acessibilidade", _accessibility),
            Section("Navegação", _navigation), Section("Janela", _window)
        };
        root.Children.Add(_sections);
        Content = root;
        Opened += async (_, _) =>
        {
            if (_loaded) return;
            await ExecuteAsync(async () =>
            {
                await _shell.InitialiseAsync();
                _loaded = true;
                Render();
                _status.Text = "Configuração recuperada de shell-state.json.";
            });
        };
    }

    public string StatePath => _shell.StateStore.StatePath;

    private static TabItem Section(string header, StackPanel content) =>
        new() { Header = header, Content = new ScrollViewer
        { Content = content, Margin = new Thickness(20, 16) } };

    private static TextBlock Caption(string title) => new()
    {
        Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap
    };

    private static TextBox Field(StackPanel panel, string label, string? value = null)
    {
        panel.Children.Add(Caption(label));
        var field = new TextBox
        {
            Text = value ?? string.Empty,
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(9)
        };
        panel.Children.Add(field);
        return field;
    }

    private static Button Action(StackPanel panel, string label, Func<Task> callback)
    {
        var button = new Button
        {
            Content = label, Padding = new Thickness(13, 9),
            CornerRadius = new CornerRadius(9), HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += async (_, _) => await callback();
        panel.Children.Add(button);
        return button;
    }

    private async Task ExecuteAsync(Func<Task> callback)
    {
        try { await callback(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
            or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _status.Text = "Não foi possível guardar: " + ex.Message;
        }
    }

    private void Render()
    {
        RenderLauncher();
        RenderRibbon();
        RenderShortcuts();
        RenderAccessibility();
        RenderNavigation();
        RenderWindow();
    }

    private void RenderLauncher()
    {
        _launcher.Children.Clear();
        _launcher.Children.Add(Caption("Módulos configurados"));
        foreach (var module in _shell.State.Launcher)
            _launcher.Children.Add(new TextBlock { Text = module.Id + " — " + module.Title });
        var id = Field(_launcher, "ID do módulo");
        var title = Field(_launcher, "Nome");
        var description = Field(_launcher, "Descrição");
        var glyph = Field(_launcher, "Ícone textual", "★");
        Action(_launcher, "Adicionar módulo", () => ExecuteAsync(async () =>
        {
            var items = _shell.State.Launcher.Append(new LauncherItem(
                id.Text?.Trim() ?? "", title.Text?.Trim() ?? "",
                description.Text?.Trim() ?? "", glyph.Text?.Trim() ?? "")).ToArray();
            await _shell.ConfigureLauncherAsync(items);
            RenderLauncher();
            _status.Text = "Launcher guardado.";
        }));
    }

    private void RenderRibbon()
    {
        _ribbon.Children.Clear();
        _ribbon.Children.Add(Caption("Separadores e comandos existentes"));
        foreach (var tab in _shell.State.Ribbon)
            _ribbon.Children.Add(new TextBlock
            {
                Text = tab.Title + ": " + string.Join(", ", tab.Commands.Select(command => command.Title)),
                TextWrapping = TextWrapping.Wrap
            });
        var tabId = Field(_ribbon, "ID do separador");
        var tabTitle = Field(_ribbon, "Título do separador");
        var cmdId = Field(_ribbon, "ID do comando");
        var cmdTitle = Field(_ribbon, "Título do comando");
        var glyph = Field(_ribbon, "Ícone do comando", "+");
        Action(_ribbon, "Adicionar comando ao Ribbon", () => ExecuteAsync(async () =>
        {
            var command = new RibbonCommand(cmdId.Text?.Trim() ?? "",
                cmdTitle.Text?.Trim() ?? "", "Comando configurado pelo utilizador",
                glyph.Text?.Trim() ?? "");
            var tabs = _shell.State.Ribbon.ToList();
            var index = tabs.FindIndex(tab => tab.Id.Equals(tabId.Text?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                tabs[index] = tabs[index] with { Commands = tabs[index].Commands.Append(command).ToArray() };
            else
                tabs.Add(new RibbonTab(tabId.Text?.Trim() ?? "", tabTitle.Text?.Trim() ?? "", [command]));
            await _shell.ConfigureRibbonAsync(tabs);
            RenderRibbon();
            _status.Text = "Ribbon guardado.";
        }));
    }

    private void RenderShortcuts()
    {
        _shortcuts.Children.Clear();
        _shortcuts.Children.Add(Caption("Atalhos registados"));
        foreach (var item in _shell.State.Shortcuts.OrderBy(x => x.Key))
            _shortcuts.Children.Add(new TextBlock { Text = item.Key + " = " + item.Value });
        var command = Field(_shortcuts, "ID do comando");
        var shortcut = Field(_shortcuts, "Combinação de teclas (ex.: Ctrl+L)");
        Action(_shortcuts, "Guardar atalho", () => ExecuteAsync(async () =>
        {
            var shortcuts = new Dictionary<string, string>(_shell.State.Shortcuts, StringComparer.OrdinalIgnoreCase)
            {
                [command.Text?.Trim() ?? ""] = shortcut.Text?.Trim() ?? ""
            };
            await _shell.ConfigureShortcutsAsync(shortcuts);
            RenderShortcuts();
            _status.Text = "Atalho validado e guardado.";
        }));
    }

    private void RenderAccessibility()
    {
        _accessibility.Children.Clear();
        _accessibility.Children.Add(Caption("Preferências de acessibilidade"));
        var contrast = new CheckBox { Content = "Ativar alto contraste",
            IsChecked = _shell.State.HighContrast };
        var motion = new CheckBox { Content = "Reduzir movimento e transições",
            IsChecked = _shell.State.ReducedMotion };
        _accessibility.Children.Add(contrast);
        _accessibility.Children.Add(motion);
        Action(_accessibility, "Guardar acessibilidade", () => ExecuteAsync(async () =>
        {
            await _shell.ConfigureAccessibilityAsync(contrast.IsChecked == true, motion.IsChecked == true);
            _status.Text = "Preferências de acessibilidade guardadas.";
        }));
        _accessibility.Children.Add(new TextBlock
        {
            Text = "As preferências são preservadas no CoreShell. " +
                   "O comportamento visual de alto contraste e movimento reduzido ainda requer validação de interface.",
            TextWrapping = TextWrapping.Wrap
        });
    }

    private void RenderNavigation()
    {
        _navigation.Children.Clear();
        _navigation.Children.Add(Caption("Comportamento de navegação"));
        var focus = new ComboBox
        {
            ItemsSource = new[] { "Launcher", "Ribbon" },
            SelectedIndex = _shell.State.Navigation.InitialFocus == NavigationFocusTarget.Ribbon ? 1 : 0
        };
        var wrap = new CheckBox { Content = "Repetir navegação ao chegar ao último controlo",
            IsChecked = _shell.State.Navigation.WrapFocus };
        _navigation.Children.Add(focus);
        _navigation.Children.Add(wrap);
        Action(_navigation, "Guardar navegação", () => ExecuteAsync(async () =>
        {
            await _shell.ConfigureNavigationAsync(new NavigationSettings(
                focus.SelectedIndex == 1 ? NavigationFocusTarget.Ribbon : NavigationFocusTarget.Launcher,
                wrap.IsChecked == true));
            _status.Text = "Preferências de navegação guardadas.";
        }));
    }

    private void RenderWindow()
    {
        _window.Children.Clear();
        _window.Children.Add(Caption("Dimensões de janela guardadas pelo Workspace"));
        var state = _shell.State.Window;
        var width = Field(_window, "Largura", state.Width.ToString(CultureInfo.InvariantCulture));
        var height = Field(_window, "Altura", state.Height.ToString(CultureInfo.InvariantCulture));
        var max = new CheckBox { Content = "Iniciar maximizada", IsChecked = state.IsMaximized };
        _window.Children.Add(max);
        Action(_window, "Guardar dimensões", () => ExecuteAsync(async () =>
        {
            if (!double.TryParse(width.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ||
                !double.TryParse(height.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                throw new ArgumentException("Dimensões inválidas.");
            await _shell.ConfigureWindowAsync(WindowLayoutPolicy.Normalize(
                new WindowLayout(w, h, state.Left, state.Top, max.IsChecked == true)));
            _status.Text = "Dimensões de janela guardadas.";
        }));
        Action(_window, "Centrar janela", () => NavigateWindowAsync(WindowNavigationAction.Center));
        Action(_window, "Maximizar janela", () => NavigateWindowAsync(WindowNavigationAction.Maximize));
        Action(_window, "Restaurar janela", () => NavigateWindowAsync(WindowNavigationAction.Restore));
    }

    private Task NavigateWindowAsync(WindowNavigationAction action) => ExecuteAsync(async () =>
    {
        var area = Owner?.Screens.Primary?.WorkingArea;
        var left = area?.X ?? 0;
        var top = area?.Y ?? 0;
        var width = area?.Width ?? 1920;
        var height = area?.Height ?? 1080;
        await _shell.NavigateWindowAsync(action, left, top, width, height);
        if (Owner is { } window)
        {
            var layout = _shell.State.Window;
            window.WindowState = layout.IsMaximized ? WindowState.Maximized : WindowState.Normal;
            if (!layout.IsMaximized)
            {
                window.Width = layout.Width;
                window.Height = layout.Height;
                window.Position = new PixelPoint((int)Math.Round(layout.Left), (int)Math.Round(layout.Top));
            }
        }
        RenderWindow();
        _status.Text = "Ação de janela aplicada: " + action;
    });
}
