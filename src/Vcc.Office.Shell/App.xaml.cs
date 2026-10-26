using System.Windows;

namespace Vcc.Office.Shell;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
