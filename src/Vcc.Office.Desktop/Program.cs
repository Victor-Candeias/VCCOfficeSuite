using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Vcc.Office.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            Args = args
        };

        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .AfterSetup(builder => builder.Instance?.Styles.Add(new FluentTheme()))
            .SetupWithLifetime(lifetime);

        // Iniciar com o splash antes de construir a janela principal.
        var splash = new SplashScreenWindow();
        lifetime.MainWindow = splash;
        splash.Opened += (_, _) =>
        {
            // Ceder o primeiro ciclo de desenho ao splash; depois abrir o Launcher.
            Dispatcher.UIThread.Post(() =>
            {
                var main = new MainWindow();
                lifetime.MainWindow = main;
                main.Show();
                splash.Close();
            }, DispatcherPriority.Background);
        };
        lifetime.Start(args);
    }
}
