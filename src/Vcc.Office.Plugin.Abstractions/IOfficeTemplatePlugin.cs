using Avalonia.Controls;

namespace Vcc.Office.Plugin.Abstractions;

/// <summary>Metadata supplied by the plugin; previews describe content, not screenshots.</summary>
public sealed record OfficeDocumentTemplate(
    string Id, string Title, string Category, string Description, string Preview);

/// <summary>Optional capability for creating a fresh, unsaved document from a template.</summary>
public interface IOfficeTemplatePlugin : IOfficePlugin
{
    IReadOnlyCollection<OfficeDocumentTemplate> Templates { get; }
    Task ApplyTemplateAsync(Window window, string templateId);
}
