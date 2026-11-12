using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Shell;

public partial class MainWindow : Window
{
    private readonly CoreShellService _shell;
    private readonly HomeScreenModel _homeModel;
    private readonly IDashboardRegistry _dashboardRegistry = new DashboardRegistry();
    private readonly DashboardSqliteRepository _dashboardSettings;
    private DashboardPreferences _dashboardPreferences = DashboardPreferences.Default;
    private bool _dashboardReady;
    private bool _switchingDashboard;
    private bool _stateReady;
    private bool _savingWindow;
    private bool _windowSaved;

    public MainWindow()
    {
        InitializeComponent();
        _homeModel = new HomeScreenModel();
        DataContext = _homeModel;
        _dashboardSettings = new DashboardSqliteRepository(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCC Office", "dashboards.sqlite"));
        DashboardSelector.ItemsSource = _dashboardRegistry.GetAll();
        var statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VCC Office", "shell-state.json");
        _shell = new CoreShellService(new ShellStateStore(statePath));
        Loaded += RestoreWindow;
        Closed += (_, _) => SystemEvents.UserPreferenceChanged -= OnWindowsPreferencesChanged;
        SystemEvents.UserPreferenceChanged += OnWindowsPreferencesChanged;
        Closing += SaveWindow;
        HomeRoot.SizeChanged += AdaptLayout;
        StateChanged += (_, _) => MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private async void RestoreWindow(object sender, RoutedEventArgs e)
    {
        await _shell.InitialiseAsync();
        var area = SystemParameters.WorkArea;
        var saved = _shell.State.Window;
        if (saved == WindowLayout.Default)
        {
            Width = Math.Min(1600, area.Width);
            Height = Math.Min(940, area.Height);
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
        }
        else
        {
            Width = Math.Clamp(saved.Width, MinWidth, Math.Max(MinWidth, area.Width));
            Height = Math.Clamp(saved.Height, MinHeight, Math.Max(MinHeight, area.Height));
            Left = Math.Clamp(saved.Left, area.Left, Math.Max(area.Left, area.Right - Width));
            Top = Math.Clamp(saved.Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
            if (saved.IsMaximized) WindowState = WindowState.Maximized;
        }
        _stateReady = true;
        try
        {
            _dashboardPreferences = _dashboardSettings.Load(Environment.UserName, _dashboardRegistry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            _dashboardPreferences = DashboardPreferences.Default;
        }
        var initial = DashboardPolicy.ResolveStartup(_dashboardPreferences, _dashboardRegistry);
        ActivateDashboard(initial, remember: false);
        _dashboardReady = true;
        AdaptLayout(this, null);
    }

    private async void SaveWindow(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_stateReady || _windowSaved || WindowState == WindowState.Minimized) return;
        e.Cancel = true;
        if (_savingWindow) return;
        _savingWindow = true;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        try
        {
            await _shell.UpdateAsync(state => state with
            {
                Window = new WindowLayout(bounds.Width, bounds.Height, bounds.Left, bounds.Top, WindowState == WindowState.Maximized)
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A state file that cannot be written must not prevent the window closing.
        }
        finally
        {
            _windowSaved = true;
            _ = Dispatcher.InvokeAsync(Close);
        }
    }

    private void AdaptLayout(object sender, SizeChangedEventArgs? e)
    {
        if (CreationItems is null) return;
        var width = HomeRoot.ActualWidth;
        if (width <= 0) return;
        var columns = width < 1360 ? 3 : 6;
        foreach (var panel in FindVisualChildren<UniformGrid>(CreationItems)) panel.Columns = columns;
        BrandSubtitle.Visibility = width < 1320 ? Visibility.Collapsed : Visibility.Visible;
        UserLabel.Visibility = width < 1180 ? Visibility.Collapsed : Visibility.Visible;
        BannerArtwork.Visibility = width < 1260 ? Visibility.Collapsed : Visibility.Visible;
        RightColumn.Width = new GridLength(width < 1260 ? 248 : 282);
        if (WelcomeBanner.ActualWidth > 0)
            WelcomeBanner.Clip = new RectangleGeometry(new Rect(0, 0, WelcomeBanner.ActualWidth, WelcomeBanner.ActualHeight), 14, 14);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void MoreApplicationsPopup_Opened(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => FindVisualChildren<System.Windows.Controls.Button>(MoreApplicationsPopup.Child).FirstOrDefault()?.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
    }


    private void DashboardSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_dashboardReady || _switchingDashboard || DashboardSelector.SelectedValue is not string id) return;
        ActivateDashboard(id, remember: true);
    }

    private void ActivateDashboard(string id, bool remember)
    {
        if (!_dashboardRegistry.Contains(id)) id = DashboardIds.Modern;
        _switchingDashboard = true;
        try
        {
            // Criar a vista antes de modificar o estado apresentado.
            var alternative = id == DashboardIds.Classic ? null : new DashboardView(id, _homeModel, IsDarkTheme(), NavigateDashboard, () => OpenDashboardSettings());
            AlternativeDashboardHost.Content = alternative;
            ClassicDashboardRoot.Visibility = alternative is null ? Visibility.Visible : Visibility.Collapsed;
            AlternativeDashboardHost.Visibility = alternative is null ? Visibility.Collapsed : Visibility.Visible;
            DashboardSelector.SelectedValue = id;
            ApplyDashboardChrome(id);

            if (remember)
            {
                _dashboardPreferences = DashboardPolicy.Select(_dashboardPreferences, id, _dashboardRegistry);
                SaveDashboardPreferences();
            }
        }
        finally { _switchingDashboard = false; }
    }

    private bool IsDarkTheme()
    {
        if (_dashboardPreferences.Theme == Vcc.Office.CoreShell.ThemeMode.Dark) return true;
        if (_dashboardPreferences.Theme == Vcc.Office.CoreShell.ThemeMode.Light) return false;
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int setting && setting == 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    private void OnWindowsPreferencesChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (_dashboardPreferences.Theme != Vcc.Office.CoreShell.ThemeMode.System || !_dashboardReady) return;
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (DashboardSelector.SelectedValue is string selected)
                ActivateDashboard(selected, remember: false);
        });
    }

    private void SaveDashboardPreferences()
    {
        try
        {
            _dashboardSettings.Save(Environment.UserName, _dashboardPreferences);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(this, "Não foi possível guardar as preferências do dashboard. " + ex.Message,
                "VCC Office", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }


    private static SolidColorBrush DashboardBrush(string value) =>
        new((Color)ColorConverter.ConvertFromString(value));

    private void ApplyDashboardChrome(string dashboardId)
    {
        var dark = dashboardId != DashboardIds.Classic && IsDarkTheme();
        TopHeader.Background = DashboardBrush(dark ? "#0D1728" : "#FDFEFF");
        TopHeader.BorderBrush = DashboardBrush(dark ? "#283852" : "#DCE3ED");
        BrandNameText.Foreground = DashboardBrush(dark ? "#F3F6FF" : "#0B1220");
        HeaderSearchSurface.Background = DashboardBrush(dark ? "#111E32" : "#FFFFFF");
        HeaderSearchSurface.BorderBrush = DashboardBrush(dark ? "#384761" : "#CCD6E6");
        GlobalSearchBox.Foreground = DashboardBrush(dark ? "#EAF2FF" : "#172238");
        SearchPlaceholder.Foreground = DashboardBrush(dark ? "#99AACA" : "#667188");
        DashboardSelector.Background = DashboardBrush(dark ? "#1B2B44" : "#FFFFFF");
        DashboardSelector.Foreground = DashboardBrush(dark ? "#F3F7FF" : "#172238");
        HomeRoot.BorderBrush = DashboardBrush(dark ? "#273953" : "#DCE3ED");
        UserLabel.Foreground = DashboardBrush(dark ? "#E7F0FF" : "#172238");
        MaximizeButton.Foreground = DashboardBrush(dark ? "#F1F6FF" : "#24334A");
        foreach (var icon in FindVisualChildren<System.Windows.Shapes.Path>(TopHeader))
            if (icon.Stroke is not null)
                icon.Stroke = DashboardBrush(dark ? "#E9F3FF" : "#213652");
        foreach (var subtitle in FindVisualChildren<System.Windows.Controls.TextBlock>(BrandSubtitle))
            subtitle.Foreground = DashboardBrush(dark ? "#C4D3EC" : "#172238");
    }

    private void NavigateDashboard(string id)
    {
        if (_dashboardRegistry.Contains(id)) ActivateDashboard(id, remember: true);
    }

    private void OpenDashboardSettings()
    {
        var dialog = new DashboardSettingsWindow(_dashboardPreferences, _dashboardRegistry) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        _dashboardPreferences = DashboardPolicy.ValidateOrDefault(dialog.Result, _dashboardRegistry);
        SaveDashboardPreferences();
        if (DashboardSelector.SelectedValue is string selected)
            ActivateDashboard(selected, remember: false);
    }

    private void OpenDashboardSettings_Click(object sender, RoutedEventArgs e) => OpenDashboardSettings();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            GlobalSearchBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && MoreApplicationsPopup.IsOpen)
        {
            MoreApplicationsPopup.IsOpen = false;
            MoreApplicationsButton.Focus();
            e.Handled = true;
        }
    }
}
