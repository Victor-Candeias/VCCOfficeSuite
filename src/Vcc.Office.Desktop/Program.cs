using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Markup.Xaml.Styling;
using ReactiveUI;
using ReactiveUI.Avalonia;
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
            .AfterSetup(builder =>
            {
                RxSchedulers.MainThreadScheduler = AvaloniaScheduler.Instance;
                if (builder.Instance is { } app)
                {
                    app.Styles.Add(new FluentTheme());
                    // Only the Writer hosts a ribbon; its theme is registered for that module.
                    app.Styles.Add(new StyleInclude(new Uri("avares://ProEdit.Ribbon.Avalonia/"))
                    {
                        Source = new Uri("avares://ProEdit.Ribbon.Avalonia/WordRibbonTheme.axaml")
                    });
                }
            })
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
