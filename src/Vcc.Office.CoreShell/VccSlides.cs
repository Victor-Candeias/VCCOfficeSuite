using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Vcc.Office.CoreShell;

public enum SlideObjectKind { Text, Image, Shape }
public sealed record SlideObject(string Id, SlideObjectKind Kind, double X, double Y, double Width, double Height, string Content, string AltText = "");
public sealed record SlideTheme(string Name = "VCC Light", string Background = "#FFFFFF", string Foreground = "#1F2937");
public sealed record SlideLayout(string Name = "Title and Content", string? TitleObjectId = null);
public sealed record SlideTransition(string Type = "fade", int DurationMilliseconds = 500);
public sealed record VccSlide(string Id, string Title, IReadOnlyList<SlideObject> Objects, string Notes = "", SlideLayout? Layout = null);
public sealed record SlideDeck(string Id, string Title, IReadOnlyList<VccSlide> Slides, SlideTheme Theme, int CurrentSlide = 0, IReadOnlyDictionary<string, SlideTransition>? Transitions = null);

public static class SlidesPolicy
{
    public static ValidationResult Validate(SlideDeck deck)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(deck.Id)) errors.Add("O ID da apresentação é obrigatório.");
        if (string.IsNullOrWhiteSpace(deck.Title)) errors.Add("O título da apresentação é obrigatório.");
        if (deck.Slides.Count == 0) errors.Add("A apresentação requer pelo menos um diapositivo.");
        if (deck.CurrentSlide < 0 || deck.CurrentSlide >= Math.Max(1, deck.Slides.Count)) errors.Add("O diapositivo atual não é válido.");
        foreach (var transition in deck.Transitions ?? new Dictionary<string, SlideTransition>()) if (transition.Value.DurationMilliseconds < 0) errors.Add($"A duração da transição {transition.Key} não é válida.");
        foreach (var slide in deck.Slides) { if (string.IsNullOrWhiteSpace(slide.Id)) errors.Add("Cada diapositivo requer ID."); if (slide.Objects.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) errors.Add($"Existem objetos duplicados no diapositivo {slide.Id}."); foreach (var item in slide.Objects) if (item.Width <= 0 || item.Height <= 0) errors.Add($"O objeto {item.Id} tem dimensões inválidas."); }
        return new ValidationResult(errors);
    }
}

