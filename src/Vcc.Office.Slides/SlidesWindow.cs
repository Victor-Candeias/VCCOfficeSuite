using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Slides;

/// <summary>Avalonia slide editor backed by the cross-platform Slides service.</summary>
public sealed class SlidesWindow : Window
{
    private readonly VccSlidesService _slides;
    private readonly ComboBox _decks = new() { MinWidth = 205 };
    private readonly ComboBox _slidePicker = new() { MinWidth = 160 };
    private readonly TextBox _title = new() { MinWidth = 240 };
    private readonly TextBox _notes = new() { AcceptsReturn = true, Height = 110, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _objects = new() { Spacing = 6 };
    private readonly TextBlock _status = new() { Text = "A iniciar..." };
    private string? _currentId;
    private int _slideIndex;
    private bool _loading;

    public SlidesWindow()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VCC Office");
        _slides = new VccSlidesService(Path.Combine(directory, "slides-avalonia.json"));
        Title = "VCC Slides — Avalonia";
        Icon = new SlidesPlugin().CreateWindowIcon();
        Width = 1040; Height = 770; MinWidth = 650; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        var top = new StackPanel { Spacing = 8, Margin = new Thickness(20, 14) };
        top.Children.Add(new TextBlock { Text = "VCC Slides", FontSize = 27, FontWeight = FontWeight.SemiBold });
        top.Children.Add(new TextBlock { Text = "Apresentações, títulos, notas e objetos de texto · motor autónomo Slides" });
        var toolbar = new WrapPanel();
        toolbar.Children.Add(_decks);
        toolbar.Children.Add(Action("Nova apresentação", () => _ = RunAsync(CreateAsync)));
        toolbar.Children.Add(Action("Importar PPTX", () => _ = RunAsync(ImportAsync)));
        toolbar.Children.Add(Action("Exportar PPTX", () => _ = RunAsync(ExportAsync)));
        toolbar.Children.Add(Action("Exportar HTML", () => _ = RunAsync(ExportHtmlAsync)));
        top.Children.Add(toolbar);
        var slidesBar = new WrapPanel();
        slidesBar.Children.Add(_slidePicker);
        slidesBar.Children.Add(Action("Novo diapositivo", () => _ = RunAsync(AddSlideAsync)));
        slidesBar.Children.Add(Action("Anterior", () => _ = RunAsync(() => SelectSlideAsync(_slideIndex - 1))));
        slidesBar.Children.Add(Action("Seguinte", () => _ = RunAsync(() => SelectSlideAsync(_slideIndex + 1))));
        top.Children.Add(slidesBar);
        top.Children.Add(new TextBlock { Text = "Título do diapositivo" });
        top.Children.Add(_title);
        top.Children.Add(new TextBlock { Text = "Notas do apresentador" });
        top.Children.Add(_notes);
        var edit = new WrapPanel();
        edit.Children.Add(Action("Guardar título e notas", () => _ = RunAsync(SaveSlideAsync)));
        edit.Children.Add(Action("Adicionar objeto de texto", () => _ = RunAsync(AddTextAsync)));
        top.Children.Add(edit);
        _decks.SelectionChanged += (_, _) =>
        {
            if (_decks.SelectedItem is SlideDeck deck)
            {
                _currentId = deck.Id;
                _slideIndex = deck.CurrentSlide;
                RenderSlide();
            }
        };
        _slidePicker.SelectionChanged += (_, _) =>
        {
            if (!_loading && _slidePicker.SelectedIndex >= 0)
                _ = RunAsync(() => SelectSlideAsync(_slidePicker.SelectedIndex));
        };
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var footer = new Border { Padding = new Thickness(20, 10), Child = _status };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = _objects, Margin = new Thickness(20) });
        Content = root;
        Opened += async (_, _) => await RunAsync(EnsureInitializedAsync);
    }

    private Task? _initialization;
    private Task EnsureInitializedAsync() => _initialization ??= InitializeAsync();

    private async Task InitializeAsync()
    {
        await _slides.LoadAsync();
        if (_slides.Decks.Count == 0) await CreateAsync();
        else Reload();
    }

    public async Task OpenDocumentAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.GetExtension(path).Equals(".pptx", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("O Slides importa atualmente apenas PPTX.");
        if (!File.Exists(path)) throw new FileNotFoundException("Documento não encontrado.", path);
        await EnsureInitializedAsync();
        var deck = await _slides.ImportPptxAsync(Guid.NewGuid().ToString("N"), Path.GetFileName(path), path);
        _currentId = deck.Id;
        _slideIndex = 0;
        Reload();
        _status.Text = "PPTX importado. Confirme a fidelidade de estilos, animações e objetos.";
    }

    private static Button Action(string caption, Action action)
    {
        var button = new Button { Content = caption, Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(9) };
        button.Click += (_, _) => action();
        return button;
    }

    private async Task RunAsync(Func<Task> command)
    {
        try { await command(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
            IOException or UnauthorizedAccessException or FormatException)
        { _status.Text = "Erro: " + ex.Message; }
    }

    private void Reload()
    {
        var id = _currentId;
        _decks.ItemsSource = _slides.Decks;
        _decks.SelectedItem = _slides.Decks.FirstOrDefault(x => x.Id == id) ?? _slides.Decks.FirstOrDefault();
        _currentId = (_decks.SelectedItem as SlideDeck)?.Id;
        RenderSlide();
    }

    private void RenderSlide()
    {
        _objects.Children.Clear();
        if (_currentId is null) return;
        var deck = _slides.Navigate(_currentId);
        _slideIndex = Math.Clamp(_slideIndex, 0, deck.Slides.Count - 1);
        _loading = true;
        _slidePicker.ItemsSource = deck.Slides.Select((s, i) => $"{i + 1}. {s.Title}").ToArray();
        _slidePicker.SelectedIndex = _slideIndex;
        _loading = false;
        var slide = deck.Slides[_slideIndex];
        _title.Text = slide.Title;
        _notes.Text = slide.Notes;
        _objects.Children.Add(new TextBlock { Text = $"Diapositivo {_slideIndex + 1} de {deck.Slides.Count}",
            FontSize = 20, FontWeight = FontWeight.SemiBold });
        _objects.Children.Add(new TextBlock { Text = slide.Title, FontSize = 25,
            FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (slide.Objects.Count == 0)
            _objects.Children.Add(new TextBlock { Text = "Sem objetos. Pode adicionar um objeto de texto." });
        foreach (var item in slide.Objects)
            _objects.Children.Add(new Border { CornerRadius = new CornerRadius(8),
                BorderBrush = Brush.Parse("#8096B4"), BorderThickness = new Thickness(1),
                Padding = new Thickness(12), Child = new TextBlock
                { Text = item.Kind + ": " + item.Content, TextWrapping = TextWrapping.Wrap } });
    }

    private async Task CreateAsync()
    {
        var deck = await _slides.CreateAsync(Guid.NewGuid().ToString("N"),
            "Apresentação " + (_slides.Decks.Count + 1));
        _currentId = deck.Id;
        _slideIndex = 0;
        Reload();
        _status.Text = "Nova apresentação criada e guardada.";
    }

    private async Task SaveSlideAsync()
    {
        if (_currentId is null) return;
        await _slides.SetSlideTitleAsync(_currentId, _slideIndex, _title.Text ?? "");
        await _slides.SetNotesAsync(_currentId, _slideIndex, _notes.Text ?? "");
        Reload();
        _status.Text = "Título e notas guardados.";
    }

    private async Task AddSlideAsync()
    {
        if (_currentId is null) return;
        var deck = await _slides.AddSlideAsync(_currentId, "Novo diapositivo");
        _slideIndex = deck.CurrentSlide;
        Reload();
        _status.Text = "Novo diapositivo criado.";
    }

    private async Task AddTextAsync()
    {
        if (_currentId is null) return;
        var text = _notes.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) { _status.Text = "Escreva primeiro o texto nas notas."; return; }
        await _slides.AddObjectAsync(_currentId, _slideIndex,
            new SlideObject(Guid.NewGuid().ToString("N"), SlideObjectKind.Text, 30, 80, 480, 100, text));
        Reload();
        _status.Text = "Objeto de texto adicionado a partir das notas.";
    }

    private async Task SelectSlideAsync(int index)
    {
        if (_currentId is null) return;
        var deck = _slides.Navigate(_currentId);
        if (index < 0 || index >= deck.Slides.Count) return;
        await _slides.NavigatePresentationAsync(_currentId, index);
        _slideIndex = index;
        Reload();
    }

    private async Task ImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "Importar apresentação PPTX", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PowerPoint PPTX") { Patterns = ["*.pptx"] }] });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath() ?? throw new InvalidOperationException("Apenas ficheiros locais.");
        await OpenDocumentAsync(path);
    }

    private async Task ExportAsync() => await ExportWithAsync("PPTX", "pptx",
        (id, path) => _slides.ExportPptxAsync(id, path));
    private async Task ExportHtmlAsync() => await ExportWithAsync("HTML", "html",
        (id, path) => _slides.ExportHtmlAsync(id, path));

    private async Task ExportWithAsync(string label, string extension, Func<string, string, Task> export)
    {
        if (_currentId is null) return;
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        { Title = "Exportar " + label, SuggestedFileName = "Apresentação." + extension,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(label) { Patterns = ["*." + extension] }] });
        if (target is null) return;
        var path = target.TryGetLocalPath() ?? throw new InvalidOperationException("Apenas ficheiros locais.");
        await export(_currentId, path);
        _status.Text = label + " exportado: " + path;
    }
}
