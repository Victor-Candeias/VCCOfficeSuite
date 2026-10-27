using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vcc.Office.CoreShell;
using CoreThemeMode = Vcc.Office.CoreShell.ThemeMode;

namespace Vcc.Office.Shell;

public partial class MainWindow : Window
{
    private readonly CoreShellService _shell;
    private bool _darkTheme;
    private CoreThemeMode _themeMode = CoreThemeMode.System;
    private bool _stateReady;

    public MainWindow()
    {
        InitializeComponent();
        var statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VCC Office", "shell-state.json");
        _shell = new CoreShellService(new ShellStateStore(statePath));
        DataContext = this;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    public ObservableCollection<LauncherItem> LauncherItems { get; } = [];
    public ObservableCollection<RibbonTab> RibbonTabs { get; } = [];
    public ObservableCollection<RibbonCommand> RibbonCommands { get; } = [];

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _shell.InitialiseAsync();
        ApplyWindowLayout(_shell.State.Window);
        foreach (var item in _shell.State.Launcher) LauncherItems.Add(item);
        foreach (var tab in _shell.State.Ribbon) RibbonTabs.Add(tab);
        SelectRibbonTab(RibbonTabs.FirstOrDefault());
        _themeMode = _shell.State.Theme;
        ApplyTheme(_themeMode);
        _stateReady = true;
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_stateReady) return;

        var normalBounds = WindowState == WindowState.Normal ?
            new WindowLayout(ActualWidth, ActualHeight, Left, Top, WindowState == WindowState.Maximized) :
            new WindowLayout(RestoreBounds.Width, RestoreBounds.Height, RestoreBounds.Left, RestoreBounds.Top, true);

