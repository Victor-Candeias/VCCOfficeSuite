using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Vcc.Office.CoreShell;
using CoreThemeMode = Vcc.Office.CoreShell.ThemeMode;

namespace Vcc.Office.Shell;

public partial class MainWindow : Window
{
    private readonly CoreShellService _shell;
    private bool _darkTheme;

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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _shell.InitialiseAsync();
        foreach (var item in _shell.State.Launcher) LauncherItems.Add(item);
        ApplyTheme(_shell.State.Theme == CoreThemeMode.Dark);
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await _shell.UpdateAsync(state => state with
        {
            Theme = _darkTheme ? CoreThemeMode.Dark : CoreThemeMode.Light,
            Window = state.Window with { Width = ActualWidth, Height = ActualHeight, IsMaximized = WindowState == WindowState.Maximized }
        });
    }

    private async void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _darkTheme = !_darkTheme;
        ApplyTheme(_darkTheme);
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
    }

    private void LauncherItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LauncherItem item }) return;
        StatusText.Text = item.Id == "settings"
            ? "Definições do Core Shell disponíveis no próximo painel"
            : $"{item.Title} está preparado para integração modular";
    }
}
