using System.Windows;
using System.Windows.Controls;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Shell;

/// <summary>Preferências locais; o resultado só é aplicado ao confirmar.</summary>
public sealed class DashboardSettingsWindow : Window
{
    private readonly ComboBox _preferred;
    private readonly ComboBox _startup;
    private readonly ComboBox _theme;

    public DashboardPreferences? Result { get; private set; }

    public DashboardSettingsWindow(DashboardPreferences current, IDashboardRegistry registry)
    {
        Title = "VCC Office — Dashboard inicial";
        Width = 470;
        Height = 380;
        MinWidth = 410;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Aparência e dashboard", FontSize = 23, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 18)
        });

        _preferred = AddSetting(panel, "Dashboard preferido",
            registry.GetAll().Select(x => (x.Id, x.Name)).ToArray(), current.PreferredDashboardId);
        _startup = AddSetting(panel, "Ao iniciar a suite",
            [("last", "Abrir o último dashboard utilizado"), ("preferred", "Abrir sempre o preferido")],
            current.StartupMode.ToString().ToLowerInvariant());
        _theme = AddSetting(panel, "Tema",
            [("system", "Utilizar tema do sistema"), ("light", "Claro"), ("dark", "Escuro")],
            current.Theme.ToString().ToLowerInvariant());

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cancel = new Button { Content = "Cancelar", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 10, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Guardar", Padding = new Thickness(18, 8, 18, 8), IsDefault = true };
        save.Click += (_, _) =>
        {
            var preferred = (string)_preferred.SelectedValue;
            var startup = (string)_startup.SelectedValue == "preferred" ? DashboardStartupMode.Preferred : DashboardStartupMode.Last;
            var theme = Enum.Parse<ThemeMode>((string)_theme.SelectedValue, ignoreCase: true);
            Result = current with { PreferredDashboardId = preferred, StartupMode = startup, Theme = theme };
            DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private static ComboBox AddSetting(StackPanel parent, string label, (string Id, string Name)[] options, string selected)
    {
        parent.Children.Add(new TextBlock
        {
            Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6)
        });
        var combo = new ComboBox
        {
            ItemsSource = options.Select(x => new KeyValuePair<string, string>(x.Id, x.Name)).ToArray(),
            DisplayMemberPath = "Value", SelectedValuePath = "Key",
            SelectedValue = selected, Height = 32, Margin = new Thickness(0, 0, 0, 14)
        };
        parent.Children.Add(combo);
        return combo;
    }
}
