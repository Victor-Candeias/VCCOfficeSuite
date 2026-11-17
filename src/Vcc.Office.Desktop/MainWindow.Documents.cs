using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Vcc.Office.CoreShell;

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
            Render();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
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
        if (RecentDocumentsService.ResolveModule(path) is null)
        {
            ShowNotice("Formato ainda não suportado",
                "Este tipo de ficheiro não está associado a um módulo do VCC Office.");
            return;
        }

        try
        {
            // O registo é feito antes da abertura externa, tal como na versão WPF.
            // Não afirmar que existe edição Avalonia para DOCX/XLSX/PPTX.
            await _recentDocuments.RegisterAsync(path);
            _launcherState = _launcherState with { LastOpenedDocumentPath = path };
            await SaveLauncherAsync();
            await RefreshRecentAsync();
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            if (process is null)
                ShowNotice("Abertura não confirmada",
                    "O sistema operativo não indicou uma aplicação associada a este formato.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Win32Exception or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            ShowNotice("Não foi possível abrir o documento", ex.Message);
        }
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

    private Border DocumentPanel(string title, bool favoritesOnly, int limit = 6)
    {
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(Text(title, 17, true));
        content.Children.Add(new Border { Height = 1, Background = LineBrush });
        var filtered = _recentEntries
            .Where(document => (!favoritesOnly || document.IsFavorite) &&
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
