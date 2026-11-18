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
    private bool _documentsPage;
    private bool _pinnedOnly;
    private string? _documentExtension;
    private string _availabilityFilter = "all";
    private string _documentSearch = "";
    private string _periodFilter = "all";
    private RecentDocumentSort _documentSort = RecentDocumentSort.MostRecent;

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

    private async Task ChangePinnedAsync(RecentDocument document)
    {
        try
        {
            await _recentDocuments.SetPinnedAsync(document.FullPath, !document.IsPinned);
            await RefreshRecentAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            ShowNotice("Não foi possível fixar o documento", ex.Message);
        }
    }

    private async Task RelinkRecentAsync(RecentDocument document)
    {
        try
        {
            var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Localizar " + document.FileName,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Mesmo formato")
                    { Patterns = ["*" + document.Extension] }]
            });
            if (selected.Count == 0) return;
            var newPath = selected[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(newPath))
                throw new InvalidOperationException("Selecione um ficheiro local.");
            await _recentDocuments.RelinkAsync(document.FullPath, newPath);
            if (string.Equals(_launcherState.LastOpenedDocumentPath, document.FullPath,
                StringComparison.OrdinalIgnoreCase))
            {
                _launcherState = _launcherState with { LastOpenedDocumentPath = newPath };
                await SaveLauncherAsync();
            }
            await RefreshRecentAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or Microsoft.Data.Sqlite.SqliteException)
        {
            ShowNotice("Não foi possível relocalizar o documento", ex.Message);
        }
    }

    private async Task CleanMissingDocumentsAsync()
    {
        try
        {
            var count = await _recentDocuments.RemoveMissingAsync();
            await RefreshRecentAsync();
            ShowNotice("Catálogo atualizado", count + " referência(s) indisponível(is) removida(s). Nenhum ficheiro foi eliminado.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            ShowNotice("Não foi possível limpar o catálogo", ex.Message);
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

    private IReadOnlyList<RecentDocument> FilterDocuments(bool favoritesOnly, int limit)
    {
        DateTimeOffset? since = _periodFilter switch
        {
            "7" => DateTimeOffset.UtcNow.AddDays(-7),
            "30" => DateTimeOffset.UtcNow.AddDays(-30),
            _ => null
        };
        var query = _recentEntries.Where(document =>
            (!favoritesOnly || document.IsFavorite) &&
            (!_pinnedOnly || document.IsPinned) &&
            (_recentModuleFilter is null || string.Equals(document.ModuleId,
                _recentModuleFilter, StringComparison.OrdinalIgnoreCase)) &&
            (_documentExtension is null || string.Equals(document.Extension,
                _documentExtension, StringComparison.OrdinalIgnoreCase)) &&
            (_availabilityFilter == "all" || document.Exists == (_availabilityFilter == "present")) &&
            (!since.HasValue || document.OpenedAt >= since.Value) &&
            (string.IsNullOrWhiteSpace(_documentSearch) ||
                document.FileName.Contains(_documentSearch.Trim(), StringComparison.OrdinalIgnoreCase) ||
                document.FullPath.Contains(_documentSearch.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(_search.Text) ||
                document.FileName.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                document.FullPath.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase)));
        query = _documentSort switch
        {
            RecentDocumentSort.Oldest => query.OrderByDescending(item => item.IsPinned)
                .ThenBy(item => item.OpenedAt),
            RecentDocumentSort.Name => query.OrderByDescending(item => item.IsPinned)
                .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase),
            RecentDocumentSort.Extension => query.OrderByDescending(item => item.IsPinned)
                .ThenBy(item => item.Extension, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(item => item.IsPinned)
                .ThenByDescending(item => item.OpenedAt)
        };
        return query.Take(limit).ToArray();
    }

    private void AddDocumentRow(StackPanel content, RecentDocument document, bool detailed)
    {
        var row = new DockPanel { LastChildFill = true };
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var favorite = new Button { Content = document.IsFavorite ? "★" : "☆", MinWidth = 32 };
        ToolTip.SetTip(favorite, document.IsFavorite ? "Retirar dos favoritos" : "Adicionar aos favoritos");
        favorite.Click += async (_, _) => await ChangeFavoriteAsync(document);
        controls.Children.Add(favorite);
        var pin = new Button { Content = document.IsPinned ? "📌" : "◇", MinWidth = 32 };
        ToolTip.SetTip(pin, document.IsPinned ? "Desafixar documento" : "Fixar documento no topo");
        pin.Click += async (_, _) => await ChangePinnedAsync(document);
        controls.Children.Add(pin);
        if (!document.Exists)
        {
            var locate = new Button { Content = "Localizar", MinWidth = 66 };
            ToolTip.SetTip(locate, "Atualizar o caminho de um documento movido");
            locate.Click += async (_, _) => await RelinkRecentAsync(document);
            controls.Children.Add(locate);
        }
        var remove = new Button { Content = "×", MinWidth = 32 };
        ToolTip.SetTip(remove, "Remover do catálogo (sem eliminar o ficheiro)");
        remove.Click += async (_, _) => await RemoveRecentAsync(document);
        controls.Children.Add(remove);
        DockPanel.SetDock(controls, Dock.Right);
        row.Children.Add(controls);
        var information = new StackPanel { Spacing = 2 };
        var open = new Button
        {
            Content = document.FileName + (document.Exists ? "" : "  (em falta)"),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent, Foreground = ForegroundBrush,
            BorderThickness = new Thickness(0), IsEnabled = document.Exists
        };
        ToolTip.SetTip(open, document.FullPath);
        open.Click += async (_, _) => await OpenLocalDocumentAsync(document.FullPath);
        information.Children.Add(open);
        if (detailed)
        {
            information.Children.Add(Text(document.FullPath, 11, false, Muted));
            information.Children.Add(Text(
                document.Extension.ToUpperInvariant() + " · " +
                document.OpenedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm") +
                (document.Exists ? " · Disponível" : " · Em falta") +
                (document.IsPinned ? " · Fixado" : ""), 11, false, Muted));
        }
        row.Children.Add(information);
        content.Children.Add(row);
    }

    private Border DocumentPanel(string title, bool favoritesOnly, int limit = 6)
    {
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(Text(title, 17, true));
        content.Children.Add(new Border { Height = 1, Background = LineBrush });
        if (limit >= 8) AddRecentModuleFilters(content);
        var filtered = FilterDocuments(favoritesOnly, limit);
        if (filtered.Count == 0)
            content.Children.Add(Text(favoritesOnly ? "Ainda não existem favoritos." :
                "Ainda não existem ficheiros recentes.", 13, false, Muted));
        foreach (var document in filtered) AddDocumentRow(content, document, false);
        return Card(content);
    }

    private void BuildDocumentsPage(StackPanel root)
    {
        root.Children.Add(Text(_favoritesOnly ? "★  Documentos favoritos" : "◷  Gestão de documentos", 27, true));
        root.Children.Add(Text("Pesquisar, filtrar, fixar e recuperar referências a documentos locais. Os ficheiros originais não são alterados.",
            13, false, Muted));

        var searchBar = new WrapPanel { Orientation = Orientation.Horizontal };
        var search = new TextBox { Text = _documentSearch, Watermark = "Nome ou caminho do documento",
            MinWidth = 290, Margin = new Thickness(0, 0, 8, 7) };
        searchBar.Children.Add(search);
        var apply = new Button { Content = "Pesquisar", Margin = new Thickness(0, 0, 8, 7) };
        apply.Click += (_, _) => { _documentSearch = search.Text ?? ""; RefreshBody(); };
        searchBar.Children.Add(apply);
        var clear = new Button { Content = "Limpar pesquisa", Margin = new Thickness(0, 0, 8, 7) };
        clear.Click += (_, _) => { _documentSearch = ""; RefreshBody(); };
        searchBar.Children.Add(clear);
        root.Children.Add(searchBar);

        var filters = new WrapPanel { Orientation = Orientation.Horizontal };
        void AddFilter(string title, string[] labels, int selected, Action<int> changed)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 14, 10), Spacing = 4 };
            container.Children.Add(Text(title, 11, false, Muted));
            var combo = new ComboBox { ItemsSource = labels, SelectedIndex = selected, MinWidth = 130 };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedIndex >= 0 && combo.SelectedIndex != selected)
                {
                    changed(combo.SelectedIndex);
                    RefreshBody();
                }
            };
            container.Children.Add(combo);
            filters.Children.Add(container);
        }
        AddFilter("Disponibilidade", ["Todos", "Disponíveis", "Em falta"],
            _availabilityFilter == "present" ? 1 : _availabilityFilter == "missing" ? 2 : 0,
            i => _availabilityFilter = i == 1 ? "present" : i == 2 ? "missing" : "all");
        AddFilter("Última abertura", ["Qualquer data", "Últimos 7 dias", "Últimos 30 dias"],
            _periodFilter == "7" ? 1 : _periodFilter == "30" ? 2 : 0,
            i => _periodFilter = i == 1 ? "7" : i == 2 ? "30" : "all");
        AddFilter("Ordenar", ["Mais recentes", "Mais antigos", "Nome", "Formato"],
            (int)_documentSort, i => _documentSort = (RecentDocumentSort)i);
        var extensions = _recentEntries.Select(item => item.Extension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
        AddFilter("Formato", ["Todos", .. extensions],
            _documentExtension is null ? 0 : Array.FindIndex(extensions,
                x => string.Equals(x, _documentExtension, StringComparison.OrdinalIgnoreCase)) + 1,
            i => _documentExtension = i == 0 ? null : extensions[i - 1]);
        root.Children.Add(filters);

        AddRecentModuleFilters(root);
        var options = new WrapPanel { Orientation = Orientation.Horizontal };
        var pinned = new Button { Content = _pinnedOnly ? "📌  Mostrar todos" : "📌  Apenas fixados",
            Margin = new Thickness(0, 0, 8, 8) };
        pinned.Click += (_, _) => { _pinnedOnly = !_pinnedOnly; RefreshBody(); };
        options.Children.Add(pinned);
        var missing = _recentEntries.Count(item => !item.Exists);
        if (missing > 0)
        {
            var clean = new Button { Content = "Remover " + missing + " referências em falta",
                Margin = new Thickness(0, 0, 8, 8) };
            clean.Click += async (_, _) => await CleanMissingDocumentsAsync();
            options.Children.Add(clean);
        }
        var browse = new Button { Content = "＋  Abrir documento", Margin = new Thickness(0, 0, 8, 8) };
        browse.Click += async (_, _) => await ChooseLocalFileAsync();
        options.Children.Add(browse);
        root.Children.Add(options);

        var filtered = FilterDocuments(_favoritesOnly, 1000);
        root.Children.Add(Text(filtered.Count + " documento(s) encontrado(s)", 12, false, Muted));
        var list = new StackPanel { Spacing = 10 };
        if (filtered.Count == 0) list.Children.Add(Text("Nenhum documento corresponde aos filtros.", 14, false, Muted));
        foreach (var document in filtered)
        {
            var item = new StackPanel { Spacing = 5 };
            AddDocumentRow(item, document, true);
            list.Children.Add(Card(item, new Thickness(12, 8)));
        }
        root.Children.Add(list);
    }

    private void ShowRecentPage(bool favoritesOnly)
    {
        _favoritesOnly = favoritesOnly;
        _documentsPage = true;
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
