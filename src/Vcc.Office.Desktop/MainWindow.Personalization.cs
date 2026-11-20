using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    private IReadOnlyList<OfficeModule> OrderedApplications(bool includeHidden = false)
    {
        var modules = LauncherCustomizationPolicy.Sort(InstalledApplications,
            _launcherState.ApplicationOrder, app => app.Id);
        return includeHidden ? modules : modules
            .Where(app => !_launcherState.HiddenApplications.Contains(app.Id,
                StringComparer.OrdinalIgnoreCase)).ToArray();
    }

    private void UpdateAppOrder(string dragged, string target)
    {
        var order = LauncherCustomizationPolicy.Move(_launcherState.ApplicationOrder,
            InstalledApplications.Select(x => x.Id), dragged, target);
        _launcherState = _launcherState with { ApplicationOrder = order };
        _ = SaveLauncherAsync();
        Render();
    }

    private void ToggleAppFavorite(string id)
    {
        _launcherState = _launcherState with
        {
            FavoriteApplications = LauncherCustomizationPolicy.Toggle(
                _launcherState.FavoriteApplications, id)
        };
        _ = SaveLauncherAsync();
        Render();
    }

    private void ToggleAppHidden(string id)
    {
        _launcherState = _launcherState with
        {
            HiddenApplications = LauncherCustomizationPolicy.Toggle(
                _launcherState.HiddenApplications, id)
        };
        _ = SaveLauncherAsync();
        Render();
    }

    private void AttachApplicationReorder(Button button, string id)
    {
        Point? start = null;
        PointerPressedEventArgs? pressed = null;
        var dragging = false;
        button.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            {
                start = e.GetPosition(button);
                pressed = e;
            }
        };
        button.PointerReleased += (_, _) => { start = null; pressed = null; };
        button.PointerMoved += async (_, e) =>
        {
            if (start is null || pressed is null || dragging ||
                !e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) return;
            var point = e.GetPosition(button);
            if (Math.Abs(point.X - start.Value.X) < 9 &&
                Math.Abs(point.Y - start.Value.Y) < 9) return;
            var originalPress = pressed;
            start = null;
            pressed = null;
            dragging = true;
            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(id));
                await DragDrop.DoDragDropAsync(originalPress, data, DragDropEffects.Move);
            }
            finally { dragging = false; }
        };
        DragDrop.SetAllowDrop(button, true);
        button.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.DataTransfer.Contains(DataFormat.Text)
                ? DragDropEffects.Move : DragDropEffects.None;
        });
        button.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.DataTransfer.TryGetText() is string source &&
                !source.Equals(id, StringComparison.OrdinalIgnoreCase))
                UpdateAppOrder(source, id);
        });
    }

    private void AttachApplicationMenu(Button button, OfficeModule app)
    {
        var favorite = new MenuItem
        {
            Header = _launcherState.FavoriteApplications.Contains(app.Id,
                StringComparer.OrdinalIgnoreCase) ? "Retirar dos favoritos" : "Fixar nos favoritos"
        };
        favorite.Click += (_, _) => ToggleAppFavorite(app.Id);
        var hidden = _launcherState.HiddenApplications.Contains(app.Id,
            StringComparer.OrdinalIgnoreCase);
        var visibility = new MenuItem { Header = hidden ? "Mostrar aplicação" : "Ocultar aplicação" };
        visibility.Click += (_, _) => ToggleAppHidden(app.Id);
        button.ContextMenu = new ContextMenu { ItemsSource = new[] { favorite, visibility } };
        AttachApplicationReorder(button, app.Id);
    }
}
