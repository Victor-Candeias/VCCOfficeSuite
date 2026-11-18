using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>Local inbox shared by launcher operations, documents and plugin lifecycle.</summary>
public sealed partial class MainWindow
{
    private readonly OfficeNotificationService _notificationService =
        new(new CoreDatabase(StoragePaths.ForLocalApplication()));
    private IReadOnlyList<OfficeNotification> _notifications = [];
    private int _unreadNoticeCount;
    private Button? _notificationButton;
    private bool _notificationsPage;
    private bool _unreadNotificationsOnly;
    private OfficeNotificationCategory? _notificationCategory;
    private OfficeNotificationSeverity? _notificationSeverity;
    private string _notificationSearch = "";

    private void ShowNotificationsPage()
    {
        _notificationsPage = true;
        _documentsPage = false;
        _templatesPage = false;
        _selectedSidebarModuleId = null;
        _activeDashboard = DashboardIds.Classic;
        _ = RefreshNotificationsAsync();
        Render();
    }

    private void UpdateNotificationsCounter()
    {
        if (_notificationButton is not null)
            _notificationButton.Content = "♧ Avisos " + (_unreadNoticeCount == 0 ? "" : "(" + _unreadNoticeCount + ")");
    }

    private async Task RefreshNotificationsAsync()
    {
        try
        {
            _notifications = await _notificationService.QueryAsync(limit: 250);
            _unreadNoticeCount = await _notificationService.UnreadCountAsync();
            UpdateNotificationsCounter();
            if (_notificationsPage) RefreshBody();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            // Never recurse through ShowNotice when the notification database is unavailable.
            System.Diagnostics.Debug.WriteLine("Notifications unavailable: " + ex.Message);
        }
    }

    private async Task RecordNotificationAsync(OfficeNotificationCategory category,
        OfficeNotificationSeverity severity, string title, string message, string? source = null)
    {
        try
        {
            await _notificationService.PublishAsync(category, severity, title, message, source);
            await RefreshNotificationsAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            System.Diagnostics.Debug.WriteLine("Could not save notification: " + ex.Message);
        }
    }

