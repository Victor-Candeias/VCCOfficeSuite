using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Shell;

public partial class MainWindow : Window
{
    private readonly CoreShellService _shell;
    private bool _stateReady;
    private bool _savingWindow;
    private bool _windowSaved;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new HomeScreenModel();
        var statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VCC Office", "shell-state.json");
        _shell = new CoreShellService(new ShellStateStore(statePath));
        Loaded += RestoreWindow;
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
