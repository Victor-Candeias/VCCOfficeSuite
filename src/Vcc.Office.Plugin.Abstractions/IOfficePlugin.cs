using Avalonia.Controls;

namespace Vcc.Office.Plugin.Abstractions;

/// <summary>Contract shared by trusted Office modules and the Avalonia launcher.</summary>
public interface IOfficePlugin
{
    string Id { get; }
    string DisplayName { get; }

    /// <summary>Create a separate application window. The host owns its lifetime.</summary>
    Window CreateWindow(WindowIcon? icon = null);
}
