using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>Editor Writer com Ribbon e folha A4 visual; mantém os formatos de texto existentes.</summary>
public sealed class WriterWindow : Window
{
    private readonly VccWriterService _writer = new();
    private readonly TextBox _editor = new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        FontSize = 16,
        MinHeight = 350,
        VerticalAlignment = VerticalAlignment.Stretch,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly TextBlock _status = new() { Text = "Novo documento · ainda não guardado" };
    private readonly TextBlock _wordCount = new() { Text = "0 palavras" };
    private readonly Border _topRuler = new() { Name = "WriterTopRuler" };
    private readonly Border _bottomRuler = new() { Name = "WriterBottomRuler" };
    private readonly Border _verticalRuler = new() { Name = "WriterVerticalRuler" };
    private bool _showRulers = true;
    private const double PageWidth = 1024; // 794; // A4 at 96 DPI
    private const double PageHeight = 1123;
    private const double PageMargin = 38; //76; // approximately 2 cm

    private string _documentId = Guid.NewGuid().ToString("N");
    private string _documentTitle = "Sem título";
    private string? _currentPath;
    private bool _dirty;

    public WriterWindow()
    {
        Title = "VCC Writer — Sem título";
        Icon = AppBranding.LoadWindowIcon();
        Width = 1200;
        Height = 860;
        MinWidth = 700;
        MinHeight = 530;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#E9EDF2");

        var root = new DockPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var ribbon = BuildRibbon();
        DockPanel.SetDock(ribbon, Dock.Top);
        root.Children.Add(ribbon);

        var footerContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        footerContent.Children.Add(_status);
        Grid.SetColumn(_wordCount, 1);
        footerContent.Children.Add(_wordCount);
        var pageLabel = new TextBlock
        {
            Text = "A4  ·  21 × 29,7 cm",
            Foreground = Brush("#4C596D"),
            Margin = new Thickness(22, 0, 0, 0)
        };
        Grid.SetColumn(pageLabel, 2);
        footerContent.Children.Add(pageLabel);
        var footer = new Border
        {
            Name = "WriterStatusBar",
            Background = Brushes.White,
            BorderBrush = Brush("#CDD5DF"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 9),
            Child = footerContent
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        _editor.Name = "WriterEditor";
        _editor.MinHeight = PageHeight - 2 * PageMargin - 2;
        _editor.FontFamily = new FontFamily("Arial");
        _editor.FontSize = 16;
        _editor.Background = Brushes.White;
        _editor.Foreground = Brushes.Black;
        _editor.BorderThickness = new Thickness(0);
        _editor.Padding = new Thickness(0);

        var page = new Border
        {
            Name = "WriterPage",
            Width = PageWidth,
            Height = PageHeight,
            Padding = new Thickness(PageMargin / 2),
            Background = Brushes.White,
            BorderBrush = Brush("#C9D0DB"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = _editor
        };

        BuildHorizontalRuler(_topRuler);
        BuildHorizontalRuler(_bottomRuler);
        BuildVerticalRuler();

        var topRow = new StackPanel 
        { 
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        topRow.Children.Add(new Border { Width = 32 });
        topRow.Children.Add(_topRuler);

        var pageRow = new StackPanel { Orientation = Orientation.Horizontal };
        pageRow.Children.Add(_verticalRuler);
        pageRow.Children.Add(page);

        var bottomRow = new StackPanel { Orientation = Orientation.Horizontal };
        bottomRow.Children.Add(new Border { Width = 32 });
        bottomRow.Children.Add(_bottomRuler);

        var workspace = new StackPanel
        {
            Name = "WriterPageWorkspace",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(20, 18, 20, 36)
        };

        workspace.Children.Add(topRow);
        workspace.Children.Add(pageRow);
        workspace.Children.Add(bottomRow);
        
        root.Children.Add(new ScrollViewer
        {
            Name = "WriterScrollViewer",
            Content = workspace,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        });

        Content = root;

        // Listen to the Text property directly: this fires synchronously for both
        // typed and programmatically loaded text, including in headless tests.
        _editor.PropertyChanged += (_, change) =>
        {
            if (change.Property != TextBox.TextProperty)
                return;
            _dirty = true;
            _wordCount.Text = CountWords(_editor.Text).ToString() + " palavras";
            UpdateTitle();
        };
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    private Control BuildRibbon()
    {
        var file = new WrapPanel 
        { 
            Margin = new Thickness(14, 11),
        };

        file.Children.Add(Action("Novo", () => _ = NewAsync()));
        file.Children.Add(Action("Abrir TXT / DOCX", () => _ = OpenAsync()));
        file.Children.Add(Action("Guardar", () => _ = SaveAsync()));
        file.Children.Add(Action("Guardar como", () => _ = SaveAsAsync()));
        file.Children.Add(Action("Exportar DOCX", () => _ = ExportDocxAsync()));

        var home = new WrapPanel 
        { 
            Margin = new Thickness(14, 11) 
        };

        home.Children.Add(Action("Selecionar tudo", () => _editor.SelectAll()));
        home.Children.Add(Action("A−", () => ChangeTextSize(-1)));
        home.Children.Add(Action("A+", () => ChangeTextSize(1)));

        home.Children.Add(new TextBlock
        {
            Text = "Editor de texto simples  ·  formatação rica numa fase posterior",
            Foreground = Brush("#59687D"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 8)
        });

        var view = new WrapPanel { Margin = new Thickness(14, 11) };
        view.Children.Add(Action("Mostrar / ocultar réguas", ToggleRulers));
        view.Children.Add(new TextBlock
        {
            Text = "Página A4  ·  Margens de 2 cm",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 8)
        });

        return new Border
        {
            Name = "WriterRibbon",
            Background = Brushes.White,
            BorderBrush = Brush("#CDD5DF"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TabControl
            {
                Name = "WriterRibbonTabs",
                SelectedIndex = 1,
                Items =
                {
                    RibbonTab("Ficheiro", file),
                    RibbonTab("Base", home),
                    RibbonTab("Ver", view)
                }
            }
        };
    }

    private TabItem RibbonTab(string title, Control content) => new()
    {
        Header = title,
        Content = content,
        Foreground = Brushes.White,
    };

    private void ChangeTextSize(int delta)
    {
        // Visual preference only: the current TXT/DOCX engine does not persist run formatting.
        _editor.FontSize = Math.Clamp(_editor.FontSize + delta, 10, 28);
        Status("Tamanho de apresentação: " + _editor.FontSize + " px (não gravado no DOCX).");
    }

    private void ToggleRulers()
    {
        _showRulers = !_showRulers;
        _topRuler.IsVisible = _showRulers;
        _bottomRuler.IsVisible = _showRulers;
        _verticalRuler.IsVisible = _showRulers;
    }

    private static int CountWords(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 :
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static void BuildHorizontalRuler(Border ruler)
    {
        var ticks = new StackPanel { Orientation = Orientation.Horizontal };
        for (var centimetre = 0; centimetre < 23; centimetre++)
        {
            ticks.Children.Add(new TextBlock
            {
                Text = centimetre.ToString(),
                Width = PageWidth / 23,
                FontSize = 10,
                Foreground = Brush("#566273"),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            });
        }
        ruler.Width = PageWidth;
        ruler.Height = 26;
        ruler.Background = Brush("#F8FAFC");
        ruler.BorderBrush = Brush("#CFD7E1");
        ruler.BorderThickness = new Thickness(0, 0, 0, 1);
        ruler.Child = ticks;
    }

    private void BuildVerticalRuler()
    {
        var ticks = new StackPanel();
        for (var centimetre = 0; centimetre < 30; centimetre++)
        {
            ticks.Children.Add(new TextBlock
            {
                Text = centimetre.ToString(),
                Height = PageHeight / 30,
                FontSize = 10,
                Foreground = Brush("#566273"),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        _verticalRuler.Width = 28;
        _verticalRuler.Height = PageHeight;
        _verticalRuler.Background = Brush("#F8FAFC");
        _verticalRuler.BorderBrush = Brush("#CFD7E1");
        _verticalRuler.BorderThickness = new Thickness(0, 0, 1, 0);
        _verticalRuler.Child = ticks;
    }

    private static Button Action(string title, Action callback)
    {
        var button = new Button
        {
            Content = title,
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 8),
            Foreground = Brushes.Blue,
            Background = Brushes.White,
        };
        button.Click += (_, _) => callback();

        // Equivalente ao MouseEnter / MouseOver
        button.PointerEntered += (sender, e) =>
        {
            // Ação quando o rato passa por cima (ex: mudar cor ou cursor)
            button.Foreground = Brushes.White;
            button.Background = Brushes.Blue; // Exemplo: mudar a cor de fundo
        };

        // Equivalente ao MouseLeave
        button.PointerExited += (sender, e) =>
        {
            // Repor o estado normal quando o rato sai
            button.Foreground = Brushes.Blue;
            button.Background = Brushes.White; // Exemplo: mudar a cor de fundo
        };

        return button;
    }

    private void UpdateTitle() => Title = "VCC Writer — " + _documentTitle + (_dirty ? " *" : "");

    private void Status(string value) => _status.Text = value;

    private async Task ExecuteAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or InvalidOperationException)
        {
            Status("Erro: " + ex.Message);
        }
    }

    private async Task SyncDocumentAsync()
    {
        if (_writer.GetState(_documentId) != WriterDocumentState.Ready)
            await _writer.CreateAsync(_documentId, _documentTitle);
        await _writer.SetRichTextAsync(_documentId, _editor.Text ?? "");
    }

    private async Task NewAsync() => await ExecuteAsync(async () =>
    {
        if (!await ConfirmDiscardAsync()) return;
        _documentId = Guid.NewGuid().ToString("N");
        _documentTitle = "Sem título";
        _currentPath = null;
        _editor.Text = "";
        _dirty = false;
        UpdateTitle();
        Status("Novo documento criado.");
    });

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!_dirty) return true;
        var confirm = new Window
        {
            Title = "Alterações por guardar",
            Icon = AppBranding.LoadWindowIcon(),
            Width = 420,
            Height = 165,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var layout = new StackPanel { Margin = new Thickness(18), Spacing = 12 };
        layout.Children.Add(new TextBlock { Text = "Descartar alterações por guardar?" });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var discard = new Button { Content = "Descartar" };
        var cancel = new Button { Content = "Cancelar" };
        discard.Click += (_, _) => confirm.Close(true);
        cancel.Click += (_, _) => confirm.Close(false);
        actions.Children.Add(discard);
        actions.Children.Add(cancel);
        layout.Children.Add(actions);
        confirm.Content = layout;
        return await confirm.ShowDialog<bool>(this);
    }

    private async Task OpenAsync() => await ExecuteAsync(async () =>
    {
        if (!await ConfirmDiscardAsync()) return;
        var items = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Abrir documento Writer",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Documentos Writer") { Patterns = ["*.txt", "*.docx"] }]
        });
        if (items.Count == 0) return;
        var path = items[0].TryGetLocalPath();
        if (path is null) throw new InvalidOperationException("Apenas são suportados ficheiros locais.");
        var id = Guid.NewGuid().ToString("N");
        var title = System.IO.Path.GetFileName(path);
        string text;
        if (System.IO.Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            var doc = await _writer.ImportDocxAsync(id, title, path);
            text = string.Join(Environment.NewLine, doc.Paragraphs.Select(p => p.Text));
            // Importação DOCX é apenas de texto: guardar no mesmo ficheiro seria destrutivo.
            _currentPath = null;
            Status("DOCX importado como texto. Use Exportar DOCX para criar uma cópia; estilos, tabelas e imagens não são preservados.");
        }
        else
        {
            text = await File.ReadAllTextAsync(path);
            _currentPath = path;
            Status("TXT aberto: " + path);
        }
        _documentId = id;
        _documentTitle = title;
        _editor.Text = text;
        _dirty = false;
        UpdateTitle();
    });

