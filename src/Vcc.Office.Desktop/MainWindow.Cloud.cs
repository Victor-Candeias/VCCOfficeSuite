using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Vcc.Office.CoreShell;

namespace Vcc.Office.Desktop;

public sealed partial class MainWindow
{
    private void ShowCloudFolder()
    {
        var dialog = new Window
        {
            Title = "VCC Office — Armazenamento cloud",
            Icon = AppBranding.LoadWindowIcon(),
            Width = 600,
            Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Pasta sincronizada com cloud",
            FontSize = 23,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Escolha uma pasta já sincronizada por OneDrive, Dropbox ou outro cliente. " +
                "A VCC Office copia ficheiros localmente; a sincronização remota continua a cargo desse cliente.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        var current = new TextBlock { Text = "Nenhuma pasta selecionada.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        panel.Children.Add(current);
        CloudSyncFolderService? service = null;
        var choose = new Button { Content = "Escolher pasta sincronizada" };
        choose.Click += async (_, _) =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Selecionar pasta cloud sincronizada",
                AllowMultiple = false
            });
            if (folders.Count == 0) return;
            try
            {
                var path = folders[0].TryGetLocalPath()
                    ?? throw new InvalidOperationException("A pasta tem de estar disponível localmente.");
                service = new CloudSyncFolderService(path);
                current.Text = "Pasta selecionada: " + path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                InvalidOperationException or ArgumentException)
            {
                current.Text = "Não foi possível usar a pasta: " + ex.Message;
            }
        };
        panel.Children.Add(choose);
        var export = new Button { Content = "Copiar documento para a pasta" };
        export.Click += async (_, _) =>
        {
            var selectedService = service;
            if (selectedService is null)
            {
                current.Text = "Selecione primeiro uma pasta.";
                return;
            }
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Documento a copiar", AllowMultiple = false
            });
            if (files.Count == 0) return;
            try
            {
                var path = files[0].TryGetLocalPath()
                    ?? throw new InvalidOperationException("Apenas ficheiros locais.");
                var result = await selectedService.ExportCopyAsync(path);
                current.Text = "Cópia local concluída: " + result +
                    "\nO envio remoto depende do estado do cliente de sincronização.";
                await RecordNotificationAsync(OfficeNotificationCategory.Document,
                    OfficeNotificationSeverity.Success, "Cópia para pasta sincronizada",
                    Path.GetFileName(path), "Cloud");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                InvalidOperationException or ArgumentException)
            {
                current.Text = "Falha na cópia: " + ex.Message;
            }
        };
        panel.Children.Add(export);
        var close = new Button { Content = "Fechar" };
        close.Click += (_, _) => dialog.Close();
        panel.Children.Add(close);
        dialog.Content = new ScrollViewer { Content = panel };
        _ = dialog.ShowDialog(this);
    }
}
