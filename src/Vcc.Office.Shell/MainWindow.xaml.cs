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
        StateChanged += (_, _) => UpdateCaptionState();
    }

    private async void RestoreWindow(object sender, RoutedEventArgs e)
    {
        await _shell.InitialiseAsync();
        var saved = _shell.State.Window;
        var area = GetWorkAreaForLayout(saved);
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


    // API Win32 para escolher corretamente a área de trabalho do monitor secundário.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public int Flags;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref NativeRect rect, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    private Rect GetWorkAreaForLayout(WindowLayout saved)
    {
        if (saved == WindowLayout.Default) return SystemParameters.WorkArea;
        // WindowLayout está em DIPs; MonitorFromRect espera coordenadas físicas.
        var dpi = VisualTreeHelper.GetDpi(this);
        var requested = new NativeRect
        {
            Left = (int)Math.Round(saved.Left * dpi.DpiScaleX),
            Top = (int)Math.Round(saved.Top * dpi.DpiScaleY),
            Right = (int)Math.Round((saved.Left + saved.Width) * dpi.DpiScaleX),
            Bottom = (int)Math.Round((saved.Top + saved.Height) * dpi.DpiScaleY)
        };
        const uint nearestMonitor = 2;
        var monitor = MonitorFromRect(ref requested, nearestMonitor);
        if (monitor == nint.Zero) return SystemParameters.WorkArea;
        var info = new MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return SystemParameters.WorkArea;
        return new Rect(
            info.Work.Left / dpi.DpiScaleX, info.Work.Top / dpi.DpiScaleY,
            (info.Work.Right - info.Work.Left) / dpi.DpiScaleX,
            (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
    }

    private void UpdateCaptionState()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.Data = (Geometry)FindResource(maximized ? "RestoreGlyph" : "MaximizeGlyph");
        MaximizeButton.ToolTip = maximized ? "Restaurar" : "Maximizar";
    }

    private void Notifications_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "Ainda não existem notificações.", "Notificações",
            MessageBoxButton.OK, MessageBoxImage.Information);
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
            var alternative = id == DashboardIds.Classic ? null : new DashboardView(id, _homeModel, IsDarkTheme());
            AlternativeDashboardHost.Content = alternative;
            ClassicDashboardRoot.Visibility = alternative is null ? Visibility.Visible : Visibility.Collapsed;
            AlternativeDashboardHost.Visibility = alternative is null ? Visibility.Collapsed : Visibility.Visible;
            DashboardSelector.SelectedValue = id;

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

    private void OpenDashboardSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DashboardSettingsWindow(_dashboardPreferences, _dashboardRegistry) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        _dashboardPreferences = DashboardPolicy.ValidateOrDefault(dialog.Result, _dashboardRegistry);
        SaveDashboardPreferences();
        if (DashboardSelector.SelectedValue is string selected)
            ActivateDashboard(selected, remember: false);
    }

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
