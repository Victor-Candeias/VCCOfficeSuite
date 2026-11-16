using System.IO;
using System.Diagnostics;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
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
    private readonly RecentDocumentsService _recentDocuments = new(new CoreDatabase(StoragePaths.ForLocalApplication()));
    private bool _favoritesOnly;
    private string? _recentModuleFilter;
    private readonly HomeScreenModel _homeModel;
    private readonly OfficeApplicationRegistry _moduleRegistry = new();
    private readonly OfficeApplicationManager _applicationManager;
    private readonly OfficeSessionStore _sessionStore;
    private OfficeSession _session = OfficeSession.Empty;
    private bool _closingSuite;
    private readonly Dictionary<string, Window> _moduleWindows = new(StringComparer.OrdinalIgnoreCase);
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
        _applicationManager = new OfficeApplicationManager(_moduleRegistry);
        _sessionStore = new OfficeSessionStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCC Office", "application-session.json"));
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
        await RefreshRecentAsync();
        _session = _sessionStore.Load(_moduleRegistry);
        // A interface principal e as preferências devem estar prontas antes de reabrir módulos.
        _ = Dispatcher.BeginInvoke(RestoreApplications, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private async void SaveWindow(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_stateReady || _windowSaved || WindowState == WindowState.Minimized) return;
        e.Cancel = true;
        if (_savingWindow) return;
        _savingWindow = true;
        _closingSuite = true;
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
            var alternative = id == DashboardIds.Classic ? null : new DashboardView(id, _homeModel, IsDarkTheme(), NavigateDashboard, () => OpenDashboardSettings(), OpenApplication,
                BrowseFiles, OpenRecent, SetFavorite);
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
        var captionForeground = DashboardBrush(dark ? "#F1F6FF" : "#24334A");
        MinimizeButton.Foreground = captionForeground;
        MaximizeButton.Foreground = captionForeground;
        CloseButton.Foreground = captionForeground;
        foreach (var icon in FindVisualChildren<System.Windows.Shapes.Path>(TopHeader))
        {
            // Preservar bindings dos glifos da legenda (incluindo hover do botão Fechar).
            // Atribuir Stroke diretamente removeria o binding ao Foreground do Button.
            if (icon.Stroke is not null &&
                System.Windows.Data.BindingOperations.GetBindingBase(
                    icon, System.Windows.Shapes.Shape.StrokeProperty) is null)
                icon.Stroke = DashboardBrush(dark ? "#E9F3FF" : "#213652");
        }

        // Os botões de ações mantêm uma superfície clara, inclusive no modo escuro.
        // Não aplicar o traço branco do cabeçalho a ícones sobre essa superfície:
        // a cor escura garante contraste nos estados normal, hover e pressed.
        var actionIconBrush = DashboardBrush("#264567");
        foreach (var button in new[] { NotificationsButton, SettingsButton })
            foreach (var icon in FindVisualChildren<System.Windows.Shapes.Path>(button))
                if (icon.Stroke is not null)
                    icon.Stroke = actionIconBrush;
        foreach (var subtitle in FindVisualChildren<System.Windows.Controls.TextBlock>(BrandSubtitle))
            subtitle.Foreground = DashboardBrush(dark ? "#C4D3EC" : "#172238");
    }

    private async Task RefreshRecentAsync()
    {
        try
        {
            var query = GlobalSearchBox?.Text;
            var entries = await _recentDocuments.QueryAsync(query, _recentModuleFilter, _favoritesOnly);
            _homeModel.UpdateRecentDocuments(entries);
            RenderRecentDocuments();
            if (_dashboardReady && DashboardSelector.SelectedValue is string id &&
                id != DashboardIds.Classic)
                ActivateDashboard(id, remember: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            MessageBox.Show(this, "Não foi possível consultar os ficheiros recentes: " + ex.Message,
                "VCC Office Suite", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RenderRecentDocuments()
    {
        if (RecentDocumentsPanel is null || RecentEmptyState is null) return;
        RecentDocumentsPanel.Children.Clear();
        var documents = _homeModel.RecentDocuments;
        RecentEmptyState.Visibility = documents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var doc in documents.Take(30))
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var favorite = new Button
            {
                Content = doc.IsFavorite ? "★" : "☆",
                ToolTip = doc.IsFavorite ? "Retirar dos favoritos" : "Adicionar aos favoritos",
                Padding = new Thickness(10, 5, 10, 5),
                Width = 42, Foreground = DashboardBrush("#24549C")
            };
            favorite.Click += (_, _) => SetFavorite(doc.FullPath, !doc.IsFavorite);
            DockPanel.SetDock(favorite, Dock.Right);
            row.Children.Add(favorite);
            var open = new Button
            {
                Content = doc.FileName + (doc.Exists ? "" : " (ficheiro em falta)"),
                ToolTip = doc.FullPath,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 9, 10, 9)
            };
            open.Click += (_, _) => OpenRecent(doc.FullPath);
            row.Children.Add(open);
            RecentDocumentsPanel.Children.Add(row);
        }
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e) => BrowseFiles();

    private void BrowseFiles()
    {
        var picker = new OpenFileDialog
        {
            Title = "Selecionar documento",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Documentos e imagens|*.docx;*.doc;*.odt;*.rtf;*.txt;*.md;*.xlsx;*.xls;*.ods;*.csv;*.pptx;*.ppt;*.odp;*.pdf;*.png;*.jpg;*.jpeg;*.webp;*.svg;*.eml;*.ics;*.vcf;*.db;*.sqlite|Todos os ficheiros (*.*)|*.*"
        };
        if (picker.ShowDialog(this) == true)
            _ = TrackAndOpenAsync(picker.FileName);
    }

    private void OpenRecent(string path) => _ = TrackAndOpenAsync(path);

    private async Task TrackAndOpenAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "O ficheiro foi movido ou eliminado. Pode removê-lo dos recentes ou localizá-lo novamente.",
                    "Ficheiro em falta", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await _recentDocuments.RegisterAsync(path);
            await RefreshRecentAsync();
            // Ainda não existem editores DOCX/XLSX/PPTX integrados: abrir com a aplicação do Windows.
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            MessageBox.Show(this, "Não foi possível abrir o documento: " + ex.Message,
                "VCC Office Suite", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetFavorite(string path, bool favorite) => _ = SetFavoriteAsync(path, favorite);

    private async Task SetFavoriteAsync(string path, bool favorite)
    {
        try
        {
            await _recentDocuments.SetFavoriteAsync(path, favorite);
            await RefreshRecentAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            MessageBox.Show(this, "Não foi possível atualizar o favorito: " + ex.Message,
                "VCC Office Suite", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowFavorites_Click(object sender, RoutedEventArgs e)
    {
        _favoritesOnly = !_favoritesOnly;
        _ = RefreshRecentAsync();
    }

    private void RecentFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (_homeModel is null || sender is not RadioButton button) return;
        _recentModuleFilter = button.Tag as string;
        _ = RefreshRecentAsync();
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_homeModel is not null && _dashboardReady) _ = RefreshRecentAsync();
    }

    private void PersistSession()
    {
        try
        {
            _sessionStore.Save(_session, _moduleRegistry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Não foi possível guardar a sessão das aplicações: " + ex.Message,
                "VCC Office Suite", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RestoreApplications()
    {
        foreach (var id in _session.OpenModuleIds.ToArray())
        {
            // A política já filtrou os módulos inexistentes ou sem interface.
            OpenApplication(id);
        }
    }

    private void OpenApplication(string moduleId)
    {
        if (!_applicationManager.TryResolve(moduleId, out var module) || module is null)
        {
            var name = _homeModel.MainApplications.Concat(_homeModel.MoreApplications)
                .FirstOrDefault(app => string.Equals(app.Id, moduleId, StringComparison.OrdinalIgnoreCase))?.Name
                ?? moduleId;
            MessageBox.Show(this, $"{name} ainda não tem uma interface disponível nesta versão.",
                "VCC Office Suite", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_moduleWindows.TryGetValue(module.Id, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        Window window = module.Id switch
        {
            "data" => new WorkspaceWindow(openDataOnLoad: true),
            _ => throw new InvalidOperationException("Não existe interface registada para este módulo.")
        };
        window.Owner = this;
        window.Closed += (_, _) =>
        {
            _moduleWindows.Remove(module.Id);
            if (_closingSuite) return; // Preservar janelas abertas ao encerrar a suite.
            _session = OfficeSessionPolicy.Close(_session, module.Id, _moduleRegistry);
            PersistSession();
        };
        _moduleWindows[module.Id] = window;
        try
        {
            window.Show();
            _session = OfficeSessionPolicy.Open(_session, module.Id, _moduleRegistry);
            PersistSession();
        }
        catch
        {
            _moduleWindows.Remove(module.Id);
            throw;
        }
    }

    private void Application_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string id })
        {
            MoreApplicationsPopup.IsOpen = false;
            OpenApplication(id);
        }
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
