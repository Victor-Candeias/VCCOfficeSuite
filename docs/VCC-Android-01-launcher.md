# VCC-Android-01 — Android Launcher bootstrap

Issue: #574

## Changes

- New independent Android startup project at `src/Vcc.Office.Android/`.
- Targets `net10.0-android` with Avalonia 12.1.6, consistent with the desktop.
- Uses Android's `AvaloniaAndroidApplication<App>` and non-generic `AvaloniaMainActivity` lifecycle.
- Uses `IActivityApplicationLifetime.MainViewFactory` to recreate the Launcher after Activity recreation.
- Launcher shows the **existing CoreShell registry** (`OfficeApplicationRegistry`) without claiming non-installed applications work.
- The desktop entry point and desktop solution are deliberately unchanged.

This is the **Android Launcher bootstrap**, not a port of the desktop `MainWindow`, which is a `Window` and requires desktop APIs. Existing modules are not yet clickable; plugin discovery/loading belongs to VCC-Android-02 and full responsive experience to VCC-Android-03.

## Build prerequisites

.NET 10 SDK, Android workload, Android SDK and compatible JDK.

```bash
dotnet workload install android
dotnet restore src/Vcc.Office.Android/Vcc.Office.Android.csproj
dotnet build src/Vcc.Office.Android/Vcc.Office.Android.csproj -c Debug
```

Run on an Android emulator/device through Visual Studio or the installed Android SDK tooling. The Android project is **intentionally excluded** from `VCC.Office.Avalonia.sln` so existing desktop builds do not require the Android workload.

## Acceptance checklist

- [ ] Android project restores and builds with official .NET 10 Android SDK.
- [ ] Android app installs and opens `LauncherView` on emulator/device.
- [ ] Activity recreation (rotate/background/reopen) recreates the Launcher without crash.
- [ ] Shared CoreShell catalog is displayed.
- [ ] Desktop CI passes Windows, Linux, macOS.

Do not check off VCC-Android-01 in #574 or merge to main until the Android runtime and desktop regression checks pass.
