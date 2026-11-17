using Avalonia.Controls;
using ProEdit.Word.Avalonia;

namespace Vcc.Office.Desktop;

/// <summary>
/// Prova de conceito do Writer baseada no motor ProEdit.
/// O motor fornece Ribbon, edição rica, paginação, réguas e operações DOCX.
/// Mantemos uma janela VCC independente, sem arrancar a aplicação ProEdit.Word.App.
/// </summary>
public sealed class WriterWindow : Window
{
    private readonly WordEditorControl _editor;

    public WriterWindow()
    {
        Title = "VCC Writer — Documento";
        Icon = AppBranding.LoadWindowIcon();
        Width = 1200;
        Height = 850;
        MinWidth = 760;
        MinHeight = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _editor = new WordEditorControl
        {
            Name = "VccWriterProEditEditor"
        };
        _editor.OwnerWindow = this;
        _editor.WindowFactory = document =>
        {
            // A janela ProEdit controla a criação do documento secundário.
            // Uma nova janela VCC preserva a identidade e os ícones da suite.
            var child = new WriterWindow();
            child._editor.SetInitialDocument(document, null);
            return child;
        };
        _editor.SetInitialDocument(null, null);
        Content = _editor;
    }
}
