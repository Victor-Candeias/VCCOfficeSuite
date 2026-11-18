# VCC-0822 — Sheets as an independent DLL plugin

Vcc.Office.Sheets owns the Avalonia window, spreadsheet engine, models, plugin metadata, and icon. The host never links the Sheets assembly. It discovers `Plugins/Sheets/plugin.json`, loads `Vcc.Office.Sheets.dll` and obtains menu metadata through `IOfficePlugin`.

Spreadsheet persistence is unchanged (LocalApplicationData/VCC Office/sheets-avalonia.json). The CoreShell retains only the shared storage primitives and ValidationResult.

Acceptance: Windows, Linux and macOS Release build and smoke tests, including icon, menu registration, standalone window creation, and existing spreadsheet calculations and exports. Missing Sheets DLL results in no Sheets application in the Launcher.
