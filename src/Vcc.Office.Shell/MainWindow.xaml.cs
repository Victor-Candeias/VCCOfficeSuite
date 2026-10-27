using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Vcc.Office.CoreShell;
using CoreThemeMode = Vcc.Office.CoreShell.ThemeMode;

namespace Vcc.Office.Shell;

public partial class MainWindow : Window
{
    private readonly CoreShellService _shell;
    private readonly VccDataService _data;
    private readonly Dictionary<string, TextBox> _formInputs = new(StringComparer.OrdinalIgnoreCase);
    private bool _darkTheme;
    private CoreThemeMode _themeMode = CoreThemeMode.System;
    private bool _stateReady;
    private IReadOnlyList<LauncherItem> _launcherCatalog = [];
    private IReadOnlyList<RibbonTab> _ribbonCatalog = [];

    public MainWindow()
    {
        InitializeComponent();
        var statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VCC Office", "shell-state.json");
        _shell = new CoreShellService(new ShellStateStore(statePath));
        _data = new VccDataService(new CoreDatabase(StoragePaths.ForLocalApplication()));
        DataContext = this;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    public ObservableCollection<LauncherItem> LauncherItems { get; } = [];
    public ObservableCollection<RibbonTab> RibbonTabs { get; } = [];
    public ObservableCollection<RibbonCommand> RibbonCommands { get; } = [];

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _shell.InitialiseAsync();
        ApplyWindowLayout(_shell.State.Window);
        _launcherCatalog = _shell.State.Launcher.ToArray();
        RefreshLauncherItems();
        _ribbonCatalog = _shell.State.Ribbon.ToArray();
        RefreshRibbonItems();
        SelectRibbonTab(RibbonTabs.FirstOrDefault());
        _themeMode = _shell.State.Theme;
        ApplyTheme(_themeMode);
        _stateReady = true;
        _ = Dispatcher.BeginInvoke(ApplyNavigationFocus, DispatcherPriority.Input);
        await RefreshDataTablesAsync();
        await RefreshDataFormsAndReportsAsync();
    }

    private async Task RefreshDataTablesAsync()
    {
        try
        {
            DataTablesList.ItemsSource = await _data.ListTablesAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"VCC Data indisponível · {ex.Message}";
        }
    }

    private async Task RefreshDataFormsAndReportsAsync()
    {
        try
        {
            DataFormsList.ItemsSource = await _data.ListFormsAsync();
            DataReportsList.ItemsSource = await _data.ListReportsAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Formulários e relatórios indisponíveis · {ex.Message}";
        }
    }

