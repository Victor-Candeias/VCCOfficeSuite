#!/usr/bin/env python3
"""Ensure the launcher and shared session code never reference Writer implementation."""
from pathlib import Path

root = Path(__file__).resolve().parent.parent
checks = {
    "src/Vcc.Office.Desktop/MainWindow.cs": (
        '_writerWindow', 'module?.Id == "writer"', 'new WriterWindow(',
        'TryCreateWindow("writer"', 'ModuleVisuals.Badge("writer"'),
    ),
    "src/Vcc.Office.Desktop/Vcc.Office.Desktop.csproj": (
        'Vcc.Office.Writer.csproj', 'Vcc.Office.Writer.dll',
        'StageWriterPluginForBuild', 'StageWriterPluginForPublish',
    ),
    "src/Vcc.Office.CoreShell/AvaloniaModuleSession.cs": (
        'new OfficeModule("writer"',),
    "src/Vcc.Office.CoreShell/OfficeApplicationRegistry.cs": (
        '"writer",',),
}
for relative, forbidden in checks.items():
    content = (root / relative).read_text(encoding="utf-8")
    for marker in forbidden:
        if marker in content:
            raise SystemExit(f"Plugin boundary violation in {relative}: {marker}")
print("PASS plugin boundaries: host and session registry do not embed Writer implementation")
