#!/usr/bin/env python3
"""Fail the release if the standalone Writer plugin was not packaged."""
import json
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("Uso: check-writer-plugin-package.py <publish-directory>")
root = Path(sys.argv[1]).resolve()
plugin = root / "Plugins" / "Writer"
manifest_file = plugin / "plugin.json"
assembly_file = plugin / "Vcc.Office.Writer.dll"
if not manifest_file.is_file() or not assembly_file.is_file():
    raise SystemExit(f"Writer plugin DLL/manifest missing from: {plugin}")
manifest = json.loads(manifest_file.read_text(encoding="utf-8"))
if (manifest.get("id"), manifest.get("assembly"), manifest.get("entryType"), manifest.get("apiVersion")) != (
    "writer", "Vcc.Office.Writer.dll", "Vcc.Office.Writer.WriterPlugin", 1
):
    raise SystemExit("Writer plugin manifest is invalid.")
print(f"PASS Writer plugin distribution: {assembly_file}")
