using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Vcc.Office.CoreShell;
using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.Desktop;

/// <summary>
/// Adaptador Avalonia para os serviços de ficheiros e sessão já existentes no CoreShell.
/// Sem dependências WPF, Win32 ou cópias dos documentos originais.
/// </summary>
public sealed partial class MainWindow
{
    private readonly CoreDatabase _documentsDatabase = new(StoragePaths.ForLocalApplication());
    private readonly RecentDocumentsService _recentDocuments =
        new(new CoreDatabase(StoragePaths.ForLocalApplication()));
    private DesktopLauncherStateStore? _launcherStateStore;
    private DesktopLauncherState _launcherState = DesktopLauncherState.Default;
    private IReadOnlyList<RecentDocument> _recentEntries = [];
    private bool _favoritesOnly;
    private string? _recentModuleFilter;

    private async Task RestoreLauncherAsync()
    {
        _launcherStateStore = new DesktopLauncherStateStore(_documentsDatabase, _dashboards, _registry);
        try
        {
            _launcherState = await _launcherStateStore.LoadAsync();
            _activeDashboard = DesktopLauncherStatePolicy.ResolveStartup(_launcherState, _dashboards);
            if (Application.Current is { } app)
            {
                app.RequestedThemeVariant = _launcherState.Theme switch
                {
                    ThemeMode.Dark => ThemeVariant.Dark,
                    ThemeMode.Light => ThemeVariant.Light,
                    _ => ThemeVariant.Default
                };
            }
            await RefreshRecentAsync();
            await _workspaceShell.InitialiseAsync();
            RestoreWorkspaceWindow();
            Render();
            ApplyWorkspaceNavigation();
            if (!_initialNavigationFocusApplied)
            {
                _initialNavigationFocusApplied = true;
                Dispatcher.UIThread.Post(FocusConfiguredTarget, DispatcherPriority.Input);
            }
            RestoreModuleSession();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException)
        {
            ShowNotice("Não foi possível recuperar as preferências", ex.Message);
        }
    }

    private async Task SaveLauncherAsync()
    {
        if (_launcherStateStore is null) return;
        try
        {
            await _launcherStateStore.SaveAsync(_launcherState);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            ShowNotice("Não foi possível guardar as preferências", ex.Message);
        }
    }

    private async Task RefreshRecentAsync()
    {
        _recentEntries = await _recentDocuments.QueryAsync();
        RefreshBody();
    }

