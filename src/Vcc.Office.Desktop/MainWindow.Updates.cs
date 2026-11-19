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

    private async Task ReportPreviousInstallationAsync()
    {
        var statusFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCCOffice", "updates", "last-install-result.txt");
        if (!File.Exists(statusFile)) return;
        try
        {
            var message = await File.ReadAllTextAsync(statusFile);
            File.Delete(statusFile);
            if (string.IsNullOrWhiteSpace(message)) return;
            var succeeded = message.StartsWith("Atualização instalada", StringComparison.Ordinal);
            await RecordNotificationAsync(OfficeNotificationCategory.System,
                succeeded ? OfficeNotificationSeverity.Success : OfficeNotificationSeverity.Warning,
                succeeded ? "Atualização instalada" : "Resultado da atualização",
                message, "Atualizações");
        }
        catch (IOException) { /* An inaccessible status file must not prevent startup. */ }
        catch (UnauthorizedAccessException) { /* Keep running with a read-only profile. */ }
    }

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
        var install = new Button { Content = "Instalar ao fechar (com rollback)", IsEnabled = false };
        var close = new Button { Content = "Fechar" };
        string? verifiedArchive = null;
        SuiteUpdate? available = null;
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false;
            download.IsEnabled = false;
            install.IsEnabled = false;
            verifiedArchive = null;
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
                verifiedArchive = path;
                install.IsEnabled = true;
                status.Text = "Download validado: " + path +
                    "\nPode instalar após fechar a suite. Guarde primeiro os documentos em edição.";
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
        install.Click += async (_, _) =>
        {
            if (available is null || verifiedArchive is null) return;
            var selectedUpdate = available;
            var archive = verifiedArchive;
            var confirmation = new Window
            {
                Title = "Confirmar instalação",
                Icon = AppBranding.LoadWindowIcon(),
                Width = 490, Height = 230,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var confirmPanel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
            confirmPanel.Children.Add(new TextBlock
            {
                Text = "A instalação fecha a VCC Office Suite. Guarde todos os documentos antes de continuar. " +
                    "A versão anterior fica em backup e será restaurada se a nova instalação falhar na verificação de arranque.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
            var confirmActions = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancelAction = new Button { Content = "Cancelar" };
            cancelAction.Click += (_, _) => confirmation.Close(false);
            var accept = new Button { Content = "Instalar e fechar" };
            accept.Click += (_, _) => confirmation.Close(true);
            confirmActions.Children.Add(cancelAction);
            confirmActions.Children.Add(accept);
            confirmPanel.Children.Add(confirmActions);
            confirmation.Content = confirmPanel;
            if (!await confirmation.ShowDialog<bool>(dialog)) return;

            install.IsEnabled = false;
            download.IsEnabled = false;
            check.IsEnabled = false;
            status.Text = "A preparar instalação verificada e cópia auxiliar...";
            try
            {
                var plan = await SuiteUpdateInstaller.PrepareAsync(
                    archive, selectedUpdate.ExpectedSha256, selectedUpdate.Version,
                    AppContext.BaseDirectory, Environment.ProcessId);
                var worker = SuiteUpdateHandoff.PrepareWorker(plan);
                var planPath = Path.Combine(worker, SuiteUpdateInstaller.PlanName);
                await SuiteUpdateInstaller.SavePlanAsync(plan, planPath);
                SuiteUpdateHandoff.LaunchWorker(worker, planPath, plan.ExpectedExecutable);
                // Worker is outside the installation and waits for this process to exit.
                dialog.Close();
                Close();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                InvalidDataException or InvalidOperationException or ArgumentException or
                System.ComponentModel.Win32Exception)
            {
                status.Text = "Instalação não iniciada: " + ex.Message;
                install.IsEnabled = true;
                check.IsEnabled = true;
                download.IsEnabled = true;
            }
        };
        actions.Children.Add(install);
        close.Click += (_, _) => dialog.Close();
        actions.Children.Add(check);
        actions.Children.Add(download);
        actions.Children.Add(close);
        panel.Children.Add(actions);
        panel.Children.Add(new TextBlock
        {
            Text = "Canal estável · instalação apenas com confirmação · backup e rollback · sem alteração de documentos ou definições.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        dialog.Content = new ScrollViewer { Content = panel };
        _ = dialog.ShowDialog(this);
    }
}
