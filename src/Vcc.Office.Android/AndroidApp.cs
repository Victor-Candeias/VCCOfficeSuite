using Android.App;
using Android.Runtime;
using Avalonia.Android;

namespace Vcc.Office.Android;

[Application]
public sealed class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}
