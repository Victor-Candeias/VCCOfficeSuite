using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Vcc.Office.CoreShell;

public enum WriterDocumentState { Draft, Ready, Invalid }
public sealed record WriterParagraph(string Text, string Style = "Normal");
public sealed record WriterStyle(string Name, string FontFamily = "Aptos", double FontSize = 11, bool Bold = false, bool Italic = false);
public sealed record WriterTable(string Id, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);
public sealed record WriterImage(string Id, string Path, string AltText);
public sealed record WriterPageSettings(double Width = 595, double Height = 842, double Margin = 48);
public sealed record WriterReviewNote(string Id, int ParagraphIndex, string Text, bool Resolved = false);
public sealed record WriterDocument(
    string Id,
    string Title,
    IReadOnlyList<WriterParagraph> Paragraphs,
    IReadOnlyList<WriterStyle> Styles,
    IReadOnlyList<WriterTable> Tables,
    IReadOnlyList<WriterImage> Images,
    WriterPageSettings Page,
    IReadOnlyList<WriterReviewNote> Reviews,
    bool AccessibilityEnabled = true);

public static class WriterPolicy
{
    public static ValidationResult Validate(WriterDocument value)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(value.Id)) errors.Add("O ID do documento é obrigatório.");
        if (string.IsNullOrWhiteSpace(value.Title)) errors.Add("O título do documento é obrigatório.");
        if (value.Paragraphs is null || value.Paragraphs.Count == 0) errors.Add("O documento requer pelo menos um parágrafo.");
        if ((value.Paragraphs ?? []).Any(item => item is null || item.Text is null)) errors.Add("Os parágrafos não podem ser nulos.");
        if (value.Images.Any(item => string.IsNullOrWhiteSpace(item.AltText))) errors.Add("Todas as imagens requerem texto alternativo.");
        if (value.Tables.Any(item => item.Headers is null || item.Headers.Count == 0)) errors.Add("Todas as tabelas requerem cabeçalhos.");
        if (value.Page.Width <= 0 || value.Page.Height <= 0 || value.Page.Margin < 0) errors.Add("As definições de página não são válidas.");
        return new ValidationResult(errors);
    }
}

public sealed class VccWriterService
{
    private readonly AtomicFileStore _files = new();
    private readonly string? _statePath;
    private readonly List<WriterDocument> _documents = [];

    public VccWriterService(string? statePath = null) => _statePath = statePath;
    public IReadOnlyList<WriterDocument> Documents => _documents.ToArray();

    public async Task<WriterDocument> CreateAsync(string id, string title, CancellationToken cancellationToken = default)
    {
        var document = new WriterDocument(id, title, [new WriterParagraph(string.Empty)], [new WriterStyle("Normal")], [], [], new(), [], true);
        return await AddAsync(document, cancellationToken);
    }

