using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.PluginHost;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>Generic template catalog; editor-specific content belongs only to plugin DLLs.</summary>
public sealed partial class MainWindow
{
    private bool _templatesPage;
    private string _templateSearch = "";
    private string? _templateCategory;
    private string? _templatePluginId;

    private void ShowTemplatesCatalog()
    {
        _diagnosticsPage = false;

        _templatesPage = true;
        _documentsPage = false;
        _notificationsPage = false;
        _selectedSidebarModuleId = null;
        _activeDashboard = DashboardIds.Classic;
        _launcherState = _launcherState with { DashboardId = _activeDashboard };
        _ = SaveLauncherAsync();
        Render();
    }

    private IReadOnlyList<OfficeTemplateEntry> FilterTemplates() =>
        _plugins.Templates.Where(item =>
            (_templatePluginId is null || item.Plugin.Id.Equals(_templatePluginId, StringComparison.OrdinalIgnoreCase)) &&
            (_templateCategory is null || item.Template.Category.Equals(_templateCategory, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(_templateSearch) ||
                item.Template.Title.Contains(_templateSearch.Trim(), StringComparison.OrdinalIgnoreCase) ||
                item.Template.Description.Contains(_templateSearch.Trim(), StringComparison.OrdinalIgnoreCase) ||
                item.Template.Category.Contains(_templateSearch.Trim(), StringComparison.OrdinalIgnoreCase) ||
                item.Plugin.DisplayName.Contains(_templateSearch.Trim(), StringComparison.OrdinalIgnoreCase))
        ).ToArray();

    private void BuildTemplatesCatalog(StackPanel root)
    {
        root.Children.Add(Text("▦  Catálogo de modelos", 27, true));
        root.Children.Add(Text(
            "Crie documentos editáveis a partir de modelos disponibilizados pelos plugins instalados.",
            13, false, Muted));
        var all = _plugins.Templates;
        var searchRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var search = new TextBox
        {
            Text = _templateSearch, PlaceholderText = "Pesquisar modelo, categoria ou aplicação",
            MinWidth = 265, Margin = new Thickness(0, 0, 9, 9)
        };
        searchRow.Children.Add(search);
        var find = new Button { Content = "Pesquisar", Margin = new Thickness(0, 0, 9, 9) };
        find.Click += (_, _) => { _templateSearch = search.Text ?? ""; RefreshBody(); };
        searchRow.Children.Add(find);
        var reset = new Button { Content = "Limpar filtros", Margin = new Thickness(0, 0, 9, 9) };
        reset.Click += (_, _) =>
        {
            _templateSearch = "";
            _templateCategory = null;
            _templatePluginId = null;
            RefreshBody();
        };
        searchRow.Children.Add(reset);
        root.Children.Add(searchRow);

        var selectors = new WrapPanel { Orientation = Orientation.Horizontal };
        var applications = all.Select(item => item.Plugin)
            .DistinctBy(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(plugin => plugin.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        var appIndex = _templatePluginId is null ? 0 :
            Array.FindIndex(applications, p => p.Id.Equals(_templatePluginId,
                StringComparison.OrdinalIgnoreCase)) + 1;
        var apps = new ComboBox
        {
            ItemsSource = new[] { "Todas as aplicações" }.Concat(
                applications.Select(p => p.DisplayName)).ToArray(),
            SelectedIndex = Math.Max(0, appIndex), MinWidth = 180,
            Margin = new Thickness(0, 0, 12, 10)
        };
        apps.SelectionChanged += (_, _) =>
        {
            var selected = apps.SelectedIndex > 0 ? applications[apps.SelectedIndex - 1].Id : null;
            if (selected == _templatePluginId) return;
            _templatePluginId = selected;
            RefreshBody();
        };
        selectors.Children.Add(apps);
        var categories = all.Select(item => item.Template.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase).ToArray();
        var catIndex = _templateCategory is null ? 0 :
            Array.FindIndex(categories, cat => cat.Equals(_templateCategory,
                StringComparison.OrdinalIgnoreCase)) + 1;
        var cats = new ComboBox
        {
            ItemsSource = new[] { "Todas as categorias" }.Concat(categories).ToArray(),
            SelectedIndex = Math.Max(0, catIndex), MinWidth = 185,
            Margin = new Thickness(0, 0, 12, 10)
        };
        cats.SelectionChanged += (_, _) =>
        {
            var selected = cats.SelectedIndex > 0 ? categories[cats.SelectedIndex - 1] : null;
            if (selected == _templateCategory) return;
            _templateCategory = selected;
            RefreshBody();
        };
        selectors.Children.Add(cats);
        root.Children.Add(selectors);

        var templates = FilterTemplates();
        root.Children.Add(Text(templates.Count + " modelo(s) disponível(is)", 12, false, Muted));
        if (templates.Count == 0)
        {
            root.Children.Add(Card(Text(all.Count == 0
                ? "Nenhum plugin instalado e ativo disponibiliza modelos."
                : "Não existem modelos com os filtros selecionados.", 14, false, Muted)));
            return;
        }
        var cards = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var item in templates)
        {
            var card = new StackPanel { Spacing = 9, Margin = new Thickness(14) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            header.Children.Add(item.Plugin.CreateMenuIcon(33));
            header.Children.Add(Text(item.Template.Title, 17, true));
            card.Children.Add(header);
            card.Children.Add(Text(item.Plugin.DisplayName + " · " + item.Template.Category, 12, false, Muted));
            card.Children.Add(Text(item.Template.Description, 13));
            card.Children.Add(new Border
            {
                Background = Brush.Parse(Dark ? "#263952" : "#EDF3FA"),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(10),
                MinHeight = 74,
                Child = new TextBlock
                {
                    Text = item.Template.Preview.Replace("\\n", Environment.NewLine,
                        StringComparison.Ordinal),
                    TextWrapping = TextWrapping.Wrap, Foreground = ForegroundBrush,
                    FontSize = 12, MaxLines = 5
                }
            });
            var create = new Button
            {
                Content = "＋  Criar a partir do modelo",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            create.Click += async (_, _) => await CreateFromTemplateAsync(item);
            card.Children.Add(create);
            cards.Children.Add(new Border
            {
                Width = _compactLayout ? 260 : 305, Background = Surface,
                BorderBrush = LineBrush, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 12, 12), Child = card
            });
        }
        root.Children.Add(cards);
    }

    private async Task CreateFromTemplateAsync(OfficeTemplateEntry entry)
    {
        if (!_plugins.TryGet(entry.Plugin.Id, out var active) ||
            !ReferenceEquals(active, entry.Plugin))
        {
            ShowNotice("Plugin indisponível", "O plugin foi desativado ou removido.");
            RefreshBody();
            return;
        }
        Window? editor = null;
        try
        {
            editor = entry.Plugin.CreateWindow(entry.Plugin.CreateWindowIcon());
            await entry.Plugin.ApplyTemplateAsync(editor, entry.Template.Id);
            var created = editor;
            _documentWindows.Add(created);
            created.Closed += (_, _) => _documentWindows.Remove(created);
            created.Show(this);
            SelectSidebarModule(entry.Plugin.Id);
            TrackModuleOpened(entry.Plugin.Id);
            _ = RecordNotificationAsync(OfficeNotificationCategory.Operation,
                OfficeNotificationSeverity.Success, "Documento criado a partir de modelo",
                entry.Template.Title, entry.Plugin.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
            IOException or UnauthorizedAccessException or NotSupportedException or
            Microsoft.Data.Sqlite.SqliteException)
        {
            editor?.Close();
            ShowNotice("Não foi possível criar o documento", ex.Message);
        }
    }
}
