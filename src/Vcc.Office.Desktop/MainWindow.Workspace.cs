using Avalonia.Controls;
using Avalonia.Input;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    private readonly CoreShellService _workspaceShell = new(new ShellStateStore(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCC Office", "shell-state.json")));

    /// <summary>Reload the same shortcut configuration that WPF used.</summary>
    private void EnableWorkspaceShortcuts()
    {
        Activated += async (_, _) =>
        {
            try { await _workspaceShell.InitialiseAsync(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        };
        KeyDown += WorkspaceKeyDown;
    }

    private void WorkspaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None) return;
        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        parts.Add(e.Key == Key.OemComma ? "," : e.Key.ToString());

        if (!ShortcutCatalog.TryResolveCommand(_workspaceShell.State.Shortcuts,
                string.Join('+', parts), out var command)) return;
        if (command == "open-settings") ShowLauncherSettings();
        else if (command == "open-launcher") Switch(DashboardIds.Grid);
        else return; // No silent claim that arbitrary configured commands were executed.
        e.Handled = true;
    }
}
