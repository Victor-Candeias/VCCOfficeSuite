using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>Applies the existing WPF navigation preferences to Avalonia controls.</summary>
public static class WorkspaceNavigationAdapter
{
    public static void Apply(Control container, NavigationSettings settings, bool reducedMotion)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(settings);
        var validation = NavigationPolicy.Validate(settings);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(" ", validation.Errors), nameof(settings));

        KeyboardNavigation.SetTabNavigation(container, settings.WrapFocus
            ? KeyboardNavigationMode.Cycle : KeyboardNavigationMode.Continue);

        if (!reducedMotion) return;
        container.Transitions = null;
        foreach (var control in container.GetVisualDescendants().OfType<Control>())
            control.Transitions = null;
    }

    public static Button InitialFocusTarget(NavigationSettings settings,
        Button launcher, Button ribbon)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(ribbon);
        return settings.InitialFocus == NavigationFocusTarget.Ribbon ? ribbon : launcher;
    }
}