public sealed class VccSlidesService
{
    private readonly AtomicFileStore _files = new(); private readonly string? _statePath; private readonly List<SlideDeck> _decks = [];
    public VccSlidesService(string? statePath = null) => _statePath = statePath;
    public IReadOnlyList<SlideDeck> Decks => _decks.ToArray();
    public async Task<SlideDeck> CreateAsync(string id, string title, CancellationToken cancellationToken = default) => await AddAsync(new SlideDeck(id, title, [new VccSlide("slide-1", "Título", [], "", new SlideLayout())], new()), cancellationToken);
    public SlideDeck Navigate(string id) => _decks.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("A apresentação não existe.", nameof(id));
    public IReadOnlyList<SlideDeck> Query(string? text = null) => string.IsNullOrWhiteSpace(text) ? Decks : Decks.Where(item => item.Title.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) || item.Slides.Any(slide => slide.Title.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();
    public Task<SlideDeck> AddObjectAsync(string id, int slideIndex, SlideObject item, CancellationToken cancellationToken = default) => UpdateAsync(id, deck => { var slide = GetSlide(deck, slideIndex); var slides = deck.Slides.ToArray(); slides[slideIndex] = slide with { Objects = slide.Objects.Append(item).ToArray() }; return deck with { Slides = slides }; }, cancellationToken);
    public Task<SlideDeck> SetNotesAsync(string id, int slideIndex, string notes, CancellationToken cancellationToken = default) => UpdateAsync(id, deck => { var slides = deck.Slides.ToArray(); slides[slideIndex] = GetSlide(deck, slideIndex) with { Notes = notes ?? "" }; return deck with { Slides = slides }; }, cancellationToken);
    public Task<SlideDeck> SetThemeAsync(string id, SlideTheme theme, CancellationToken cancellationToken = default) => UpdateAsync(id, deck => deck with { Theme = theme }, cancellationToken);
    public Task<SlideDeck> SetTransitionAsync(string id, int slideIndex, SlideTransition transition, CancellationToken cancellationToken = default) => UpdateAsync(id, deck => { var slide = GetSlide(deck, slideIndex); var transitions = new Dictionary<string, SlideTransition>(deck.Transitions ?? new Dictionary<string, SlideTransition>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase) { [slide.Id] = transition }; return deck with { Transitions = transitions }; }, cancellationToken);
    public Task<SlideDeck> NavigatePresentationAsync(string id, int slideIndex, CancellationToken cancellationToken = default) => UpdateAsync(id, deck => { _ = GetSlide(deck, slideIndex); return deck with { CurrentSlide = slideIndex }; }, cancellationToken);
    public SlideDeck Present(string id) => Navigate(id) with { CurrentSlide = 0 };

    public async Task<SlideDeck> ImportPptxAsync(string id, string title, string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("O ficheiro PPTX não existe.", path); using var archive = ZipFile.OpenRead(path); var slides = new List<VccSlide>();
        foreach (var entry in archive.Entries.Where(item => item.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && item.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.FullName)) { await using var stream = entry.Open(); var xml = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken); var text = string.Join(" ", xml.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value)); slides.Add(new VccSlide($"slide-{slides.Count + 1}", text.Length > 80 ? text[..80] : text, [new SlideObject($"text-{slides.Count + 1}", SlideObjectKind.Text, 24, 24, 680, 80, text)])); }
        if (slides.Count == 0) throw new InvalidDataException("O PPTX não contém diapositivos legíveis."); return await AddAsync(new SlideDeck(id, title, slides, new()), cancellationToken);
    }

    public async Task ExportHtmlAsync(string id, string path, CancellationToken cancellationToken = default)
    {
        var deck = Navigate(id); var html = new StringBuilder($"<!doctype html><html lang=\"pt\"><head><meta charset=\"utf-8\"><title>{Escape(deck.Title)}</title><style>body{{font-family:Arial;background:{deck.Theme.Background};color:{deck.Theme.Foreground}}}.slide{{min-height:420px;padding:32px;margin:24px;border:1px solid #ccc}}.object{{position:relative;margin:8px}}</style></head><body>"); foreach (var slide in deck.Slides) { html.Append($"<section class=\"slide\"><h2>{Escape(slide.Title)}</h2>"); foreach (var item in slide.Objects) html.Append($"<div class=\"object\" style=\"width:{item.Width}px;height:{item.Height}px\">{(item.Kind == SlideObjectKind.Image ? $"<img alt=\"{Escape(item.AltText)}\" src=\"{Escape(item.Content)}\" style=\"max-width:100%;max-height:100%\">" : Escape(item.Content))}</div>"); html.Append($"<aside>{Escape(slide.Notes)}</aside></section>"); } html.Append("</body></html>"); await _files.WriteAsync(path, Encoding.UTF8.GetBytes(html.ToString()), cancellationToken);
    }
    public async Task ExportPptxAsync(string id, string path, CancellationToken cancellationToken = default)
    {
        var deck = Navigate(id); await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None); using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>");
        WriteEntry(archive, "ppt/presentation.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><presentation xmlns=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><title>{Escape(deck.Title)}</title><slides>{deck.Slides.Count}</slides></presentation>");
        for (var index = 0; index < deck.Slides.Count; index++) { var slide = deck.Slides[index]; WriteEntry(archive, $"ppt/slides/slide{index + 1}.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><slide xmlns=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><title>{Escape(slide.Title)}</title><text>{Escape(string.Join(" ", slide.Objects.Select(item => item.Content)))}</text></slide>"); }
        await stream.FlushAsync(cancellationToken);
    }
    public async Task ExportPdfAsync(string id, string path, CancellationToken cancellationToken = default) { var deck = Navigate(id); var content = $"BT /F1 12 Tf 36 800 Td ({deck.Title.Replace("(", "\\(").Replace(")", "\\)")}) Tj ET"; var bytes = Encoding.ASCII.GetBytes($"%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n% {content}\n%%EOF"); await _files.WriteAsync(path, bytes, cancellationToken); }

    public async Task SaveAsync(CancellationToken cancellationToken = default) { if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado."); await _files.WriteAsync(_statePath, JsonSerializer.SerializeToUtf8Bytes(_decks, new JsonSerializerOptions { WriteIndented = true }), cancellationToken); }
    public async Task LoadAsync(CancellationToken cancellationToken = default) { if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado."); if (!File.Exists(_statePath)) return; var decks = JsonSerializer.Deserialize<List<SlideDeck>>(await File.ReadAllTextAsync(_statePath, cancellationToken)) ?? throw new InvalidDataException("O estado de Slides é inválido."); foreach (var deck in decks) { var validation = SlidesPolicy.Validate(deck); if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors)); } _decks.Clear(); _decks.AddRange(decks); }
    private async Task<SlideDeck> AddAsync(SlideDeck deck, CancellationToken cancellationToken) { var validation = SlidesPolicy.Validate(deck); if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors)); if (_decks.Any(item => item.Id.Equals(deck.Id, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe uma apresentação com esse ID."); _decks.Add(deck); await PersistIfConfiguredAsync(cancellationToken); return deck; }
    private async Task<SlideDeck> UpdateAsync(string id, Func<SlideDeck, SlideDeck> update, CancellationToken cancellationToken) { var index = _decks.FindIndex(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); if (index < 0) throw new ArgumentException("A apresentação não existe.", nameof(id)); var next = update(_decks[index]); var validation = SlidesPolicy.Validate(next); if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors)); _decks[index] = next; await PersistIfConfiguredAsync(cancellationToken); return next; }
    private static VccSlide GetSlide(SlideDeck deck, int index) => index >= 0 && index < deck.Slides.Count ? deck.Slides[index] : throw new ArgumentOutOfRangeException(nameof(index));
    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value ?? "");
    private static void WriteEntry(ZipArchive archive, string name, string content) { using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8); writer.Write(content); }
    private Task PersistIfConfiguredAsync(CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(_statePath) ? Task.CompletedTask : SaveAsync(cancellationToken);
}
