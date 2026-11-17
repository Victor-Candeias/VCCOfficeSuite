using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// Cross-platform visual Ribbon: same search, tab and command navigation contract
/// as the legacy WPF WorkspaceWindow. Commands are navigation records, not fake
/// document actions: document commands need explicit editor integration.
/// </summary>
public sealed class RibbonWindow : Window
{
    private readonly CoreShellService _shell;
    private readonly TextBox _search = new() { Watermark = "Pesquisar separadores e comandos..." };
    private readonly ComboBox _tabs = new() { MinWidth = 170 };
    private readonly StackPanel _commands = new() { Spacing = 9 };
    private readonly TextBlock _status = new() { Text = "A carregar Ribbon..." };
    private IReadOnlyList<RibbonTab> _filtered = [];
    private bool _refreshing;

    public RibbonWindow(CoreShellService shell)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        Title = "VCC Office — Ribbon";
        Icon = AppBranding.LoadWindowIcon();
        Width = 750;
        Height = 560;
        MinWidth = 520;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        var header = new StackPanel { Margin = new Thickness(20, 18, 20, 10), Spacing = 8 };
        header.Children.Add(new TextBlock { Text = "Ribbon", FontSize = 25, FontWeight = FontWeight.SemiBold });
        header.Children.Add(new TextBlock { Text = "Separadores e comandos partilhados com o CoreShell" });
        header.Children.Add(_search);
        header.Children.Add(_tabs);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        _search.TextChanged += (_, _) => Refresh();
        _tabs.SelectionChanged += (_, _) =>
        {
            if (_refreshing || _tabs.SelectedIndex < 0) return;
            RenderCommands(_filtered[_tabs.SelectedIndex]);
        };

        var footer = new Border { Padding = new Thickness(20, 10), Child = _status };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = _commands, Margin = new Thickness(20) });
        Content = root;

        Opened += async (_, _) =>
        {
            try
            {
                await _shell.InitialiseAsync();
                Refresh();
                _search.Focus();
            }
            catch (IOException ex) { _status.Text = "Erro: " + ex.Message; }
            catch (UnauthorizedAccessException ex) { _status.Text = "Erro: " + ex.Message; }
        };
    }

    private void Refresh()
    {
        _filtered = RibbonPolicy.Query(_shell.State.Ribbon, _search.Text);
        _refreshing = true;
        _tabs.ItemsSource = _filtered.Select(tab => tab.Title).ToArray();
        _tabs.SelectedIndex = _filtered.Count > 0 ? 0 : -1;
        _refreshing = false;
        if (_filtered.Count == 0)
        {
            _commands.Children.Clear();
            _status.Text = "Não foram encontrados comandos.";
            return;
        }
        RenderCommands(_filtered[0]);
    }

    private void RenderCommands(RibbonTab tab)
    {
        _commands.Children.Clear();
        _commands.Children.Add(new TextBlock { Text = tab.Title, FontSize = 20, FontWeight = FontWeight.SemiBold });
        foreach (var command in tab.Commands)
        {
            var button = new Button
            {
                Content = command.Glyph + "  " + command.Title + " — " + command.Description,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(9),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 9),
                Margin = new Thickness(0, 3)
            };
            button.Click += async (_, _) =>
            {
                try
                {
                    await _shell.NavigateRibbonAsync(tab.Id, command.Id);
                    _status.Text = "Comando selecionado: " + command.Title +
                        " (navegação registada; execução requer integração com o editor).";
                }
                catch (ArgumentException ex) { _status.Text = "Comando inválido: " + ex.Message; }
                catch (IOException ex) { _status.Text = "Não foi possível guardar: " + ex.Message; }
            };
            _commands.Children.Add(button);
        }
    }
}