    private async Task UpdateNotificationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            await RefreshNotificationsAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            System.Diagnostics.Debug.WriteLine("Could not update notification: " + ex.Message);
        }
    }

    private void BuildNotificationsPage(StackPanel root)
    {
        root.Children.Add(Text("♧  Centro de notificações", 27, true));
        root.Children.Add(Text("Eventos locais de documentos, plugins, operações e erros. O histórico é guardado no computador.", 13, false, Muted));

        var tools = new WrapPanel { Orientation = Orientation.Horizontal };
        var search = new TextBox
        {
            Text = _notificationSearch, PlaceholderText = "Pesquisar avisos",
            MinWidth = 245, Margin = new Thickness(0, 0, 8, 8)
        };
        tools.Children.Add(search);
        var searchButton = new Button { Content = "Pesquisar", Margin = new Thickness(0, 0, 8, 8) };
        searchButton.Click += (_, _) => { _notificationSearch = search.Text ?? ""; RefreshBody(); };
        tools.Children.Add(searchButton);
        var unread = new Button
        {
            Content = _unreadNotificationsOnly ? "Mostrar todos" : "Apenas não lidos",
            Margin = new Thickness(0, 0, 8, 8)
        };
        unread.Click += (_, _) => { _unreadNotificationsOnly = !_unreadNotificationsOnly; RefreshBody(); };
        tools.Children.Add(unread);
        root.Children.Add(tools);

        var choices = new WrapPanel { Orientation = Orientation.Horizontal };
        var categories = new[] { "Todas as categorias", "Sistema", "Documentos", "Plugins", "Operações" };
        var category = new ComboBox
        {
            ItemsSource = categories,
            SelectedIndex = _notificationCategory.HasValue ? (int)_notificationCategory.Value + 1 : 0,
            MinWidth = 180, Margin = new Thickness(0, 0, 10, 8)
        };
        category.SelectionChanged += (_, _) =>
        {
            var value = category.SelectedIndex > 0
                ? (OfficeNotificationCategory?)(category.SelectedIndex - 1) : null;
            if (value == _notificationCategory) return;
            _notificationCategory = value;
            RefreshBody();
        };
        choices.Children.Add(category);
        var levels = new[] { "Todas as gravidades", "Informação", "Sucesso", "Aviso", "Erro" };
        var level = new ComboBox
        {
            ItemsSource = levels,
            SelectedIndex = _notificationSeverity.HasValue ? (int)_notificationSeverity.Value + 1 : 0,
            MinWidth = 160, Margin = new Thickness(0, 0, 10, 8)
        };
        level.SelectionChanged += (_, _) =>
        {
            var value = level.SelectedIndex > 0
                ? (OfficeNotificationSeverity?)(level.SelectedIndex - 1) : null;
            if (value == _notificationSeverity) return;
            _notificationSeverity = value;
            RefreshBody();
        };
        choices.Children.Add(level);
        var markAll = new Button { Content = "Marcar todas como lidas", Margin = new Thickness(0, 0, 10, 8) };
        markAll.Click += async (_, _) =>
            await UpdateNotificationAsync(() => _notificationService.MarkAllReadAsync());
        choices.Children.Add(markAll);
        var clear = new Button { Content = "Limpar lidas", Margin = new Thickness(0, 0, 10, 8) };
        clear.Click += async (_, _) =>
            await UpdateNotificationAsync(() => _notificationService.ClearReadAsync());
        choices.Children.Add(clear);
        root.Children.Add(choices);

        var filtered = _notifications.Where(n =>
            (!_unreadNotificationsOnly || !n.IsRead) &&
            (!_notificationCategory.HasValue || n.Category == _notificationCategory.Value) &&
            (!_notificationSeverity.HasValue || n.Severity == _notificationSeverity.Value) &&
            (string.IsNullOrWhiteSpace(_notificationSearch) ||
                n.Title.Contains(_notificationSearch.Trim(), StringComparison.OrdinalIgnoreCase) ||
                n.Message.Contains(_notificationSearch.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();
        root.Children.Add(Text(filtered.Length + " notificação(ões) · " +
            _unreadNoticeCount + " não lida(s)", 12, false, Muted));
        if (filtered.Length == 0)
        {
            root.Children.Add(Card(Text("Não existem notificações para estes filtros.", 14, false, Muted)));
            return;
        }
        foreach (var n in filtered)
        {
            var body = new StackPanel { Spacing = 7 };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var symbol = n.Severity switch
            {
                OfficeNotificationSeverity.Error => "✕",
                OfficeNotificationSeverity.Warning => "⚠",
                OfficeNotificationSeverity.Success => "✓",
                _ => "ℹ"
            };
            head.Children.Add(Text(symbol + "  " + n.Title + (n.IsRead ? "" : "  • Não lida"),
                15, !n.IsRead));
            body.Children.Add(head);
            body.Children.Add(Text(n.Message, 13));
            body.Children.Add(Text(n.Category + " · " + (n.Source ?? "VCC Office") +
                " · " + n.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 11, false, Muted));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var toggle = new Button { Content = n.IsRead ? "Marcar como não lida" : "Marcar como lida" };
            toggle.Click += async (_, _) =>
                await UpdateNotificationAsync(() => _notificationService.MarkReadAsync(n.Id, !n.IsRead));
            actions.Children.Add(toggle);
            var remove = new Button { Content = "Eliminar" };
            remove.Click += async (_, _) =>
                await UpdateNotificationAsync(() => _notificationService.RemoveAsync(n.Id));
            actions.Children.Add(remove);
            body.Children.Add(actions);
            root.Children.Add(Card(body, new Thickness(14, 10)));
        }
    }
}
