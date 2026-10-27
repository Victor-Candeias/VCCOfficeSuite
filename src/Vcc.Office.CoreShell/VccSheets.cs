using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Vcc.Office.CoreShell;

public readonly record struct SheetCellAddress(int Row, int Column)
{
    public override string ToString()
    {
        var column = Column;
        var name = new StringBuilder();
        while (column > 0) { column--; name.Insert(0, (char)('A' + column % 26)); column /= 26; }
        return $"{name}{Row}";
    }

    public static bool TryParse(string value, out SheetCellAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim().ToUpperInvariant();
        var split = 0;
        while (split < text.Length && char.IsLetter(text[split])) split++;
        if (split == 0 || split == text.Length || !int.TryParse(text[split..], out var row) || row < 1) return false;
        var column = 0;
        foreach (var letter in text[..split]) { if (letter is < 'A' or > 'Z') return false; column = column * 26 + letter - 'A' + 1; }
        address = new(row, column); return true;
    }
}

public sealed record SheetCell(string Value = "", string? Formula = null, string Format = "General");
public sealed record SheetRange(SheetCellAddress Start, SheetCellAddress End)
{
    public IEnumerable<SheetCellAddress> Cells => Enumerable.Range(Start.Row, End.Row - Start.Row + 1).SelectMany(row => Enumerable.Range(Start.Column, End.Column - Start.Column + 1).Select(column => new SheetCellAddress(row, column)));
}
public sealed record SheetSelection(SheetRange Range, SheetCellAddress ActiveCell);
public sealed record SheetChart(string Id, string Title, SheetRange Range);
public sealed record SheetTable(string Id, SheetRange Range, string? FilterText = null);
public sealed record SheetDocument(string Id, string Title, IReadOnlyDictionary<string, SheetCell> Cells, IReadOnlyList<SheetChart> Charts, IReadOnlyList<SheetTable> Tables, IReadOnlyDictionary<string, string> ColumnFormats);

public static class SheetsPolicy
{
    public static ValidationResult Validate(SheetDocument sheet)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(sheet.Id)) errors.Add("O ID da folha é obrigatório.");
        if (string.IsNullOrWhiteSpace(sheet.Title)) errors.Add("O título da folha é obrigatório.");
        foreach (var key in sheet.Cells.Keys) if (!SheetCellAddress.TryParse(key, out _)) errors.Add($"A célula {key} não é válida.");
        foreach (var chart in sheet.Charts) { if (string.IsNullOrWhiteSpace(chart.Id) || string.IsNullOrWhiteSpace(chart.Title)) errors.Add("Os gráficos requerem ID e título."); if (!IsValidRange(chart.Range)) errors.Add($"O intervalo do gráfico {chart.Id} não é válido."); }
        foreach (var table in sheet.Tables) { if (string.IsNullOrWhiteSpace(table.Id)) errors.Add("As tabelas requerem ID."); if (!IsValidRange(table.Range)) errors.Add($"O intervalo da tabela {table.Id} não é válido."); }
        return new ValidationResult(errors);
    }
    private static bool IsValidRange(SheetRange range) => range.Start.Row > 0 && range.Start.Column > 0 && range.End.Row >= range.Start.Row && range.End.Column >= range.Start.Column;
}

public sealed class VccSheetsService
{
    private readonly AtomicFileStore _files = new();
    private readonly string? _statePath;
    private readonly List<SheetDocument> _sheets = [];
    public VccSheetsService(string? statePath = null) => _statePath = statePath;
    public IReadOnlyList<SheetDocument> Sheets => _sheets.ToArray();

