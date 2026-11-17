using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

/// <summary>
/// The WPF Workspace's persisted WindowLayout is expressed in logical
/// coordinates. Convert explicitly between Avalonia pixel placement and DIPs.
/// </summary>
public sealed partial class MainWindow
{
    private bool _windowLayoutReady;
    private bool _windowLayoutRestored;

    private void RestoreWorkspaceWindow()
    {
        if (_windowLayoutRestored) return;
        _windowLayoutRestored = true;
        var saved = _workspaceShell.State.Window;
        var screen = FindWorkspaceScreen(saved.Left, saved.Top);
        if (screen is null)
        {
            _windowLayoutReady = true;
            return;
        }
        var scale = Math.Max(0.25, screen.Scaling);
        var area = screen.WorkingArea;
        var areaLeft = area.X / scale;
        var areaTop = area.Y / scale;
        var areaWidth = area.Width / scale;
        var areaHeight = area.Height / scale;
        if (saved == WindowLayout.Default)
        {
            _windowLayoutReady = true;
            return;
        }

        var safe = WindowLayoutPolicy.Normalize(saved, areaLeft, areaTop, areaWidth, areaHeight);
        Width = Math.Clamp(safe.Width, MinWidth, Math.Max(MinWidth, areaWidth));
        Height = Math.Clamp(safe.Height, MinHeight, Math.Max(MinHeight, areaHeight));
        Position = new PixelPoint(
            (int)Math.Round(Math.Clamp(safe.Left, areaLeft, areaLeft + Math.Max(0, areaWidth - Width)) * scale),
            (int)Math.Round(Math.Clamp(safe.Top, areaTop, areaTop + Math.Max(0, areaHeight - Height)) * scale));
        if (safe.IsMaximized) WindowState = WindowState.Maximized;
        _windowLayoutReady = true;
    }

    private Screen? FindWorkspaceScreen(double logicalLeft, double logicalTop)
    {
        var all = Screens.All;
        foreach (var screen in all)
        {
            var scale = Math.Max(0.25, screen.Scaling);
            var area = screen.WorkingArea;
            var x = logicalLeft * scale;
            var y = logicalTop * scale;
            if (x >= area.X && x < area.X + area.Width &&
                y >= area.Y && y < area.Y + area.Height)
                return screen;
        }
        return Screens.Primary ?? all.FirstOrDefault();
    }

    private void SaveWorkspaceWindow()
    {
        if (!_windowLayoutReady || WindowState == WindowState.Minimized) return;
        try
        {
            var screen = FindWorkspaceScreen(
                Position.X / Math.Max(0.25, Screens.Primary?.Scaling ?? 1),
                Position.Y / Math.Max(0.25, Screens.Primary?.Scaling ?? 1));
            var scale = Math.Max(0.25, screen?.Scaling ?? 1);
            var area = screen?.WorkingArea;
            var left = (WindowState == WindowState.Maximized ? _workspaceShell.State.Window.Left : Position.X / scale);
            var top = (WindowState == WindowState.Maximized ? _workspaceShell.State.Window.Top : Position.Y / scale);
            var width = WindowState == WindowState.Maximized ? _workspaceShell.State.Window.Width : Bounds.Width;
            var height = WindowState == WindowState.Maximized ? _workspaceShell.State.Window.Height : Bounds.Height;
            var next = WindowLayoutPolicy.Normalize(
                new WindowLayout(width, height, left, top, WindowState == WindowState.Maximized),
                (area?.X ?? 0) / scale, (area?.Y ?? 0) / scale,
                (area?.Width ?? 1920) / scale, (area?.Height ?? 1080) / scale);

            // Closing cannot wait for async UI continuations; persist on a
            // worker and finish before the process is terminated.
            Task.Run(async () =>
                await _workspaceShell.UpdateAsync(state => state with { Window = next }))
                .GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException)
        {
            // Window shutdown must not be prevented by a read-only profile.
        }
    }
}