        await _shell.UpdateAsync(state => state with
        {
            Theme = _themeMode,
            Window = WindowLayoutPolicy.Normalize(normalBounds,
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height)
        });
    }

    private void ApplyWindowLayout(WindowLayout layout)
    {
        var normalized = WindowLayoutPolicy.Normalize(layout,
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);

        Width = normalized.Width;
        Height = normalized.Height;
        Left = normalized.Left;
        Top = normalized.Top;
        WindowState = normalized.IsMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private async void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _themeMode = _darkTheme ? CoreThemeMode.Light : CoreThemeMode.Dark;
        ApplyTheme(_themeMode);

        ToggleThemeButton.Content = _darkTheme ? "Tema Claro" : "Tema Escuro";
        await _shell.ConfigureThemeAsync(_themeMode);
        StatusText.Text = _darkTheme ? "Tema escuro ativo · offline" : "Tema claro ativo · offline";
    }

    private void ApplyTheme(CoreThemeMode mode)
    {
        var dark = mode == CoreThemeMode.Dark;
        _darkTheme = dark;
        Resources["WindowBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#202124" : "#F7F8FA"));
        Resources["PanelBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#2B2D31" : "#FFFFFF"));
        Resources["PrimaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#F5F7FA" : "#17202A"));
        Resources["SecondaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#B7C0CA" : "#5E6B78"));
        Resources["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#4A4F58" : "#D1D5DB"));
        if (ToggleThemeButton is not null) ToggleThemeButton.Content = dark ? "Tema Claro" : "Tema Escuro";
    }

    private void RibbonTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RibbonTab tab }) SelectRibbonTab(tab);
    }

    private void SelectRibbonTab(RibbonTab? tab)
    {
        RibbonCommands.Clear();
        if (tab is null) return;
        foreach (var command in tab.Commands) RibbonCommands.Add(command);
        StatusText.Text = $"Ribbon · {tab.Title} · offline";
    }

    private void RibbonCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RibbonCommand command }) return;
        StatusText.Text = $"{command.Title}: disponível para integração modular · offline";
    }

    private void LauncherItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LauncherItem item }) return;
        StatusText.Text = item.Id == "settings"
            ? "Definições do Core Shell disponíveis no próximo painel"
            : $"{item.Title} está preparado para integração modular";
    }

    private async void ConfigureLauncher_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar Launcher",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };

        var id = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var title = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var description = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var glyph = new TextBox { Margin = new Thickness(0, 4, 0, 10), MaxLength = 2 };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar módulo", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Novo módulo", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "ID técnico", id);
        AddField(panel, "Título", title);
        AddField(panel, "Descrição", description);
        AddField(panel, "Ícone", glyph);
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            try
            {
                var item = new LauncherItem(id.Text.Trim(), title.Text.Trim(), description.Text.Trim(), glyph.Text.Trim());
                await _shell.ConfigureLauncherAsync(_shell.State.Launcher.Append(item));
                LauncherItems.Add(item);
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = $"{item.Title} adicionado ao Launcher · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private async void ConfigureWindow_Click(object sender, RoutedEventArgs e)
    {
        var current = WindowLayoutPolicy.Normalize(_shell.State.Window,
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);
        var dialog = new Window
        {
            Title = "Configurar janela",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };

        var width = new TextBox { Text = current.Width.ToString("0.##", CultureInfo.CurrentCulture), Margin = new Thickness(0, 4, 0, 10) };
        var height = new TextBox { Text = current.Height.ToString("0.##", CultureInfo.CurrentCulture), Margin = new Thickness(0, 4, 0, 10) };
        var maximized = new CheckBox { Content = "Abrir maximizada", IsChecked = current.IsMaximized, Margin = new Thickness(0, 4, 0, 14) };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar configuração", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Dimensões da janela", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "Largura (px)", width);
        AddField(panel, "Altura (px)", height);
        panel.Children.Add(maximized);
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            if (!double.TryParse(width.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var widthValue) ||
                !double.TryParse(height.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var heightValue))
            {
                feedback.Text = "Indique dimensões numéricas válidas.";
                return;
            }

            var layout = WindowLayoutPolicy.Normalize(
                new WindowLayout(widthValue, heightValue, current.Left, current.Top, maximized.IsChecked == true),
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height);
            try
            {
                await _shell.ConfigureWindowAsync(layout);
                ApplyWindowLayout(layout);
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = "Configuração da janela guardada · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private async void ConfigureRibbon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar Ribbon",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var tabId = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var tabTitle = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var commandId = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var commandTitle = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var glyph = new TextBox { Margin = new Thickness(0, 4, 0, 10), MaxLength = 2 };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar configuração", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Novo separador ou comando", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "ID do separador", tabId);
        AddField(panel, "Título do separador", tabTitle);
        AddField(panel, "ID do comando", commandId);
        AddField(panel, "Título do comando", commandTitle);
        AddField(panel, "Ícone do comando", glyph);
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            try
            {
                var command = new RibbonCommand(commandId.Text.Trim(), commandTitle.Text.Trim(), "Comando configurado pelo utilizador", glyph.Text.Trim());
                var tabs = _shell.State.Ribbon.ToList();
                var existingIndex = tabs.FindIndex(tab => string.Equals(tab.Id, tabId.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                {
                    var existing = tabs[existingIndex];
                    tabs[existingIndex] = existing with { Commands = existing.Commands.Append(command).ToArray() };
                }
                else
                {
                    tabs.Add(new RibbonTab(tabId.Text.Trim(), tabTitle.Text.Trim(), [command]));
                }

                await _shell.ConfigureRibbonAsync(tabs);
                RibbonTabs.Clear();
                foreach (var tab in _shell.State.Ribbon) RibbonTabs.Add(tab);
                SelectRibbonTab(RibbonTabs.LastOrDefault());
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = $"Ribbon configurado · {command.Title} · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private async void ConfigureShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar atalhos",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var command = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var gesture = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar atalho", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Novo atalho", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "ID do comando", command);
        AddField(panel, "Combinação (ex.: Ctrl+L)", gesture);
        panel.Children.Add(new TextBlock { Text = "Um comando existente será atualizado.", Foreground = (Brush)Resources["SecondaryText"], Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            try
            {
                var shortcuts = new Dictionary<string, string>(_shell.State.Shortcuts, StringComparer.OrdinalIgnoreCase)
                {
                    [command.Text.Trim()] = gesture.Text.Trim()
                };
                await _shell.ConfigureShortcutsAsync(shortcuts);
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = $"Atalho de '{command.Text.Trim()}' guardado · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private async void ConfigureTheme_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar tema",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var themes = new ComboBox { Margin = new Thickness(0, 4, 0, 14), SelectedValue = _themeMode };
        themes.Items.Add(new ComboBoxItem { Content = "Sistema", Tag = CoreThemeMode.System });
        themes.Items.Add(new ComboBoxItem { Content = "Claro", Tag = CoreThemeMode.Light });
        themes.Items.Add(new ComboBoxItem { Content = "Escuro", Tag = CoreThemeMode.Dark });
        themes.SelectedIndex = _themeMode switch
        {
            CoreThemeMode.Light => 1,
            CoreThemeMode.Dark => 2,
            _ => 0
        };
        var save = new Button { Content = "Guardar tema", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Aparência do Core Shell", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "Modo de tema", themes);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            var mode = (themes.SelectedItem as ComboBoxItem)?.Tag is CoreThemeMode selected
                ? selected
                : CoreThemeMode.System;
            await _shell.ConfigureThemeAsync(mode);
            _themeMode = mode;
            ApplyTheme(mode);
            dialog.DialogResult = true;
            dialog.Close();
            StatusText.Text = $"Tema {ThemeLabel(mode).ToLowerInvariant()} ativo · offline";
        };

        dialog.ShowDialog();
    }

    private static string ThemeLabel(CoreThemeMode mode)
        => mode switch
        {
            CoreThemeMode.Light => "Claro",
            CoreThemeMode.Dark => "Escuro",
            _ => "Sistema"
        };

    private static void AddField(Panel panel, string label, FrameworkElement input)
    {
        panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(input);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        var modifiers = System.Windows.Input.Keyboard.Modifiers;
        if (modifiers == System.Windows.Input.ModifierKeys.None) return;

        var gesture = string.Join('+', new[]
        {
            modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control) ? "Ctrl" : null,
            modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt) ? "Alt" : null,
            modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) ? "Shift" : null,
            key.ToString()
        }.Where(value => value is not null));

        if (!ShortcutCatalog.TryResolveCommand(_shell.State.Shortcuts, gesture, out var commandId)) return;

        e.Handled = true;
        StatusText.Text = commandId switch
        {
            "open-launcher" => "Launcher aberto pelo atalho · offline",
            "open-settings" => "Definições abertas pelo atalho · offline",
            _ => $"Comando '{commandId}' executado pelo atalho · offline"
        };
    }
}