    private void DataFormsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DataFormFieldsPanel.Children.Clear();
        _formInputs.Clear();
        if (DataFormsList.SelectedItem is not DataFormDefinition form) return;
        foreach (var field in form.Fields)
        {
            var input = new TextBox { Margin = new Thickness(0, 4, 0, 10), ToolTip = field.Required ? "Campo obrigatório" : null };
            _formInputs[field.Name] = input;
            var label = new TextBlock { Text = field.Required ? $"{field.Label} *" : field.Label };
            DataFormFieldsPanel.Children.Add(label);
            DataFormFieldsPanel.Children.Add(input);
        }
    }

    private async void SubmitDataForm_Click(object sender, RoutedEventArgs e)
    {
        if (DataFormsList.SelectedItem is not DataFormDefinition form)
        {
            StatusText.Text = "Selecione um formulário.";
            return;
        }
        try
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in form.Fields)
            {
                var text = _formInputs[field.Name].Text.Trim();
                values[field.Name] = field.Type switch
                {
                    "INTEGER" when long.TryParse(text, out var integer) => integer,
                    "REAL" when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) => real,
                    "INTEGER" or "REAL" => throw new ArgumentException($"O campo «{field.Label}» tem um valor inválido."),
                    _ => text
                };
            }
            await _data.SubmitFormAsync(form, values);
            StatusText.Text = $"Formulário «{form.DisplayName}» guardado · offline";
        }
        catch (Exception ex) { StatusText.Text = $"Formulário inválido · {ex.Message}"; }
    }

    private void DataReportsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataReportsList.SelectedItem is DataReportDefinition report) StatusText.Text = $"Relatório selecionado · {report.DisplayName}";
    }

    private async void RunDataReport_Click(object sender, RoutedEventArgs e)
    {
        if (DataReportsList.SelectedItem is not DataReportDefinition report)
        {
            StatusText.Text = "Selecione um relatório.";
            return;
        }
        try
        {
            var result = await _data.RunReportAsync(report);
            SetDataGridItems(DataReportResultsGrid, result);
            StatusText.Text = $"Relatório «{report.DisplayName}» · {result.Rows.Count} registos · offline";
        }
        catch (Exception ex) { StatusText.Text = $"Relatório inválido · {ex.Message}"; }
    }

    private static void SetDataGridItems(DataGrid grid, DataQueryResult result)
    {
        var table = new DataTable();
        foreach (var column in result.Columns) table.Columns.Add(column);
        foreach (var row in result.Rows) table.Rows.Add(result.Columns.Select(column => row[column] ?? DBNull.Value).ToArray());
        grid.ItemsSource = table.DefaultView;
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        LauncherView.Visibility = Visibility.Collapsed;
        DataPanel.Visibility = Visibility.Visible;
        StatusText.Text = "VCC Data · offline";
        DataQueryBox.Focus();
    }

    private void CloseData_Click(object sender, RoutedEventArgs e)
    {
        DataPanel.Visibility = Visibility.Collapsed;
        LauncherView.Visibility = Visibility.Visible;
        StatusText.Text = "Launcher · offline";
        ConfigureLauncherButton.Focus();
    }

    private async void DataTablesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataTablesList.SelectedItem is not DataTableDefinition table) return;
        DataQueryBox.Text = $"SELECT * FROM \"{table.Name.Replace("\"", "\"\"", StringComparison.Ordinal)}\" LIMIT 100";
        await ExecuteDataQueryAsync();
    }

    private async void ExecuteDataQuery_Click(object sender, RoutedEventArgs e) => await ExecuteDataQueryAsync();

    private async Task ExecuteDataQueryAsync()
    {
        try
        {
            var result = await _data.QueryAsync(DataQueryBox.Text);
            SetDataGridItems(DataResultsGrid, result);
            StatusText.Text = $"VCC Data · {result.Rows.Count} registos · offline";
        }
        catch (Exception ex)
        {
            DataResultsGrid.ItemsSource = null;
            StatusText.Text = $"Consulta inválida · {ex.Message}";
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_stateReady) return;

        var normalBounds = WindowState == WindowState.Normal ?
            new WindowLayout(ActualWidth, ActualHeight, Left, Top, WindowState == WindowState.Maximized) :
            new WindowLayout(RestoreBounds.Width, RestoreBounds.Height, RestoreBounds.Left, RestoreBounds.Top, true);

        await _shell.UpdateAsync(state => state with
        {
            Theme = _themeMode,
            Window = WindowLayoutPolicy.Normalize(normalBounds,
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height)
        });
    }

    private void ApplyWindowLayout(WindowLayout layout)
    {
        var normalized = WindowLayoutPolicy.Normalize(layout,
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);

        Width = normalized.Width;
        Height = normalized.Height;
        Left = normalized.Left;
        Top = normalized.Top;
        WindowState = normalized.IsMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private void ApplyNavigationFocus()
    {
        KeyboardNavigation.SetTabNavigation(this, _shell.State.Navigation.WrapFocus
            ? KeyboardNavigationMode.Cycle
            : KeyboardNavigationMode.Continue);
        var target = _shell.State.Navigation.InitialFocus == NavigationFocusTarget.Ribbon
            ? ConfigureRibbonButton
            : ConfigureLauncherButton;
        target.Focus();
    }

    private async void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _themeMode = _darkTheme ? CoreThemeMode.Light : CoreThemeMode.Dark;
        ApplyTheme(_themeMode);

        ToggleThemeButton.Content = _darkTheme ? "Tema Claro" : "Tema Escuro";
        await _shell.ConfigureThemeAsync(_themeMode);
        StatusText.Text = _darkTheme ? "Tema escuro ativo · offline" : "Tema claro ativo · offline";
    }

    private void ApplyTheme(CoreThemeMode mode)
    {
        var dark = mode == CoreThemeMode.Dark;
        _darkTheme = dark;
        var highContrast = _shell.State.HighContrast;
        Resources["WindowBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(highContrast ? "#000000" : dark ? "#202124" : "#F7F8FA"));
        Resources["PanelBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(highContrast ? "#000000" : dark ? "#2B2D31" : "#FFFFFF"));
        Resources["PrimaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(highContrast ? "#FFFFFF" : dark ? "#F5F7FA" : "#17202A"));
        Resources["SecondaryText"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(highContrast ? "#FFFF00" : dark ? "#B7C0CA" : "#5E6B78"));
        Resources["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(highContrast ? "#FFFFFF" : dark ? "#4A4F58" : "#D1D5DB"));
        if (ToggleThemeButton is not null) ToggleThemeButton.Content = dark ? "Tema Claro" : "Tema Escuro";
    }

    private async void RibbonTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RibbonTab tab }) return;
        SelectRibbonTab(tab);
        try { await _shell.NavigateRibbonAsync(tab.Id); }
        catch (ArgumentException ex) { StatusText.Text = ex.Message; }
    }

    private void SelectRibbonTab(RibbonTab? tab)
    {
        RibbonCommands.Clear();
        if (tab is null) return;
        foreach (var command in tab.Commands) RibbonCommands.Add(command);
        StatusText.Text = $"Ribbon · {tab.Title} · offline";
    }

    private async void RibbonCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RibbonCommand command }) return;
        var tab = RibbonTabs.FirstOrDefault(candidate => candidate.Commands.Contains(command));
        if (tab is null) return;
        try
        {
            await _shell.NavigateRibbonAsync(tab.Id, command.Id);
            StatusText.Text = $"{command.Title} selecionado · offline";
        }
        catch (ArgumentException ex) { StatusText.Text = ex.Message; }
    }

    private void RibbonSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshRibbonItems();

    private void ClearRibbonSearch_Click(object sender, RoutedEventArgs e)
    {
        RibbonSearchBox.Clear();
        RibbonSearchBox.Focus();
    }

    private void RefreshRibbonItems()
    {
        if (RibbonTabs is null) return;
        var results = RibbonPolicy.Query(_ribbonCatalog, RibbonSearchBox?.Text);
        RibbonTabs.Clear();
        foreach (var tab in results) RibbonTabs.Add(tab);
        RibbonResultsText.Text = $"{results.Count} de {_ribbonCatalog.Count} separadores";
        SelectRibbonTab(RibbonTabs.FirstOrDefault());
    }

    private async void LauncherItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LauncherItem item }) return;
        try
        {
            await _shell.NavigateLauncherAsync(item.Id);
            StatusText.Text = item.Id == "settings"
                ? "Definições do Core Shell disponíveis no próximo painel"
                : $"{item.Title} selecionado · offline";
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void LauncherSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshLauncherItems();

    private void ClearLauncherSearch_Click(object sender, RoutedEventArgs e)
    {
        LauncherSearchBox.Clear();
        LauncherSearchBox.Focus();
    }

    private void RefreshLauncherItems()
    {
        if (LauncherItems is null) return;
        var results = LauncherPolicy.Query(_launcherCatalog, LauncherSearchBox?.Text);
        LauncherItems.Clear();
        foreach (var item in results) LauncherItems.Add(item);
        if (LauncherResultsText is not null)
            LauncherResultsText.Text = $"{results.Count} de {_launcherCatalog.Count} módulos";
    }

    private async void ConfigureLauncher_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar Launcher",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };

        var id = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var title = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var description = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var glyph = new TextBox { Margin = new Thickness(0, 4, 0, 10), MaxLength = 2 };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar módulo", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Novo módulo", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "ID técnico", id);
        AddField(panel, "Título", title);
        AddField(panel, "Descrição", description);
        AddField(panel, "Ícone", glyph);
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            try
            {
                var item = new LauncherItem(id.Text.Trim(), title.Text.Trim(), description.Text.Trim(), glyph.Text.Trim());
                await _shell.ConfigureLauncherAsync(_shell.State.Launcher.Append(item));
                _launcherCatalog = _launcherCatalog.Append(item).ToArray();
                RefreshLauncherItems();
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = $"{item.Title} adicionado ao Launcher · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private async void ConfigureWindow_Click(object sender, RoutedEventArgs e)
    {
        var current = WindowLayoutPolicy.Normalize(_shell.State.Window,
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);
        var dialog = new Window
        {
            Title = "Configurar janela",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };

        var width = new TextBox { Text = current.Width.ToString("0.##", CultureInfo.CurrentCulture), Margin = new Thickness(0, 4, 0, 10) };
        var height = new TextBox { Text = current.Height.ToString("0.##", CultureInfo.CurrentCulture), Margin = new Thickness(0, 4, 0, 10) };
        var maximized = new CheckBox { Content = "Abrir maximizada", IsChecked = current.IsMaximized, Margin = new Thickness(0, 4, 0, 14) };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar configuração", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Dimensões da janela", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "Largura (px)", width);
        AddField(panel, "Altura (px)", height);
        panel.Children.Add(maximized);
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            if (!double.TryParse(width.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var widthValue) ||
                !double.TryParse(height.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var heightValue))
            {
                feedback.Text = "Indique dimensões numéricas válidas.";
                return;
            }

            var layout = WindowLayoutPolicy.Normalize(
                new WindowLayout(widthValue, heightValue, current.Left, current.Top, maximized.IsChecked == true),
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height);
            try
            {
                await _shell.ConfigureWindowAsync(layout);
                ApplyWindowLayout(layout);
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = "Configuração da janela guardada · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private void ConsultWindow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Consultar janela",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 430,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var stateText = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
        AutomationProperties.SetName(stateText, "Estado atual da janela");
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Estado da janela", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        panel.Children.Add(stateText);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(actions);
        dialog.Content = panel;

        void RefreshState()
        {
            var current = WindowLayoutPolicy.Normalize(_shell.State.Window,
                SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
            stateText.Text = $"Tamanho: {current.Width:0} × {current.Height:0}\n" +
                             $"Posição: {current.Left:0}, {current.Top:0}\n" +
                             $"Estado: {(current.IsMaximized ? "maximizada" : "normal")}";
        }

        foreach (var (label, action) in new[]
        {
            ("Maximizar", WindowNavigationAction.Maximize),
            ("Restaurar", WindowNavigationAction.Restore),
            ("Centrar", WindowNavigationAction.Center)
        })
        {
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0) };
            AutomationProperties.SetName(button, label + " janela");
            button.Click += async (_, _) =>
            {
                try
                {
                    await _shell.NavigateWindowAsync(action,
                        SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top,
                        SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
                    ApplyWindowLayout(_shell.State.Window);
                    RefreshState();
                    StatusText.Text = $"Janela: {label.ToLowerInvariant()} · offline";
                }
                catch (ArgumentException ex)
                {
                    stateText.Text = ex.Message;
                }
            };
            actions.Children.Add(button);
        }

        RefreshState();
        dialog.ShowDialog();
    }

    private async void ConfigureRibbon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar Ribbon",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var tabId = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var tabTitle = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var commandId = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var commandTitle = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var glyph = new TextBox { Margin = new Thickness(0, 4, 0, 10), MaxLength = 2 };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar configuração", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Novo separador ou comando", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "ID do separador", tabId);
        AddField(panel, "Título do separador", tabTitle);
        AddField(panel, "ID do comando", commandId);
        AddField(panel, "Título do comando", commandTitle);
        AddField(panel, "Ícone do comando", glyph);
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            try
            {
                var command = new RibbonCommand(commandId.Text.Trim(), commandTitle.Text.Trim(), "Comando configurado pelo utilizador", glyph.Text.Trim());
                var tabs = _shell.State.Ribbon.ToList();
                var existingIndex = tabs.FindIndex(tab => string.Equals(tab.Id, tabId.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                {
                    var existing = tabs[existingIndex];
                    tabs[existingIndex] = existing with { Commands = existing.Commands.Append(command).ToArray() };
                }
                else
                {
                    tabs.Add(new RibbonTab(tabId.Text.Trim(), tabTitle.Text.Trim(), [command]));
                }

                await _shell.ConfigureRibbonAsync(tabs);
                _ribbonCatalog = _shell.State.Ribbon.ToArray();
                RefreshRibbonItems();
                SelectRibbonTab(RibbonTabs.LastOrDefault());
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = $"Ribbon configurado · {command.Title} · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private async void ConfigureShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar atalhos",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var command = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var gesture = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar atalho", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Novo atalho", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "ID do comando", command);
        AddField(panel, "Combinação (ex.: Ctrl+L)", gesture);
        panel.Children.Add(new TextBlock { Text = "Um comando existente será atualizado.", Foreground = (Brush)Resources["SecondaryText"], Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            try
            {
                var shortcuts = new Dictionary<string, string>(_shell.State.Shortcuts, StringComparer.OrdinalIgnoreCase)
                {
                    [command.Text.Trim()] = gesture.Text.Trim()
                };
                await _shell.ConfigureShortcutsAsync(shortcuts);
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = $"Atalho de '{command.Text.Trim()}' guardado · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private async void ConfigureTheme_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar tema",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var themes = new ComboBox { Margin = new Thickness(0, 4, 0, 14), SelectedValue = _themeMode };
        themes.Items.Add(new ComboBoxItem { Content = "Sistema", Tag = CoreThemeMode.System });
        themes.Items.Add(new ComboBoxItem { Content = "Claro", Tag = CoreThemeMode.Light });
        themes.Items.Add(new ComboBoxItem { Content = "Escuro", Tag = CoreThemeMode.Dark });
        themes.SelectedIndex = _themeMode switch
        {
            CoreThemeMode.Light => 1,
            CoreThemeMode.Dark => 2,
            _ => 0
        };
        var save = new Button { Content = "Guardar tema", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Aparência do Core Shell", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "Modo de tema", themes);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            var mode = (themes.SelectedItem as ComboBoxItem)?.Tag is CoreThemeMode selected
                ? selected
                : CoreThemeMode.System;
            await _shell.ConfigureThemeAsync(mode);
            _themeMode = mode;
            ApplyTheme(mode);
            dialog.DialogResult = true;
            dialog.Close();
            StatusText.Text = $"Tema {ThemeLabel(mode).ToLowerInvariant()} ativo · offline";
        };

        dialog.ShowDialog();
    }

    private async void ConfigureAccessibility_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar acessibilidade",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var highContrast = new CheckBox
        {
            Content = "Ativar alto contraste",
            IsChecked = _shell.State.HighContrast,
            Margin = new Thickness(0, 4, 0, 10)
        };
        AutomationProperties.SetName(highContrast, "Ativar alto contraste");
        var reducedMotion = new CheckBox
        {
            Content = "Reduzir movimento e transições",
            IsChecked = _shell.State.ReducedMotion,
            Margin = new Thickness(0, 4, 0, 14)
        };
        AutomationProperties.SetName(reducedMotion, "Reduzir movimento e transições");
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar acessibilidade", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Preferências de acessibilidade", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        panel.Children.Add(highContrast);
        panel.Children.Add(reducedMotion);
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            try
            {
                await _shell.ConfigureAccessibilityAsync(highContrast.IsChecked == true, reducedMotion.IsChecked == true);
                ApplyTheme(_themeMode);
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = "Acessibilidade guardada · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private async void ConfigureNavigation_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Configurar navegação",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)Resources["PanelBackground"],
            Foreground = (Brush)Resources["PrimaryText"]
        };
        var initialFocus = new ComboBox { Margin = new Thickness(0, 4, 0, 10) };
        initialFocus.Items.Add(new ComboBoxItem { Content = "Launcher", Tag = NavigationFocusTarget.Launcher });
        initialFocus.Items.Add(new ComboBoxItem { Content = "Ribbon", Tag = NavigationFocusTarget.Ribbon });
        initialFocus.SelectedIndex = _shell.State.Navigation.InitialFocus == NavigationFocusTarget.Ribbon ? 1 : 0;
        AutomationProperties.SetName(initialFocus, "Foco inicial de navegação");
        var wrapFocus = new CheckBox
        {
            Content = "Repetir a navegação ao chegar ao último controlo",
            IsChecked = _shell.State.Navigation.WrapFocus,
            Margin = new Thickness(0, 4, 0, 14)
        };
        AutomationProperties.SetName(wrapFocus, "Repetir a navegação ao chegar ao último controlo");
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 10) };
        var save = new Button { Content = "Guardar navegação", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Preferências de navegação", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        AddField(panel, "Começar foco em", initialFocus);
        panel.Children.Add(wrapFocus);
        panel.Children.Add(feedback);
        panel.Children.Add(save);
        dialog.Content = panel;

        save.Click += async (_, _) =>
        {
            var focus = (initialFocus.SelectedItem as ComboBoxItem)?.Tag is NavigationFocusTarget selected
                ? selected
                : NavigationFocusTarget.Launcher;
            try
            {
                await _shell.ConfigureNavigationAsync(new NavigationSettings(focus, wrapFocus.IsChecked == true));
                ApplyNavigationFocus();
                dialog.DialogResult = true;
                dialog.Close();
                StatusText.Text = "Navegação guardada · offline";
            }
            catch (ArgumentException ex)
            {
                feedback.Text = ex.Message;
            }
        };

        dialog.ShowDialog();
    }

    private static string ThemeLabel(CoreThemeMode mode)
        => mode switch
        {
            CoreThemeMode.Light => "Claro",
            CoreThemeMode.Dark => "Escuro",
            _ => "Sistema"
        };

    private static void AddField(Panel panel, string label, FrameworkElement input)
    {
        panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(input);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        var modifiers = System.Windows.Input.Keyboard.Modifiers;
        if (modifiers == System.Windows.Input.ModifierKeys.None) return;

        var gesture = string.Join('+', new[]
        {
            modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control) ? "Ctrl" : null,
            modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt) ? "Alt" : null,
            modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) ? "Shift" : null,
            key.ToString()
        }.Where(value => value is not null));

        if (!ShortcutCatalog.TryResolveCommand(_shell.State.Shortcuts, gesture, out var commandId)) return;

        e.Handled = true;
        StatusText.Text = commandId switch
        {
            "open-launcher" => "Launcher aberto pelo atalho · offline",
            "open-settings" => "Definições abertas pelo atalho · offline",
            _ => $"Comando '{commandId}' executado pelo atalho · offline"
        };
    }
}
