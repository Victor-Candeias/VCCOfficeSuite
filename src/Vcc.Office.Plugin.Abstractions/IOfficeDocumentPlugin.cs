using Avalonia.Controls;

namespace Vcc.Office.Plugin.Abstractions;

/// <summary>Optional capability: a plugin can open existing local documents.</summary>
public interface IOfficeDocumentPlugin : IOfficePlugin
{
    /// <summary>Extensions the plugin can actually import (including the dot).</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>Loads the requested file into a new editor window; errors must propagate.</summary>
    Task OpenDocumentAsync(Window window, string filePath);
}
