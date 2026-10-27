using System.Collections.ObjectModel;
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
        ApplyTheme(_shell.State.Theme == CoreThemeMode.Dark);
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
            Theme = _darkTheme ? CoreThemeMode.Dark : CoreThemeMode.Light,
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
        _darkTheme = !_darkTheme;
        ApplyTheme(_darkTheme);

        ToggleThemeButton.Content = _darkTheme ? "Tema Claro" : "Tema Escuro";
        await _shell.UpdateAsync(state => state with { Theme = _darkTheme ? CoreThemeMode.Dark : CoreThemeMode.Light });
        StatusText.Text = _darkTheme ? "Tema escuro ativo · offline" : "Tema claro ativo · offline";
    }

    private void ApplyTheme(bool dark)
    {
        _darkTheme = dark;
        Resources["WindowBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#202124" : "#F7F8FA"));
        Resources["PanelBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#2B2D31" : "#FFFFFF"));
        Resources["PrimaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#F5F7FA" : "#17202A"));
        Resources["SecondaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#B7C0CA" : "#5E6B78"));
        Resources["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#4A4F58" : "#D1D5DB"));
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
}