    public async Task<WriterDocument> UpdateAsync(string id, Func<WriterDocument, WriterDocument> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var index = FindIndex(id);
        var next = update(_documents[index]);
        var validation = WriterPolicy.Validate(next);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors));
        _documents[index] = next;
        await PersistIfConfiguredAsync(cancellationToken);
        return next;
    }

    public IReadOnlyList<WriterDocument> Query(string? text = null) => string.IsNullOrWhiteSpace(text) ? Documents : Documents.Where(item => item.Title.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) || item.Paragraphs.Any(paragraph => paragraph.Text.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();
    public WriterDocumentState GetState(string id) => string.IsNullOrWhiteSpace(id) ? WriterDocumentState.Invalid : _documents.Any(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ? WriterDocumentState.Ready : WriterDocumentState.Draft;
    public WriterDocument Navigate(string id) => _documents.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("O documento não existe.", nameof(id));

    public Task<WriterDocument> SetRichTextAsync(string id, string text, string style = "Normal", CancellationToken cancellationToken = default) => UpdateAsync(id, document => document with { Paragraphs = text.Split(["\r\n", "\n"], StringSplitOptions.None).Select(line => new WriterParagraph(line, style)).ToArray() }, cancellationToken);
    public Task<WriterDocument> AddStyleAsync(string id, WriterStyle style, CancellationToken cancellationToken = default) => UpdateAsync(id, document => document with { Styles = document.Styles.Append(style).ToArray() }, cancellationToken);
    public Task<WriterDocument> AddTableAsync(string id, WriterTable table, CancellationToken cancellationToken = default) => UpdateAsync(id, document => document with { Tables = document.Tables.Append(table).ToArray() }, cancellationToken);
    public Task<WriterDocument> AddImageAsync(string id, WriterImage image, CancellationToken cancellationToken = default) => UpdateAsync(id, document => document with { Images = document.Images.Append(image).ToArray() }, cancellationToken);
    public Task<WriterDocument> SetPageAsync(string id, WriterPageSettings page, CancellationToken cancellationToken = default) => UpdateAsync(id, document => document with { Page = page }, cancellationToken);
    public Task<WriterDocument> AddReviewAsync(string id, WriterReviewNote review, CancellationToken cancellationToken = default) => UpdateAsync(id, document => document with { Reviews = document.Reviews.Append(review).ToArray() }, cancellationToken);
    public Task<WriterDocument> SetAccessibilityAsync(string id, bool enabled, CancellationToken cancellationToken = default) => UpdateAsync(id, document => document with { AccessibilityEnabled = enabled }, cancellationToken);

    public async Task<WriterDocument> ImportDocxAsync(string id, string title, string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("O ficheiro DOCX não existe.", path);
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("O DOCX não contém word/document.xml.");
        await using var stream = entry.Open();
        var xml = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = xml.Descendants(word + "p").Select(paragraph => new WriterParagraph(string.Concat(paragraph.Descendants(word + "t").Select(text => text.Value)))).ToArray();
        if (paragraphs.Length == 0) paragraphs = [new WriterParagraph(string.Empty)];
        return await AddAsync(new WriterDocument(id, title, paragraphs, [new WriterStyle("Normal")], [], [], new(), [], true), cancellationToken);
    }

    public async Task ExportPdfAsync(string id, string path, CancellationToken cancellationToken = default)
    {
        var document = Navigate(id);
        var lines = document.Paragraphs.Select(item => item.Text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)"));
        var content = $"BT /F1 11 Tf 48 780 Td {string.Join(" Tj 0 -16 Td ", lines.Select(line => $"({line})"))} Tj ET";
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>", $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
        var output = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var (item, index) in objects.Select((item, index) => (item, index + 1))) { offsets.Add(Encoding.ASCII.GetByteCount(output.ToString())); output.Append($"{index} 0 obj\n{item}\nendobj\n"); }
        var xref = Encoding.ASCII.GetByteCount(output.ToString());
        output.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        output.Append(string.Join("", offsets.Skip(1).Select(offset => $"{offset:0000000000} 00000 n \n")));
        output.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        await _files.WriteAsync(path, Encoding.ASCII.GetBytes(output.ToString()), cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado.");
        await _files.WriteAsync(_statePath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_documents, new JsonSerializerOptions { WriteIndented = true })), cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado.");
        if (!File.Exists(_statePath)) return;
        var documents = JsonSerializer.Deserialize<List<WriterDocument>>(await File.ReadAllTextAsync(_statePath, cancellationToken)) ?? throw new InvalidDataException("O estado do Writer é inválido.");
        foreach (var document in documents) { var validation = WriterPolicy.Validate(document); if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors)); }
        _documents.Clear(); _documents.AddRange(documents);
    }

    private async Task<WriterDocument> AddAsync(WriterDocument document, CancellationToken cancellationToken)
    {
        var validation = WriterPolicy.Validate(document);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors));
        if (_documents.Any(item => item.Id.Equals(document.Id, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um documento com esse ID.");
        _documents.Add(document); await PersistIfConfiguredAsync(cancellationToken); return document;
    }
    private int FindIndex(string id) { var index = _documents.FindIndex(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); if (index < 0) throw new ArgumentException("O documento não existe.", nameof(id)); return index; }
    private Task PersistIfConfiguredAsync(CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(_statePath) ? Task.CompletedTask : SaveAsync(cancellationToken);
}
