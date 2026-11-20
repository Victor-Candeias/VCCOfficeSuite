using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    private readonly LauncherProfileStore _profiles = new(
        new CoreDatabase(StoragePaths.ForLocalApplication()), new DashboardRegistry());

    private void ActivateProfile(LauncherProfile profile)
    {
        _launcherState = LauncherProfilePolicy.Apply(profile, _launcherState, _dashboards);
        _activeDashboard = _launcherState.DashboardId;
        _favoritesOnly = false;
        if (Application.Current is { } app)
            app.RequestedThemeVariant = _launcherState.Theme switch
            {
                ThemeMode.Light => ThemeVariant.Light,
                ThemeMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        _ = SaveLauncherAsync();
        Render();
    }

    private void ShowLauncherProfiles()
    {
        var window = new Window
        {
            Title = "VCC Office — Perfis do Launcher",
            Icon = AppBranding.LoadWindowIcon(),
            Width = 530, Height = 450, MinWidth = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(22), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Perfis de personalização",
            FontSize = 23, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Guarde layouts diferentes para trabalho, projetos ou documentos. " +
            "A exportação contém apenas preferências visuais e organização de aplicações.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var name = new TextBox { Watermark = "Nome do perfil (até 48 caracteres)" };
        panel.Children.Add(name);
        var profiles = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(profiles);
        var status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        panel.Children.Add(status);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var save = new Button { Content = "Guardar perfil" };
        var apply = new Button { Content = "Aplicar" };
        var remove = new Button { Content = "Eliminar" };
        var export = new Button { Content = "Exportar JSON" };
        var import = new Button { Content = "Importar JSON" };
        foreach (var action in new[] { save, apply, remove, export, import })
        {
            action.Margin = new Thickness(0, 0, 8, 8);
            actions.Children.Add(action);
        }
        panel.Children.Add(actions);
        IReadOnlyList<LauncherProfile> current = [];
        async Task RefreshAsync(string? select = null)
        {
            current = await _profiles.LoadAsync();
            profiles.ItemsSource = current.Select(p => p.Name).ToArray();
            profiles.SelectedIndex = current.Count == 0 ? -1 :
                Math.Max(0, Array.FindIndex(current.ToArray(), p => p.Name == select));
            status.Text = current.Count == 0 ? "Ainda não existem perfis guardados." :
                current.Count + " perfil(is) disponível(is).";
        }
        LauncherProfile? Selected() =>
            profiles.SelectedIndex >= 0 && profiles.SelectedIndex < current.Count
                ? current[profiles.SelectedIndex] : null;
        async Task HandleAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                InvalidDataException or ArgumentException or InvalidOperationException or
                Microsoft.Data.Sqlite.SqliteException or System.Text.Json.JsonException)
            {
                status.Text = "Não foi possível concluir a operação: " + ex.Message;
            }
        }
        save.Click += async (_, _) => await HandleAsync(async () =>
        {
            var profile = LauncherProfilePolicy.Capture(name.Text?.Trim() ?? "", _launcherState);
            await _profiles.SaveAsync(profile);
            await RefreshAsync(profile.Name);
            status.Text = "Perfil guardado em SQLite: " + profile.Name;
        });
        apply.Click += async (_, _) => await HandleAsync(async () =>
        {
            if (Selected() is not { } profile) return;
            ActivateProfile(profile);
            status.Text = "Perfil aplicado: " + profile.Name;
            await Task.CompletedTask;
        });
        remove.Click += async (_, _) => await HandleAsync(async () =>
        {
            if (Selected() is not { } profile) return;
            await _profiles.RemoveAsync(profile.Name);
            if (_launcherState.ActiveProfileName == profile.Name)
            {
                _launcherState = _launcherState with { ActiveProfileName = null };
                await SaveLauncherAsync();
            }
            await RefreshAsync();
        });
        export.Click += async (_, _) => await HandleAsync(async () =>
        {
            if (Selected() is not { } profile) return;
            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar perfil VCC Office",
                SuggestedFileName = "vcc-launcher-profile.json",
                DefaultExtension = "json"
            });
            if (file is null) return;
            await using var output = await file.OpenWriteAsync();
            output.SetLength(0);
            await output.WriteAsync(Encoding.UTF8.GetBytes(_profiles.Export(profile)));
            status.Text = "Perfil exportado.";
        });
        import.Click += async (_, _) => await HandleAsync(async () =>
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Importar perfil VCC Office",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Perfis JSON") { Patterns = ["*.json"] }]
            });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync();
            if (stream.CanSeek && stream.Length > 65536)
                throw new InvalidDataException("Perfil importado demasiado grande.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var imported = _profiles.Import(await reader.ReadToEndAsync());
            await _profiles.SaveAsync(imported);
            await RefreshAsync(imported.Name);
            status.Text = "Perfil importado: " + imported.Name;
        });
        window.Content = new ScrollViewer { Content = panel };
        window.Opened += async (_, _) => await HandleAsync(() => RefreshAsync(_launcherState.ActiveProfileName));
        _ = window.ShowDialog(this);
    }
}
