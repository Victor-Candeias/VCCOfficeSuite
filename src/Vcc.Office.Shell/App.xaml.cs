using System.Windows;
using System.Windows.Threading;

namespace Vcc.Office.Shell;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A janela de arranque é mostrada antes da construção do MainWindow.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var splash = new SplashScreenWindow();
        splash.Show();

        try
        {
            // Deixar o WPF desenhar o splash antes da inicialização do shell.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            var main = new MainWindow();
            MainWindow = main;
            main.ContentRendered += (_, _) =>
            {
                if (splash.IsVisible) splash.Close();
            };

            main.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            splash.Close();
            MessageBox.Show("Não foi possível iniciar o VCC Office Suite.\n\n" + ex.Message,
                "VCC Office Suite", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
