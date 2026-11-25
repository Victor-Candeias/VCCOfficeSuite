using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Vcc.Office.Android;

/// <summary>
/// Android entry point. Desktop retains its own ClassicDesktopStyleApplicationLifetime.
/// Android 12 uses a view factory because an Activity can be recreated by the OS.
/// </summary>
public sealed class App : global::Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IActivityApplicationLifetime lifetime)
        {
            lifetime.MainViewFactory = () => new LauncherView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
