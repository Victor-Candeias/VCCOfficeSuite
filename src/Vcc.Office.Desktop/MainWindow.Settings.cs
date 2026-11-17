using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Preferences are edited in a new window with fresh controls on each opening.
    /// Reusing live Avalonia controls across containers would reintroduce visual-parent crashes.
    /// </summary>
    private void ShowLauncherSettings()
    {
        var dialog = new Window
        {
            Title = "VCC Office — Aparência e dashboard",
            Icon = AppBranding.LoadWindowIcon(),
            Width = 475,
            Height = 405,
            MinWidth = 400,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 11 };
        panel.Children.Add(new TextBlock
        {
            Text = "Aparência e dashboard",
            FontSize = 23,
            FontWeight = FontWeight.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Dashboard inicial",
            FontWeight = FontWeight.SemiBold
        });
        var dashboards = _dashboards.GetAll().ToArray();
        var dashboardOptions = dashboards.Select(d => d.Name).ToArray();
        var dashboardChoice = new ComboBox
        {
            ItemsSource = dashboardOptions,
            SelectedIndex = Math.Max(0, Array.FindIndex(dashboards, x => x.Id == _activeDashboard))
        };
        panel.Children.Add(dashboardChoice);

        panel.Children.Add(new TextBlock
        {
            Text = "Tema da aplicação",
            FontWeight = FontWeight.SemiBold
        });
        var themeChoice = new ComboBox
        {
            ItemsSource = new[] { "Sistema", "Claro", "Escuro" },
            SelectedIndex = _launcherState.Theme switch
            {
                ThemeMode.Light => 1,
                ThemeMode.Dark => 2,
                _ => 0
            }
        };
        panel.Children.Add(themeChoice);

        panel.Children.Add(new TextBlock
        {
            Text = "As preferências são guardadas e recuperadas no próximo arranque.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Muted
        });
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        var cancel = new Button { Content = "Cancelar", MinWidth = 95 };
        cancel.Click += (_, _) => dialog.Close();
        var save = new Button { Content = "Guardar", MinWidth = 95 };
        save.Click += async (_, _) =>
        {
            if (dashboardChoice.SelectedIndex < 0 || themeChoice.SelectedIndex < 0)
                return;

            var dashboard = dashboards[dashboardChoice.SelectedIndex].Id;
            var theme = themeChoice.SelectedIndex switch
            {
                1 => ThemeMode.Light,
                2 => ThemeMode.Dark,
                _ => ThemeMode.System
            };
            _launcherState = _launcherState with { DashboardId = dashboard, Theme = theme };
            _activeDashboard = dashboard;
            _favoritesOnly = false;
            _search.Text = string.Empty;
            if (Application.Current is { } app)
                app.RequestedThemeVariant = theme switch
                {
                    ThemeMode.Light => ThemeVariant.Light,
                    ThemeMode.Dark => ThemeVariant.Dark,
                    _ => ThemeVariant.Default
                };
            await SaveLauncherAsync();
            Render();
            dialog.Close();
        };
        var advanced = new Button { Content = "Configuração avançada", MinWidth = 170 };
        advanced.Click += (_, _) =>
        {
            var workspace = new WorkspaceSettingsWindow();
            workspace.Show(this);
            dialog.Close();
        };
        footer.Children.Add(advanced);
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        panel.Children.Add(footer);
        dialog.Content = panel;
        _ = dialog.ShowDialog(this);
    }
}
