using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>Primeira interface Writer Avalonia: texto, TXT, importação e exportação DOCX.</summary>
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
    private string _documentId = Guid.NewGuid().ToString("N");
    private string _documentTitle = "Sem título";
    private string? _currentPath;
    private bool _dirty;

    public WriterWindow()
    {
        Title = "VCC Writer — Sem título";
        Icon = AppBranding.LoadWindowIcon();
        Width = 990;
        Height = 750;
        MinWidth = 610;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel();
        var toolbar = new WrapPanel { Margin = new Thickness(15, 12) };
        toolbar.Children.Add(Action("Novo", () => _ = NewAsync()));
        toolbar.Children.Add(Action("Abrir TXT/DOCX", () => _ = OpenAsync()));
        toolbar.Children.Add(Action("Guardar", () => _ = SaveAsync()));
        toolbar.Children.Add(Action("Guardar como", () => _ = SaveAsAsync()));
        toolbar.Children.Add(Action("Exportar DOCX", () => _ = ExportDocxAsync()));
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        var footer = new Border { Padding = new Thickness(16, 10), Child = _status };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(new Border { Padding = new Thickness(20, 8, 20, 14), Child = _editor });
        Content = root;
        _editor.TextChanged += (_, _) =>
        {
            _dirty = true;
            UpdateTitle();
        };
    }

    private static Button Action(string title, Action callback)
    {
        var button = new Button
        {
            Content = title,
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 8)
        };
        button.Click += (_, _) => callback();
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
