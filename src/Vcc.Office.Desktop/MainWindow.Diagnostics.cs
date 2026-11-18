using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    private bool _diagnosticsPage;
    private DiagnosticReport? _diagnosticReport;
    private readonly OfficeDiagnosticsService _diagnostics = new(
        StoragePaths.ForLocalApplication(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCC Office", "shell-state.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VCC Office", "avalonia-application-session.json"));

    private void ShowDiagnosticsPage()
    {
        _diagnosticsPage = true;
        _notificationsPage = false;
        _documentsPage = false;
        _templatesPage = false;
        _selectedSidebarModuleId = null;
        _ = ScanDiagnosticsAsync();
        Render();
    }

    private async Task ScanDiagnosticsAsync()
    {
        try
        {
            _diagnosticReport = await _diagnostics.ScanAsync();
            if (_diagnosticsPage) RefreshBody();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException)
        {
            ShowNotice("Diagnóstico indisponível", ex.Message);
        }
    }

    private void BuildDiagnosticsPage(StackPanel root)
    {
        root.Children.Add(Text("Diagnóstico e recuperação", 27, true));
        root.Children.Add(Text("Verificações locais apenas de leitura. Nenhum documento é alterado durante o diagnóstico.",
            13, false, Muted));
        var refresh = new Button { Content = "Executar diagnóstico" };
        refresh.Click += async (_, _) => await ScanDiagnosticsAsync();
        root.Children.Add(refresh);
        if (_diagnosticReport is null)
        {
            root.Children.Add(Text("A recolher informação...", 13, false, Muted));
            return;
        }
        root.Children.Add(Text("Última verificação: " +
            _diagnosticReport.CheckedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"), 12, false, Muted));
        foreach (var check in _diagnosticReport.Checks)
        {
            var symbol = check.Status switch
            {
                DiagnosticStatus.Healthy => "✓",
                DiagnosticStatus.Warning => "⚠",
                _ => "✕"
            };
            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(Text(symbol + "  " + check.Title, 16, true));
            content.Children.Add(Text(check.Details, 13, false, Muted));
            root.Children.Add(Card(content, new Thickness(14)));
        }
        var restore = new Button { Content = "Recuperar estado da interface a partir do backup" };
        restore.Click += (_, _) => ConfirmShellRecovery();
        root.Children.Add(restore);
        root.Children.Add(Text("A recuperação exige confirmação, preserva o estado atual numa cópia independente " +
            "e só altera as definições do Launcher. Reinicie a aplicação para carregar o estado recuperado.",
            12, false, Muted));
    }

    private void ConfirmShellRecovery()
    {
        var dialog = new Window
        {
            Title = "Confirmar recuperação",
            Icon = AppBranding.LoadWindowIcon(),
            Width = 450,
            Height = 230,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Deseja restaurar as definições do Launcher a partir do backup? " +
                "Será guardada uma cópia do estado atual antes da recuperação.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new Button { Content = "Cancelar" };
        cancel.Click += (_, _) => dialog.Close();
        var confirm = new Button { Content = "Recuperar" };
        confirm.Click += async (_, _) =>
        {
            confirm.IsEnabled = false;
            try
            {
                await _diagnostics.RestoreShellBackupAsync();
                dialog.Close();
                await RecordNotificationAsync(OfficeNotificationCategory.System,
                    OfficeNotificationSeverity.Success, "Definições recuperadas",
                    "O estado do Launcher foi restaurado a partir de um backup validado.");
                await ScanDiagnosticsAsync();
                ShowNotice("Recuperação concluída", "Reinicie a aplicação para aplicar as definições recuperadas.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Text.Json.JsonException or InvalidDataException or ArgumentException)
            {
                ShowNotice("Não foi possível recuperar o estado", ex.Message);
            }
            finally { confirm.IsEnabled = true; }
        };
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        panel.Children.Add(actions);
        dialog.Content = panel;
        _ = dialog.ShowDialog(this);
    }
}
