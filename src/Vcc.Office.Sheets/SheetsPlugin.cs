using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.Sheets;

/// <summary>Fully independent Sheets application and plugin metadata.</summary>
public sealed class SheetsPlugin : IOfficeDocumentPlugin, IOfficeTemplatePlugin
{
    public string Id => "sheets";
    public string DisplayName => "VCC Sheets";
    public string ToolTip => "Criar, editar e exportar folhas de cálculo, fórmulas e tabelas.";
    public bool SupportsNewDocument => true;
    public IReadOnlyCollection<OfficeDocumentTemplate> Templates { get; } =
    [
        new("budget", "Orçamento mensal", "Finanças", "Tabela de receitas, despesas e saldo.", "Categoria | Previsto | Real\nReceitas\nDespesas"),
        new("tasks", "Lista de tarefas", "Produtividade", "Registo de tarefas, responsáveis e estados.", "Tarefa | Responsável | Estado\nPreparar ..."),
        new("expenses", "Mapa de despesas", "Finanças", "Registo de despesas com cálculo total.", "Data | Descrição | Valor\nTOTAL")
    ];

    public Task ApplyTemplateAsync(Window window, string templateId) =>
        window is SheetsWindow editor ? editor.ApplyTemplateAsync(templateId)
            : throw new ArgumentException("A janela não pertence ao plugin SheetsPlugin.", nameof(window));

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".xlsx"];

    public Task OpenDocumentAsync(Window window, string filePath) =>
        window is SheetsWindow editor ? editor.OpenDocumentAsync(filePath)
            : throw new ArgumentException("A janela não pertence ao plugin SheetsPlugin.", nameof(window));

    public Control CreateMenuIcon(double size)
    {
        if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
            throw new ArgumentOutOfRangeException(nameof(size));
        return new Border
        {
            Name = "SheetsPluginIcon", Width = size, Height = size,
            Background = Brush.Parse("#188451"), CornerRadius = new CornerRadius(size * 0.2),
            Child = new TextBlock
            {
                Text = "▦", Foreground = Brushes.White,
                FontSize = size * 0.65, FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
    public WindowIcon CreateWindowIcon()
    {
        const string icon = "AAABAAEAICAAAAAAIADsAAAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAAgAAAAIAgGAAAAc3p69AAAALNJREFUeJxjFKrw+s8wgIAFXeBe0yqaWqhUF4bbAfeaVmEooDZAt4OJnpYzMEBCADmUmehpOTZHMBFQS3MwdBzwtn0rw9v2rWTLU+wAWoFRB4w6YMAdgFEZwQCuLEUoq+GTF670xhAbvCGA7lqYz7D5ghh5XGDAQ2DUAQPugNFsOJoNB9wBOKMAHRAKWlKDHgYGPASYGBgwOwu0Bsj9EHgI0MsR6J0gRvTeMb07pxgOoDcAAIGrUGnXDbFZAAAAAElFTkSuQmCC";
        using var stream = new MemoryStream(Convert.FromBase64String(icon));
        return new WindowIcon(stream);
    }
    public Window CreateWindow(WindowIcon? icon = null) => new SheetsWindow { Icon = icon ?? CreateWindowIcon() };
}
