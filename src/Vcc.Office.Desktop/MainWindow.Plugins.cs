using Avalonia.Controls;
using Vcc.Office.PluginHost;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    private void ShowPluginManager()
    {
        var dialog = new PluginManagerWindow(_plugins, (id, enabled) =>
        {
            if (!enabled && _pluginWindows.TryGetValue(id, out var current))
                current.Close();
            if (!enabled && string.Equals(_selectedSidebarModuleId, id, StringComparison.OrdinalIgnoreCase))
                _selectedSidebarModuleId = null;
            Render();
            _ = RecordNotificationAsync(OfficeNotificationCategory.Plugin,
                OfficeNotificationSeverity.Info, enabled ? "Plugin ativado" : "Plugin desativado",
                id, id);
        }, (title, message, success) =>
        {
            _ = RecordNotificationAsync(OfficeNotificationCategory.Plugin,
                success ? OfficeNotificationSeverity.Success : OfficeNotificationSeverity.Error,
                title, message, "Gestor de plugins");
        });
        dialog.Show(this);
    }
}
