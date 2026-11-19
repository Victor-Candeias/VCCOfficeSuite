using System.Reflection;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    private readonly HttpClient _updatesHttp = new() { Timeout = TimeSpan.FromMinutes(10) };
    private SuiteUpdateService? _updatesService;

    private void ShowSuiteUpdates()
    {
        var service = _updatesService ??= new SuiteUpdateService(_updatesHttp,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VCCOffice", "updates"));
        var currentVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var dialog = new Window
        {
            Title = "VCC Office — Atualizações",
            Icon = AppBranding.LoadWindowIcon(),
            Width = 570, Height = 320, MinWidth = 430,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(22), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Atualizações da VCC Office Suite",
            FontSize = 22, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Versão instalada: " + currentVersion });
        var status = new TextBlock
        {
            Text = "As atualizações são obtidas exclusivamente das releases oficiais do GitHub.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        panel.Children.Add(status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var check = new Button { Content = "Procurar atualizações" };
        var download = new Button { Content = "Descarregar e verificar", IsEnabled = false };
        var close = new Button { Content = "Fechar" };
        SuiteUpdate? available = null;
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false;
            download.IsEnabled = false;
            status.Text = "A verificar releases oficiais...";
            try
            {
                var result = await service.CheckAsync(currentVersion);
                available = result.Available;
                status.Text = result.Message;
                download.IsEnabled = available is not null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
                System.Text.Json.JsonException or InvalidDataException or InvalidOperationException)
            {
                status.Text = "Não foi possível verificar atualizações: " + ex.Message;
            }
            finally { check.IsEnabled = true; }
        };
        download.Click += async (_, _) =>
        {
            if (available is null) return;
            check.IsEnabled = false;
            download.IsEnabled = false;
            status.Text = "A descarregar e verificar a integridade do pacote...";
            try
            {
                var path = await service.StageAsync(available);
                status.Text = "Download validado e guardado em: " + path +
                    "\nFeche a aplicação antes de efetuar a atualização manual. " +
                    "O ZIP não é executado automaticamente.";
                await RecordNotificationAsync(OfficeNotificationCategory.System,
                    OfficeNotificationSeverity.Success, "Atualização descarregada",
                    "Pacote " + available.Version + " verificado com SHA-256 e preparado para instalação manual.",
                    "Atualizações");
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or
                UnauthorizedAccessException or InvalidDataException or TaskCanceledException)
            {
                status.Text = "O pacote não foi preparado: " + ex.Message;
            }
            finally { check.IsEnabled = true; }
        };
        close.Click += (_, _) => dialog.Close();
        actions.Children.Add(check);
        actions.Children.Add(download);
        actions.Children.Add(close);
        panel.Children.Add(actions);
        panel.Children.Add(new TextBlock
        {
            Text = "Canal estável · sem instalação silenciosa · sem alterações a documentos, plugins ou definições.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        dialog.Content = new ScrollViewer { Content = panel };
        _ = dialog.ShowDialog(this);
    }
}
