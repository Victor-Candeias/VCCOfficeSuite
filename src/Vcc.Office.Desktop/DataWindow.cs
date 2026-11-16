using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// Interface Avalonia do VCC Data: utiliza VccDataService existente sobre a base
/// SQLite partilhada. Não depende de WPF, Windows Forms ou Win32.
/// </summary>
public sealed class DataWindow : Window
{
    private readonly VccDataService _data;
    private readonly StackPanel _content = new() { Spacing = 14, Margin = new Thickness(22) };
    private readonly TextBlock _status = new() { Text = "Pronto · dados locais", TextWrapping = TextWrapping.Wrap };
    private ComboBox _tables = new() { MinWidth = 210 };
    private ComboBox _forms = new() { MinWidth = 210 };
    private ComboBox _reports = new() { MinWidth = 210 };
    private string _querySql = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
    private TextBox _query = new();
    private StackPanel _result = new() { Spacing = 5 };
    private StackPanel _formFields = new() { Spacing = 8 };
    private readonly Dictionary<string, TextBox> _formInputs = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<DataTableDefinition> _tableDefinitions = [];
    private IReadOnlyList<DataFormDefinition> _formDefinitions = [];
    private IReadOnlyList<DataReportDefinition> _reportDefinitions = [];
    private DataQueryResult? _lastQuery;

    public DataWindow(VccDataService data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Title = "VCC Data — Avalonia";
        Icon = AppBranding.LoadWindowIcon();
        Width = 1040;
        Height = 750;
        MinWidth = 690;
        MinHeight = 490;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        var header = new StackPanel { Spacing = 9, Margin = new Thickness(22, 16, 22, 8) };
        header.Children.Add(new TextBlock { Text = "VCC Data", FontSize = 29, FontWeight = FontWeight.Bold });
        header.Children.Add(new TextBlock
        {
            Text = "Tabelas · Consultas · CSV · Formulários · Relatórios — dados SQLite offline",
            TextWrapping = TextWrapping.Wrap
        });
        var tabs = new WrapPanel();
        tabs.Children.Add(Action("Tabelas", ShowTables));
        tabs.Children.Add(Action("Consultas", ShowQuery));
        tabs.Children.Add(Action("Formulários", ShowForms));
        tabs.Children.Add(Action("Relatórios", ShowReports));
        header.Children.Add(tabs);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new Border
        {
            BorderBrush = Brush.Parse("#B4C3D5"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 11),
            Child = _status
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = _content });
        Content = root;