    public async Task<SheetDocument> CreateAsync(string id, string title, CancellationToken cancellationToken = default)
    {
        var sheet = new SheetDocument(id, title, new Dictionary<string, SheetCell>(StringComparer.OrdinalIgnoreCase), [], [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        return await AddAsync(sheet, cancellationToken);
    }

    public SheetDocument Navigate(string id) => _sheets.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("A folha não existe.", nameof(id));
    public IReadOnlyList<SheetDocument> Query(string? text = null) => string.IsNullOrWhiteSpace(text) ? Sheets : Sheets.Where(item => item.Title.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
    public string GetValue(string id, string address) { var sheet = Navigate(id); if (!SheetCellAddress.TryParse(address, out var cell)) throw new ArgumentException("A referência da célula não é válida.", nameof(address)); return Evaluate(sheet, cell, []); }
    public SheetSelection Select(string id, SheetRange range, SheetCellAddress? activeCell = null) { var sheet = Navigate(id); ValidateRange(range); var active = activeCell ?? range.Start; if (!range.Cells.Contains(active)) throw new ArgumentException("A célula ativa tem de pertencer ao intervalo selecionado.", nameof(activeCell)); return new SheetSelection(range, active); }
    public IReadOnlyDictionary<string, string> GetValues(string id, SheetRange range) { ValidateRange(range); var sheet = Navigate(id); return range.Cells.ToDictionary(cell => cell.ToString(), cell => Evaluate(sheet, cell, []), StringComparer.OrdinalIgnoreCase); }
    public IReadOnlyDictionary<string, string> Recalculate(string id) { var sheet = Navigate(id); return sheet.Cells.Keys.Where(key => SheetCellAddress.TryParse(key, out _)).ToDictionary(key => key, key => GetValue(id, key), StringComparer.OrdinalIgnoreCase); }

    public Task<SheetDocument> SetValueAsync(string id, string address, string value, CancellationToken cancellationToken = default) => UpdateAsync(id, sheet => SetCell(sheet, address, new SheetCell(value)), cancellationToken);
    public Task<SheetDocument> SetFormulaAsync(string id, string address, string formula, CancellationToken cancellationToken = default) => UpdateAsync(id, sheet => SetCell(sheet, address, new SheetCell("", formula.StartsWith('=') ? formula[1..] : formula)), cancellationToken);
    public Task<SheetDocument> SetFormatAsync(string id, string address, string format, CancellationToken cancellationToken = default) => UpdateAsync(id, sheet => SetCell(sheet, address, GetCell(sheet, address) with { Format = string.IsNullOrWhiteSpace(format) ? "General" : format.Trim() }), cancellationToken);
    public Task<SheetDocument> AddChartAsync(string id, SheetChart chart, CancellationToken cancellationToken = default) => UpdateAsync(id, sheet => sheet with { Charts = sheet.Charts.Append(chart).ToArray() }, cancellationToken);
    public Task<SheetDocument> AddTableAsync(string id, SheetTable table, CancellationToken cancellationToken = default) => UpdateAsync(id, sheet => sheet with { Tables = sheet.Tables.Append(table).ToArray() }, cancellationToken);
    public IReadOnlyList<(SheetCellAddress Address, string Value)> Filter(string id, SheetRange range, string text) => range.Cells.Select(address => (Address: address, Value: GetValue(id, address.ToString()))).Where(item => item.Value.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
    public Task<SheetDocument> CopyRangeAsync(string id, SheetRange source, SheetCellAddress destination, CancellationToken cancellationToken = default) => UpdateAsync(id, sheet => { ValidateRange(source); var cells = new Dictionary<string, SheetCell>(sheet.Cells, StringComparer.OrdinalIgnoreCase); foreach (var cell in source.Cells) { var target = new SheetCellAddress(destination.Row + cell.Row - source.Start.Row, destination.Column + cell.Column - source.Start.Column); if (sheet.Cells.TryGetValue(cell.ToString(), out var value)) cells[target.ToString()] = value; } return sheet with { Cells = cells }; }, cancellationToken);
    public Task<SheetDocument> ClearRangeAsync(string id, SheetRange range, CancellationToken cancellationToken = default) => UpdateAsync(id, sheet => { ValidateRange(range); var cells = new Dictionary<string, SheetCell>(sheet.Cells, StringComparer.OrdinalIgnoreCase); foreach (var cell in range.Cells) cells.Remove(cell.ToString()); return sheet with { Cells = cells }; }, cancellationToken);

    public async Task<SheetDocument> ImportXlsxAsync(string id, string title, string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("O ficheiro XLSX não existe.", path);
        using var archive = ZipFile.OpenRead(path);
        var shared = ReadSharedStrings(archive);
        var entry = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidDataException("O XLSX não contém a primeira folha.");
        await using var stream = entry.Open();
        var xml = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var cells = new Dictionary<string, SheetCell>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in xml.Descendants().Where(item => item.Name.LocalName == "c"))
        {
            var reference = cell.Attribute("r")?.Value; if (!SheetCellAddress.TryParse(reference ?? "", out _)) continue;
            var formula = cell.Elements().FirstOrDefault(item => item.Name.LocalName == "f")?.Value;
            var raw = cell.Elements().FirstOrDefault(item => item.Name.LocalName == "v")?.Value ?? "";
            if (cell.Attribute("t")?.Value == "s" && int.TryParse(raw, out var index) && index >= 0 && index < shared.Count) raw = shared[index];
            cells[reference!] = new SheetCell(raw, formula);
        }
        return await AddAsync(new SheetDocument(id, title, cells, [], [], new Dictionary<string, string>()), cancellationToken);
    }

    public async Task ExportCsvAsync(string id, string path, CancellationToken cancellationToken = default)
    {
        var sheet = Navigate(id); var maxRow = sheet.Cells.Keys.Select(key => SheetCellAddress.TryParse(key, out var cell) ? cell.Row : 0).DefaultIfEmpty(1).Max(); var maxColumn = sheet.Cells.Keys.Select(key => SheetCellAddress.TryParse(key, out var cell) ? cell.Column : 0).DefaultIfEmpty(1).Max();
        var rows = Enumerable.Range(1, maxRow).Select(row => string.Join(",", Enumerable.Range(1, maxColumn).Select(column => CsvEscape(GetValue(id, new SheetCellAddress(row, column).ToString())))));
        await _files.WriteAsync(path, Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, rows) + Environment.NewLine), cancellationToken);
    }
    public async Task ExportPdfAsync(string id, string path, CancellationToken cancellationToken = default)
    {
        var sheet = Navigate(id); var rows = sheet.Cells.Keys.Select(key => SheetCellAddress.TryParse(key, out var cell) ? cell.Row : 0).DefaultIfEmpty(1).Max();
        var lines = Enumerable.Range(1, rows).Select(row => string.Join(" | ", Enumerable.Range(1, sheet.Cells.Keys.Select(key => SheetCellAddress.TryParse(key, out var cell) ? cell.Column : 0).DefaultIfEmpty(1).Max()).Select(column => GetValue(id, new SheetCellAddress(row, column).ToString()))));
        var content = $"BT /F1 10 Tf 36 800 Td {string.Join(" Tj 0 -14 Td ", lines.Select(line => $"({line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")})"))} Tj ET";
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>", $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
        var output = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
        foreach (var (item, index) in objects.Select((item, index) => (item, index + 1))) { offsets.Add(Encoding.ASCII.GetByteCount(output.ToString())); output.Append($"{index} 0 obj\n{item}\nendobj\n"); }
        var xref = Encoding.ASCII.GetByteCount(output.ToString()); output.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n"); output.Append(string.Join("", offsets.Skip(1).Select(offset => $"{offset:0000000000} 00000 n \n"))); output.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        await _files.WriteAsync(path, Encoding.ASCII.GetBytes(output.ToString()), cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado.");
        await _files.WriteAsync(_statePath, JsonSerializer.SerializeToUtf8Bytes(_sheets, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado.");
        if (!File.Exists(_statePath)) return;
        var sheets = JsonSerializer.Deserialize<List<SheetDocument>>(await File.ReadAllTextAsync(_statePath, cancellationToken)) ?? throw new InvalidDataException("O estado do Sheets é inválido.");
        foreach (var sheet in sheets) { var validation = SheetsPolicy.Validate(sheet); if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors)); }
        _sheets.Clear(); _sheets.AddRange(sheets);
    }

    private async Task<SheetDocument> AddAsync(SheetDocument sheet, CancellationToken cancellationToken) { var validation = SheetsPolicy.Validate(sheet); if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors)); if (_sheets.Any(item => item.Id.Equals(sheet.Id, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe uma folha com esse ID."); _sheets.Add(sheet); await PersistIfConfiguredAsync(cancellationToken); return sheet; }
    private async Task<SheetDocument> UpdateAsync(string id, Func<SheetDocument, SheetDocument> update, CancellationToken cancellationToken) { var index = _sheets.FindIndex(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); if (index < 0) throw new ArgumentException("A folha não existe.", nameof(id)); var next = update(_sheets[index]); var validation = SheetsPolicy.Validate(next); if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors)); _sheets[index] = next; await PersistIfConfiguredAsync(cancellationToken); return next; }
    private static SheetDocument SetCell(SheetDocument sheet, string address, SheetCell cell) { if (!SheetCellAddress.TryParse(address, out var parsed)) throw new ArgumentException("A referência da célula não é válida.", nameof(address)); var cells = new Dictionary<string, SheetCell>(sheet.Cells, StringComparer.OrdinalIgnoreCase) { [parsed.ToString()] = cell }; return sheet with { Cells = cells }; }
    private static void ValidateRange(SheetRange range) { if (range.Start.Row < 1 || range.Start.Column < 1 || range.End.Row < range.Start.Row || range.End.Column < range.Start.Column) throw new ArgumentException("O intervalo da folha não é válido.", nameof(range)); }
    private static SheetCell GetCell(SheetDocument sheet, string address) => sheet.Cells.TryGetValue(address, out var cell) ? cell : new();
    private string Evaluate(SheetDocument sheet, SheetCellAddress address, HashSet<string> stack) { var key = address.ToString(); if (!stack.Add(key)) throw new InvalidDataException("Foi detetada uma referência circular."); if (!sheet.Cells.TryGetValue(key, out var cell)) { stack.Remove(key); return ""; } if (string.IsNullOrWhiteSpace(cell.Formula)) { stack.Remove(key); return cell.Value; } var result = EvaluateFormula(sheet, cell.Formula!, stack); stack.Remove(key); return result; }
    private string EvaluateFormula(SheetDocument sheet, string formula, HashSet<string> stack)
    {
        var expression = formula.Trim(); if (expression.StartsWith("SUM(", StringComparison.OrdinalIgnoreCase) && expression.EndsWith(')')) { var range = expression[4..^1].Split(':'); if (range.Length != 2 || !SheetCellAddress.TryParse(range[0], out var start) || !SheetCellAddress.TryParse(range[1], out var end)) throw new InvalidDataException("A função SUM requer um intervalo válido."); var total = new SheetRange(start, end).Cells.Sum(cell => double.TryParse(Evaluate(sheet, cell, stack), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : 0); return total.ToString(CultureInfo.InvariantCulture); }
        if (SheetCellAddress.TryParse(expression, out var reference)) return Evaluate(sheet, reference, stack);
        var tokens = expression.Replace("+", " + ").Replace("-", " - ").Replace("*", " * ").Replace("/", " / ").Split(' ', StringSplitOptions.RemoveEmptyEntries); if (tokens.Length == 1 && double.TryParse(tokens[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var literal)) return literal.ToString(CultureInfo.InvariantCulture); if (tokens.Length == 3 && double.TryParse(ResolveToken(sheet, tokens[0], stack), NumberStyles.Any, CultureInfo.InvariantCulture, out var left) && double.TryParse(ResolveToken(sheet, tokens[2], stack), NumberStyles.Any, CultureInfo.InvariantCulture, out var right)) { var result = tokens[1] switch { "+" => left + right, "-" => left - right, "*" => left * right, "/" when right != 0 => left / right, _ => throw new InvalidDataException("Operador de fórmula inválido.") }; return result.ToString(CultureInfo.InvariantCulture); } throw new InvalidDataException("A fórmula não é suportada.");
    }
    private string ResolveToken(SheetDocument sheet, string token, HashSet<string> stack) => SheetCellAddress.TryParse(token, out var address) ? Evaluate(sheet, address, stack) : token;
    private static List<string> ReadSharedStrings(ZipArchive archive) { var entry = archive.GetEntry("xl/sharedStrings.xml"); if (entry is null) return []; using var stream = entry.Open(); var xml = XDocument.Load(stream); return xml.Descendants().Where(item => item.Name.LocalName == "si").Select(item => string.Concat(item.Descendants().Where(child => child.Name.LocalName == "t").Select(child => child.Value))).ToList(); }
    private static string CsvEscape(string value) => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal) ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    private Task PersistIfConfiguredAsync(CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(_statePath) ? Task.CompletedTask : SaveAsync(cancellationToken);
}