    private async Task SaveAsync() => await ExecuteAsync(async () =>
    {
        if (_currentPath is null) { await SaveAsAsync(); return; }
        await File.WriteAllTextAsync(_currentPath, _editor.Text ?? "");
        _dirty = false;
        UpdateTitle();
        Status("Documento guardado: " + _currentPath);
    });

    private async Task SaveAsAsync() => await ExecuteAsync(async () =>
    {
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar texto Writer",
            SuggestedFileName = "Documento.txt",
            DefaultExtension = "txt",
            FileTypeChoices = [new FilePickerFileType("Texto simples") { Patterns = ["*.txt"] }]
        });
        if (target is null) return;
        var path = target.TryGetLocalPath();
        if (path is null) throw new InvalidOperationException("Apenas são suportados ficheiros locais.");
        await File.WriteAllTextAsync(path, _editor.Text ?? "");
        _currentPath = path;
        _documentTitle = System.IO.Path.GetFileName(path);
        _dirty = false;
        UpdateTitle();
        Status("TXT guardado: " + path);
    });

    private async Task ExportDocxAsync() => await ExecuteAsync(async () =>
    {
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar documento DOCX",
            SuggestedFileName = System.IO.Path.GetFileNameWithoutExtension(_documentTitle) + ".docx",
            DefaultExtension = "docx",
            FileTypeChoices = [new FilePickerFileType("Documento Word") { Patterns = ["*.docx"] }]
        });
        if (target is null) return;
        var path = target.TryGetLocalPath();
        if (path is null) throw new InvalidOperationException("Apenas são suportados ficheiros locais.");
        await SyncDocumentAsync();
        await _writer.ExportDocxAsync(_documentId, path);
        Status("DOCX exportado: " + path + " (texto simples; sem formatação avançada).");
    });
}
