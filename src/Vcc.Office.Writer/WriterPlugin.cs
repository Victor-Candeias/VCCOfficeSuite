using Avalonia.Controls;
using Vcc.Office.Plugin.Abstractions;

namespace Vcc.Office.Writer;

/// <summary>Self-describing Writer module: all visual metadata comes from this DLL.</summary>
public sealed class WriterPlugin : IOfficeDocumentPlugin, IOfficeTemplatePlugin
{
    public string Id => "writer";
    public string DisplayName => "VCC Writer";
    public string ToolTip => "Criar, editar, abrir e guardar documentos de texto e DOCX.";
    public bool SupportsNewDocument => true;
    public IReadOnlyCollection<OfficeDocumentTemplate> Templates { get; } =
    [
        new("letter", "Carta formal", "Correspondência", "Carta com destinatário, assunto e assinatura.", "[Destinatário]\nAssunto: ...\nExmo.(a) Senhor(a), ..."),
        new("meeting", "Ata de reunião", "Reuniões", "Registo de participantes, decisões e ações.", "Ata de reunião\nPresentes: ...\nDecisões: ..."),
        new("report", "Relatório simples", "Relatórios", "Relatório estruturado com resumo e conclusões.", "Relatório\nResumo executivo\nConclusões")
    ];

    public Task ApplyTemplateAsync(Window window, string templateId) =>
        window is WriterWindow editor ? editor.ApplyTemplateAsync(templateId)
            : throw new ArgumentException("A janela não pertence ao plugin WriterPlugin.", nameof(window));

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".txt", ".docx"];

    public Task OpenDocumentAsync(Window window, string filePath) =>
        window is WriterWindow editor ? editor.OpenDocumentAsync(filePath)
            : throw new ArgumentException("A janela não pertence ao plugin WriterPlugin.", nameof(window));


    public Control CreateMenuIcon(double size) => WriterBranding.CreateMenuIcon(size);
    public WindowIcon CreateWindowIcon() => WriterBranding.CreateWindowIcon();
    public Window CreateWindow(WindowIcon? icon = null) =>
        new WriterWindow(icon ?? CreateWindowIcon());
}
