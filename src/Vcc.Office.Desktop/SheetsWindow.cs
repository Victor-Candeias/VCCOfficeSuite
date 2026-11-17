using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// Spreadsheet interface for the shared Sheets engine. Uses a selectable cell
/// editor rather than pretending to provide a complete Excel-compatible grid.
/// </summary>
public sealed class SheetsWindow : Window
{
    private readonly VccSheetsService _sheets;
    private readonly ComboBox _documents = new() { MinWidth = 200 };
    private readonly TextBox _address = new() { Width = 75, Text = "A1" };
    private readonly TextBox _value = new() { MinWidth = 240 };
    private readonly StackPanel _preview = new() { Spacing = 4 };
    private readonly TextBlock _status = new() { Text = "A iniciar..." };
    private string? _currentId;

    public SheetsWindow()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VCC Office");
        _sheets = new VccSheetsService(Path.Combine(directory, "sheets-avalonia.json"));
        Title = "VCC Sheets — Avalonia";
        Icon = AppBranding.LoadWindowIcon();
        Width = 1050; Height = 750; MinWidth = 650; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel();
        var top = new StackPanel { Margin = new Thickness(18), Spacing = 8 };
        top.Children.Add(new TextBlock { Text = "VCC Sheets", FontSize = 27, FontWeight = FontWeight.SemiBold });
        top.Children.Add(new TextBlock { Text = "Editor de células e fórmulas · motor CoreShell partilhado" });
        var tools = new WrapPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(_documents);
        tools.Children.Add(Action("Nova folha", () => _ = RunAsync(CreateAsync)));
        tools.Children.Add(Action("Importar XLSX", () => _ = RunAsync(ImportAsync)));
        tools.Children.Add(Action("Exportar CSV", () => _ = RunAsync(ExportAsync)));
        tools.Children.Add(Action("Exportar PDF", () => _ = RunAsync(ExportPdfAsync)));
        top.Children.Add(tools);
        var edit = new WrapPanel { Orientation = Orientation.Horizontal };
        edit.Children.Add(new TextBlock { Text = "Célula", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        edit.Children.Add(_address);
        edit.Children.Add(_value);
        edit.Children.Add(Action("Aplicar valor/fórmula", () => _ = RunAsync(ApplyAsync)));
        edit.Children.Add(Action("Ler célula", ReadCell));
        top.Children.Add(edit);
        _documents.SelectionChanged += (_, _) =>
        {
            if (_documents.SelectedItem is SheetDocument item)
            {
                _currentId = item.Id;
                RenderPreview();
            }
        };
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var bottom = new Border { Padding = new Thickness(18, 10), Child = _status };
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = _preview, Margin = new Thickness(18) });
        Content = root;
        Opened += async (_, _) => await RunAsync(async () =>
        {
            await _sheets.LoadAsync();
            if (_sheets.Sheets.Count == 0) await CreateAsync();
            else Reload();
        });
    }

    private static Button Action(string caption, Action click)
    {
        var b = new Button { Content = caption, Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(9) };
        b.Click += (_, _) => click();
        return b;
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
            IOException or UnauthorizedAccessException or FormatException)
        { _status.Text = "Erro: " + ex.Message; }
    }

    private void Reload()
    {
        var current = _currentId;
        _documents.ItemsSource = _sheets.Sheets;
        _documents.SelectedItem = _sheets.Sheets.FirstOrDefault(d => d.Id == current) ?? _sheets.Sheets.FirstOrDefault();
        _currentId = (_documents.SelectedItem as SheetDocument)?.Id;
        RenderPreview();
    }

    private async Task CreateAsync()
    {
        var title = "Folha " + (_sheets.Sheets.Count + 1);
        var item = await _sheets.CreateAsync(Guid.NewGuid().ToString("N"), title);
        _currentId = item.Id;
        Reload();
        _status.Text = "Nova folha criada e guardada localmente.";
    }

    private async Task ApplyAsync()
    {
        if (_currentId is null) return;
        var address = (_address.Text ?? "").Trim().ToUpperInvariant();
        if (!SheetCellAddress.TryParse(address, out _))
            throw new ArgumentException("Endereço de célula inválido.");
        var value = _value.Text ?? "";
        if (value.StartsWith('='))
            await _sheets.SetFormulaAsync(_currentId, address, value);
        else
            await _sheets.SetValueAsync(_currentId, address, value);
        Reload();
        _status.Text = "Célula " + address + " atualizada.";
    }

    private void ReadCell()
    {
        if (_currentId is null) return;
        var address = (_address.Text ?? "").Trim().ToUpperInvariant();
        if (!SheetCellAddress.TryParse(address, out _)) { _status.Text = "Endereço inválido."; return; }
        var document = _sheets.Navigate(_currentId);
        _value.Text = document.Cells.TryGetValue(address, out var cell)
            ? cell.Formula is null ? cell.Value : "=" + cell.Formula : "";
        _status.Text = address + " = " + _sheets.GetValue(_currentId, address);
    }

    private void RenderPreview()
    {
        _preview.Children.Clear();
        if (_currentId is null) return;
        var sheet = _sheets.Navigate(_currentId);
        _preview.Children.Add(new TextBlock { Text = sheet.Title + " · pré-visualização A1:H12",
            FontSize = 20, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        for (var row = 0; row <= 12; row++)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            for (var col = 0; col <= 8; col++)
            {
                var address = row == 0 || col == 0 ? "" : new SheetCellAddress(row, col).ToString();
                var text = row == 0 ? (col == 0 ? "" : new SheetCellAddress(1, col).ToString().TrimEnd('1'))
                    : col == 0 ? row.ToString() :
                    sheet.Cells.ContainsKey(address) ? _sheets.GetValue(_currentId, address) : "";
                var button = new Button { Content = text, Width = col == 0 ? 45 : 107, Height = 31,
                    HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(6, 3) };
                if (row > 0 && col > 0)
                {
                    var selected = address;
                    button.Click += (_, _) => { _address.Text = selected; ReadCell(); };
                }
                else button.IsEnabled = false;
                line.Children.Add(button);
            }
            _preview.Children.Add(line);
        }
    }

    private async Task ImportAsync()
    {
        var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Importar XLSX", AllowMultiple = false, FileTypeFilter =
            [new FilePickerFileType("Excel XLSX") { Patterns = ["*.xlsx"] }] });
        if (picked.Count == 0) return;
        var path = picked[0].TryGetLocalPath() ?? throw new InvalidOperationException("Apenas ficheiros locais.");
        var sheet = await _sheets.ImportXlsxAsync(Guid.NewGuid().ToString("N"), Path.GetFileName(path), path);
        _currentId = sheet.Id; Reload();
        _status.Text = "XLSX importado (primeira folha; verifique fórmulas e formatos).";
    }

    private async Task ExportAsync() => await ExportWithAsync("CSV", "csv",
        (id, path) => _sheets.ExportCsvAsync(id, path));
    private async Task ExportPdfAsync() => await ExportWithAsync("PDF", "pdf",
        (id, path) => _sheets.ExportPdfAsync(id, path));

    private async Task ExportWithAsync(string name, string extension, Func<string, string, Task> export)
    {
        if (_currentId is null) return;
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        { Title = "Exportar " + name, SuggestedFileName = "Folha." + extension,
            DefaultExtension = extension, FileTypeChoices =
                [new FilePickerFileType(name) { Patterns = ["*." + extension] }] });
        if (target is null) return;
        var path = target.TryGetLocalPath() ?? throw new InvalidOperationException("Apenas ficheiros locais.");
        await export(_currentId, path);
        _status.Text = name + " exportado: " + path;
    }
}
