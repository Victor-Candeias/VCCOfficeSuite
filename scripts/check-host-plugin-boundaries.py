#!/usr/bin/env python3
"""Enforce application-neutral Launcher boundaries across Office plugins."""
from pathlib import Path

root = Path(__file__).resolve().parent.parent
apps = ("Writer", "Sheets", "Slides", "Data")
main = (root / "src/Vcc.Office.Desktop/MainWindow.cs").read_text(encoding="utf-8")
project = (root / "src/Vcc.Office.Desktop/Vcc.Office.Desktop.csproj").read_text(encoding="utf-8")
session = (root / "src/Vcc.Office.CoreShell/AvaloniaModuleSession.cs").read_text(encoding="utf-8")
for app in apps:
    key = app.lower()
    for marker in (f"new {app}Window(", f"private {app}Window", f"_" + key + "Window",
                   f'module?.Id == "{key}"', f'TryCreateWindow("{key}"'):
        if marker in main:
            raise SystemExit(f"Host contains app-specific implementation: {marker}")
    for marker in (f"Vcc.Office.{app}.csproj", f"Vcc.Office.{app}.dll"):
        if marker in project:
            raise SystemExit(f"Desktop project references plugin: {marker}")
    if f'new OfficeModule("{key}"' in session:
        raise SystemExit(f"Session code hardcodes app availability: {app}")
if "OfficePluginHost.Discover" not in main or "InstalledApplications" not in main:
    raise SystemExit("Launcher must discover and list installed plugins dynamically")
print("PASS plugin boundaries: Launcher is application-neutral")