        Opened += async (_, _) =>
        {
            await ReloadAsync();
            ShowTables();
        };
    }

    private static Button Action(string label, Action onClick)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 9, 8), Padding = new Thickness(14, 8) };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static TextBlock TitleText(string value) =>
        new() { Text = value, FontSize = 20, FontWeight = FontWeight.SemiBold };

    private void Status(string message) => _status.Text = message;

    private async Task ExecuteAsync(Func<Task> command, string successMessage)
    {
        try
        {
            await command();
            Status(successMessage);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
            IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            Status("Operação não concluída: " + ex.Message);
        }
    }

    private async Task ReloadAsync()
    {
        await ExecuteAsync(async () =>
        {
            _tableDefinitions = await _data.ListTablesAsync();
            _formDefinitions = await _data.ListFormsAsync();
            _reportDefinitions = await _data.ListReportsAsync();
            _tables.ItemsSource = _tableDefinitions.Select(t => t.Name).ToArray();
            _reports.ItemsSource = _reportDefinitions.Select(r => r.DisplayName).ToArray();
            _forms.ItemsSource = _formDefinitions.Select(f => f.DisplayName).ToArray();
        }, "Dados atualizados.");
    }

    private void ShowTables()
    {
        _content.Children.Clear();
        _result = new StackPanel { Spacing = 5 };
        _tables = new ComboBox { MinWidth = 210, ItemsSource = _tableDefinitions.Select(t => t.Name).ToArray() };
        _content.Children.Add(TitleText("Tabelas locais"));
        _content.Children.Add(new TextBlock { Text = "Crie tabelas SQLite e consulte os respetivos registos." });
        var picker = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        picker.Children.Add(_tables);
        picker.Children.Add(Action("Atualizar", () => _ = ReloadAsync()));
        picker.Children.Add(Action("Ver registos", () => _ = PreviewTableAsync()));
        _content.Children.Add(picker);
        foreach (var table in _tableDefinitions)
            _content.Children.Add(new TextBlock
            {
                Text = table.DisplayName + " (" + table.Name + ") — " +
                    string.Join(", ", table.Columns.Select(c => c.Name + ": " + c.Type)),
                TextWrapping = TextWrapping.Wrap
            });

        _content.Children.Add(TitleText("Nova tabela"));
        var name = new TextBox { PlaceholderText = "Nome técnico (ex.: clientes)", MinWidth = 220 };
        var display = new TextBox { PlaceholderText = "Nome de apresentação", MinWidth = 220 };
        var cols = new TextBox
        {
            PlaceholderText = "Uma coluna por linha: nome:TIPO:opcionalidade",
            Text = "nome:TEXT:required" + Environment.NewLine + "idade:INTEGER:nullable",
            AcceptsReturn = true, Height = 100, TextWrapping = TextWrapping.Wrap
        };
        _content.Children.Add(name);
        _content.Children.Add(display);
        _content.Children.Add(cols);
        _content.Children.Add(Action("Criar tabela", () => _ = ExecuteAsync(async () =>
        {
            var definitions = ParseColumns(cols.Text ?? "");
            await _data.CreateTableAsync(new DataTableDefinition(
                (name.Text ?? "").Trim(), (display.Text ?? "").Trim(), definitions));
            await ReloadAsync();
            ShowTables();
        }, "Tabela criada.")));
        _content.Children.Add(_result);
    }

    private static IReadOnlyList<DataColumnDefinition> ParseColumns(string text)
    {
        var result = new List<DataColumnDefinition>();
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || parts.Length > 3)
                throw new ArgumentException("Cada coluna deve usar nome:TIPO:required|nullable.");
            var optionality = parts.Length == 3 ? parts[2] : "nullable";
            if (optionality is not ("nullable" or "required"))
                throw new ArgumentException("Use nullable ou required para a terceira opção.");
            result.Add(new DataColumnDefinition(parts[0], parts[1], optionality == "nullable"));
        }
        return result;
    }

    private async Task PreviewTableAsync()
    {
        if (_tables.SelectedItem is not string selected)
        {
            Status("Selecione uma tabela.");
            return;
        }
        // Os nomes vêm do catálogo validado por VccDataService; não aceitar texto livre.
        await ExecuteAsync(async () =>
        {
            var result = await _data.QueryAsync("SELECT * FROM \"" + selected.Replace("\"", "\"\"", StringComparison.Ordinal) + "\" LIMIT 100");
            RenderResult(result);
        }, "Registos apresentados (até 100).");
    }

    private void ShowQuery()
    {
        _content.Children.Clear();
        _result = new StackPanel { Spacing = 5 };
        _tables = new ComboBox { MinWidth = 210, ItemsSource = _tableDefinitions.Select(t => t.Name).ToArray() };
        _query = new TextBox
        {
            Text = _querySql,
            AcceptsReturn = true,
            Height = 100,
            TextWrapping = TextWrapping.Wrap
        };
        _query.TextChanged += (_, _) => _querySql = _query.Text ?? "";
        _content.Children.Add(TitleText("Consultas SQL de leitura"));
        _content.Children.Add(new TextBlock
        {
            Text = "A execução utiliza o validador de consultas do CoreShell. Não permite comandos de escrita.",
            TextWrapping = TextWrapping.Wrap
        });
        _content.Children.Add(_query);
        var actions = new WrapPanel();
        actions.Children.Add(Action("Executar", () => _ = ExecuteQueryAsync()));
        actions.Children.Add(Action("Exportar CSV", () => _ = ExportCsvAsync()));
        actions.Children.Add(Action("Importar CSV", () => _ = ImportCsvAsync()));
        _content.Children.Add(actions);
        var selector = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        selector.Children.Add(new TextBlock { Text = "Importar para:", VerticalAlignment = VerticalAlignment.Center });
        selector.Children.Add(_tables);
        _content.Children.Add(selector);
        _content.Children.Add(_result);
    }

    private async Task ExecuteQueryAsync()
    {
        await ExecuteAsync(async () =>
        {
            var result = await _data.QueryAsync(_query.Text ?? "");
            RenderResult(result);
        }, "Consulta executada.");
    }

    private void RenderResult(DataQueryResult data)
    {
        _lastQuery = data;
        _result.Children.Clear();
        _result.Children.Add(new TextBlock
        {
            Text = data.Rows.Count + " registos · " + data.Columns.Count + " colunas",
            FontWeight = FontWeight.SemiBold
        });
        if (data.Columns.Count == 0) return;
        _result.Children.Add(new TextBlock { Text = string.Join(" | ", data.Columns), FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        foreach (var row in data.Rows.Take(150))
        {
            _result.Children.Add(new TextBlock
            {
                Text = string.Join(" | ", data.Columns.Select(col =>
                    Convert.ToString(row.TryGetValue(col, out var value) ? value : null, CultureInfo.InvariantCulture) ?? "")),
                TextWrapping = TextWrapping.Wrap
            });
        }
        if (data.Rows.Count > 150)
            _result.Children.Add(new TextBlock { Text = "Pré-visualização limitada a 150 registos; exporte CSV para obter todos." });
    }

    private async Task ImportCsvAsync()
    {
        if (_tables.SelectedItem is not string table)
        {
            Status("Selecione a tabela de destino do CSV.");
            return;
        }
        await ExecuteAsync(async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Importar CSV para " + table,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
            });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var csv = await reader.ReadToEndAsync();
            var count = await _data.ImportCsvAsync(table, csv);
            Status(count + " registos importados.");
        }, "Importação CSV concluída.");
    }

    private async Task ExportCsvAsync()
    {
        await ExecuteAsync(async () =>
        {
            var csv = await _data.ExportCsvAsync(_query.Text ?? "");
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar resultado para CSV",
                SuggestedFileName = "vcc-data.csv",
                DefaultExtension = "csv",
                FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(csv);
        }, "CSV exportado.");
    }

    private void ShowForms()
    {
        _content.Children.Clear();
        _forms = new ComboBox { MinWidth = 210, ItemsSource = _formDefinitions.Select(f => f.DisplayName).ToArray() };
        _forms.SelectionChanged += (_, _) => RenderFormFields();
        _formFields = new StackPanel { Spacing = 8 };
        _content.Children.Add(TitleText("Formulários VCC Data"));
        _content.Children.Add(new TextBlock { Text = "Selecione um formulário existente e introduza os valores." });
        _content.Children.Add(_forms);
        _content.Children.Add(_formFields);
        _content.Children.Add(Action("Guardar registo", () => _ = SubmitFormAsync()));
        _content.Children.Add(Action("Atualizar formulários", () => _ = RefreshFormsAsync()));
        RenderFormFields();
    }

    private async Task RefreshFormsAsync()
    {
        await ReloadAsync();
        ShowForms();
    }

    private void RenderFormFields()
    {
        _formFields.Children.Clear();
        _formInputs.Clear();
        if (_forms.SelectedIndex < 0 || _forms.SelectedIndex >= _formDefinitions.Count) return;
        foreach (var field in _formDefinitions[_forms.SelectedIndex].Fields)
        {
            _formFields.Children.Add(new TextBlock { Text = field.Label + (field.Required ? " *" : "") });
            var input = new TextBox { PlaceholderText = field.Type, MinWidth = 200 };
            _formInputs[field.Name] = input;
            _formFields.Children.Add(input);
        }
    }

    private async Task SubmitFormAsync()
    {
        if (_forms.SelectedIndex < 0 || _forms.SelectedIndex >= _formDefinitions.Count)
        {
            Status("Selecione um formulário.");
            return;
        }
        var form = _formDefinitions[_forms.SelectedIndex];
        await ExecuteAsync(async () =>
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in form.Fields)
            {
                var raw = (_formInputs[field.Name].Text ?? "").Trim();
                values[field.Name] = string.IsNullOrEmpty(raw) && !field.Required ? null : field.Type switch
                {
                    "INTEGER" when long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) => v,
                    "REAL" when double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) => v,
                    "INTEGER" or "REAL" => throw new ArgumentException("Valor numérico inválido: " + field.Label),
                    _ => raw
                };
            }
            await _data.SubmitFormAsync(form, values);
        }, "Formulário guardado.");
    }

    private void ShowReports()
    {
        _content.Children.Clear();
        _reports = new ComboBox { MinWidth = 210, ItemsSource = _reportDefinitions.Select(r => r.DisplayName).ToArray() };
        _result = new StackPanel { Spacing = 5 };
        _content.Children.Add(TitleText("Relatórios VCC Data"));
        _content.Children.Add(_reports);
        _content.Children.Add(Action("Executar relatório", () => _ = ExecuteReportAsync()));
        _content.Children.Add(Action("Atualizar relatórios", () => _ = RefreshReportsAsync()));
        _content.Children.Add(_result);
    }

    private async Task RefreshReportsAsync()
    {
        await ReloadAsync();
        ShowReports();
    }

    private async Task ExecuteReportAsync()
    {
        if (_reports.SelectedIndex < 0 || _reports.SelectedIndex >= _reportDefinitions.Count)
        {
            Status("Selecione um relatório.");
            return;
        }
        await ExecuteAsync(async () =>
        {
            var result = await _data.RunReportAsync(_reportDefinitions[_reports.SelectedIndex]);
            RenderResult(result);
        }, "Relatório executado.");
    }
}
