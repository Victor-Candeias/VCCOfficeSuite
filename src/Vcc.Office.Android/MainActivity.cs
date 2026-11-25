using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Vcc.Office.Android;

[Activity(
    Label = "VCC Office Suite",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
