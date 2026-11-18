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
        });
        dialog.Show(this);
    }
}
