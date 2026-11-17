using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    private RibbonWindow? _ribbonWindow;
    private Button? _launcherFocusButton;
    private Button? _ribbonFocusButton;
    private bool _initialNavigationFocusApplied;
    private bool _lastReducedMotion;
    private string _configuredLauncherRevision = "";

    private readonly CoreShellService _workspaceShell = new(new ShellStateStore(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCC Office", "shell-state.json")));

    /// <summary>Reload the same shortcut configuration that WPF used.</summary>
    private void EnableWorkspaceShortcuts()
    {
        Activated += async (_, _) =>
        {
            try
            {
                var previousContrast = _workspaceShell.State.HighContrast;
                await _workspaceShell.InitialiseAsync();
                var motionChanged = _lastReducedMotion != _workspaceShell.State.ReducedMotion;
                _lastReducedMotion = _workspaceShell.State.ReducedMotion;
                var newRevision = string.Join("|", _workspaceShell.State.Launcher.Select(item =>
                    item.Id + ":" + item.Title + ":" + item.Description + ":" + item.Glyph));
                if (previousContrast != _workspaceShell.State.HighContrast || motionChanged ||
                    !string.Equals(_configuredLauncherRevision, newRevision, StringComparison.Ordinal))
                {
                    _configuredLauncherRevision = newRevision;
                    Render();
                }
                ApplyWorkspaceNavigation();
                if (!_initialNavigationFocusApplied)
                {
                    _initialNavigationFocusApplied = true;
                    Dispatcher.UIThread.Post(FocusConfiguredTarget, DispatcherPriority.Input);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        };
        KeyDown += WorkspaceKeyDown;
    }

    /// <summary>Apply the same Cycle/Continue policy that the WPF Workspace used.</summary>
    private void ApplyWorkspaceNavigation()
    {
        WorkspaceNavigationAdapter.Apply(_layout, _workspaceShell.State.Navigation,
            _workspaceShell.State.ReducedMotion);
    }

    private void FocusConfiguredTarget()
    {
        if (_launcherFocusButton is null || _ribbonFocusButton is null ||
            !WorkspaceNavigationAdapter.InitialFocusTarget(_workspaceShell.State.Navigation,
                _launcherFocusButton, _ribbonFocusButton).Focus())
            _search.Focus();
    }

    private static void SetAccessibleName(Button button, string name) =>
        AutomationProperties.SetName(button, name);

    private IReadOnlyList<LauncherItem> ConfiguredItems(string? query = null) =>
        ConfiguredLauncherPolicy.CustomItems(_workspaceShell.State.Launcher, _registry, query);

    private async Task OpenConfiguredAsync(LauncherItem item)
    {
        try
        {
            await _workspaceShell.NavigateLauncherAsync(item.Id);
        }
        catch (ArgumentException ex)
        {
            ShowNotice("Módulo não encontrado", ex.Message);
            return;
        }
        catch (IOException ex)
        {
            ShowNotice("Erro ao guardar navegação", ex.Message);
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowNotice("Erro ao guardar navegação", ex.Message);
            return;
        }

        // A custom definition is metadata. No executable path or arbitrary
        // command is accepted from shell-state.json for security reasons.
        ShowNotice(item.Title, "Módulo personalizado registado no Launcher. " +
            "Ainda não existe uma aplicação associada a este ID. " +
            "Para permitir a abertura é necessária uma integração específica.");
    }

    private Button ConfiguredItemButton(LauncherItem item)
    {
        var button = Nav(item.Glyph + "   " + item.Title,
            () => _ = OpenConfiguredAsync(item));
        ToolTip.SetTip(button, item.Description + " · Registo personalizado, sem aplicação associada");
        return button;
    }

    private void OpenRibbon()
    {
        if (_ribbonWindow is not null)
        {
            if (_ribbonWindow.WindowState == WindowState.Minimized)
                _ribbonWindow.WindowState = WindowState.Normal;
            _ribbonWindow.Activate();
            return;
        }
        var ribbon = new RibbonWindow(_workspaceShell);
        ribbon.Closed += (_, _) => _ribbonWindow = null;
        _ribbonWindow = ribbon;
        ribbon.Show(this);
    }

    private void WorkspaceKeyDown(object? sender, KeyEventArgs e)
    {
        // WPF global-search shortcut: it must work even without a configured
        // CoreShell command and must not open an external window.
        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control)
        {
            _search.Focus();
            e.Handled = true;
            return;
        }
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
        else if (command == "open-ribbon") OpenRibbon();
        else return; // No silent claim that arbitrary configured commands were executed.
        e.Handled = true;
    }
}