    private async Task ChooseLocalFileAsync()
    {
        try
        {
            var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Abrir documento local",
                AllowMultiple = false
            });
            if (selected.Count == 0) return;
            var path = selected[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                ShowNotice("Ficheiro não local",
                    "O seletor devolveu um recurso sem caminho local. O acesso a ficheiros remotos ainda não está integrado.");
                return;
            }
            await OpenLocalDocumentAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException)
        {
            ShowNotice("Erro ao selecionar ficheiro", ex.Message);
        }
    }

    private async Task OpenLocalDocumentAsync(string path)
    {
        if (!File.Exists(path))
        {
            ShowNotice("Ficheiro não encontrado",
                "O documento foi movido ou eliminado. Pode removê-lo da lista de recentes.");
            return;
        }
        var candidates = _plugins.DocumentHandlers(path);
        if (candidates.Count == 0)
        {
            ShowNotice("Formato sem editor instalado",
                "Não existe um plugin ativo capaz de abrir " + Path.GetExtension(path) +
                ". Verifique os plugins instalados; o Launcher não executa aplicações externas.");
            return;
        }
        var selected = candidates.Count == 1 ? candidates[0] :
            await ChooseDocumentPluginAsync(candidates, Path.GetFileName(path));
        if (selected is null) return;

        Window? editor = null;
        try
        {
            // Each file gets its own window; an existing unsaved document is never replaced.
            editor = selected.CreateWindow(selected.CreateWindowIcon());
            await selected.OpenDocumentAsync(editor, path);
            var openedEditor = editor;
            _documentWindows.Add(openedEditor);
            openedEditor.Closed += (_, _) => _documentWindows.Remove(openedEditor);
            openedEditor.Show(this);
            SelectSidebarModule(selected.Id);
            TrackModuleOpened(selected.Id);
            await _recentDocuments.RegisterAsync(path);
            _launcherState = _launcherState with { LastOpenedDocumentPath = path,
                LastSelectedModuleId = selected.Id };
            await SaveLauncherAsync();
            await RefreshRecentAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException or InvalidDataException or
            NotSupportedException or FormatException or Microsoft.Data.Sqlite.SqliteException)
        {
            editor?.Close();
            ShowNotice("Não foi possível abrir o documento", ex.Message);
        }
    }

    private readonly HashSet<Window> _documentWindows = [];

    private async Task<IOfficeDocumentPlugin?> ChooseDocumentPluginAsync(
        IReadOnlyList<IOfficeDocumentPlugin> candidates, string fileName)
    {
        var dialog = new Window
        {
            Title = "Escolher aplicação para abrir", Width = 430, Height = 210,
            MinWidth = 370, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var stack = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = "Abrir " + fileName + " com:", TextWrapping = TextWrapping.Wrap
        });
        var choices = new ComboBox
        {
            ItemsSource = candidates.Select(item => item.DisplayName).ToArray(),
            SelectedIndex = 0
        };
        stack.Children.Add(choices);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar" };
        cancel.Click += (_, _) => dialog.Close(false);
        var confirm = new Button { Content = "Abrir" };
        confirm.Click += (_, _) => dialog.Close(true);
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        stack.Children.Add(actions);
        dialog.Content = stack;
        return await dialog.ShowDialog<bool>(this) && choices.SelectedIndex >= 0
            ? candidates[choices.SelectedIndex] : null;
    }

    private async Task ChangeFavoriteAsync(RecentDocument document)
    {
        try
        {
            await _recentDocuments.SetFavoriteAsync(document.FullPath, !document.IsFavorite);
            await RefreshRecentAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            ShowNotice("Não foi possível atualizar os favoritos", ex.Message);
        }
    }

    private async Task RemoveRecentAsync(RecentDocument document)
    {
        try
        {
            await _recentDocuments.RemoveAsync(document.FullPath);
            await RefreshRecentAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            ShowNotice("Não foi possível remover dos recentes", ex.Message);
        }
    }

    private void SetRecentModuleFilter(string? moduleId)
    {
        _recentModuleFilter = string.IsNullOrWhiteSpace(moduleId) ? null : moduleId;
        RefreshBody();
    }

    private void AddRecentModuleFilters(StackPanel content)
    {
        var modules = _recentEntries
            .Where(item => !string.IsNullOrWhiteSpace(item.ModuleId))
            .Select(item => item.ModuleId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var filters = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var id in new[] { (string?)null }.Concat(modules.Cast<string?>()))
        {
            var selected = string.Equals(id, _recentModuleFilter, StringComparison.OrdinalIgnoreCase);
            var label = id is null ? "Todos" : _registry.Find(id)?.Name ?? id;
            var button = new Button
            {
                Content = (selected ? "● " : "○ ") + label,
                Margin = new Thickness(0, 0, 7, 7),
                Padding = new Thickness(10, 6)
            };
            button.Click += (_, _) => SetRecentModuleFilter(id);
            filters.Children.Add(button);
        }
        content.Children.Add(filters);
    }

    private Border DocumentPanel(string title, bool favoritesOnly, int limit = 6)
    {
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(Text(title, 17, true));
        content.Children.Add(new Border { Height = 1, Background = LineBrush });
        if (limit >= 8) AddRecentModuleFilters(content);
        var filtered = _recentEntries
            .Where(document => (!favoritesOnly || document.IsFavorite) &&
                (_recentModuleFilter is null ||
                    string.Equals(document.ModuleId, _recentModuleFilter, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(_search.Text) ||
                 document.FileName.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 document.FullPath.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
            .ToArray();

        if (filtered.Length == 0)
        {
            content.Children.Add(Text(
                favoritesOnly ? "Ainda não existem favoritos." : "Ainda não existem ficheiros recentes.",
                13, false, Muted));
        }

        foreach (var document in filtered)
        {
            var row = new DockPanel { LastChildFill = true };
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var pin = new Button
            {
                Content = document.IsFavorite ? "★" : "☆",
                MinWidth = 32
            };
            ToolTip.SetTip(pin, document.IsFavorite ? "Retirar dos favoritos" : "Adicionar aos favoritos");
            pin.Click += async (_, _) => await ChangeFavoriteAsync(document);
            controls.Children.Add(pin);
            var remove = new Button { Content = "×", MinWidth = 32 };
            ToolTip.SetTip(remove, "Remover dos recentes");
            remove.Click += async (_, _) => await RemoveRecentAsync(document);
            controls.Children.Add(remove);
            DockPanel.SetDock(controls, Dock.Right);
            row.Children.Add(controls);

            var open = new Button
            {
                Content = document.FileName + (document.Exists ? "" : "  (em falta)"),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                Foreground = ForegroundBrush,
                BorderThickness = new Thickness(0)
            };
            ToolTip.SetTip(open, document.FullPath);
            open.Click += async (_, _) => await OpenLocalDocumentAsync(document.FullPath);
            row.Children.Add(open);
            content.Children.Add(row);
        }
        return Card(content);
    }

    private void ShowRecentPage(bool favoritesOnly)
    {
        _favoritesOnly = favoritesOnly;
        _recentModuleFilter = null;
        _activeDashboard = DashboardIds.Classic;
        _launcherState = _launcherState with { DashboardId = _activeDashboard };
        _ = SaveLauncherAsync();
        Render();
    }

    private void ShowNotice(string title, string description)
    {
        var dialog = new Window
        {
            Title = title,
            Icon = AppBranding.LoadWindowIcon(),
            Width = 460,
            Height = 195,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        _ = dialog.ShowDialog(this);
    }
}
