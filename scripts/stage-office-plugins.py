#!/usr/bin/env python3
"""Discover built Office plugins and stage DLLs + manifests into a release folder.

The host project has NO references or copy targets for individual plugins.
Use after building the solution: python scripts/stage-office-plugins.py <publish-dir> Release
"""
import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
if len(sys.argv) != 3:
    raise SystemExit("Usage: stage-office-plugins.py <publish-dir> <Debug|Release>")
destination = Path(sys.argv[1]).resolve() / "Plugins"
configuration = sys.argv[2]
if configuration not in ("Debug", "Release"):
    raise SystemExit("Invalid build configuration")
manifests = sorted((ROOT / "src").glob("*/plugin.json"))
if not manifests:
    raise SystemExit("No plugin manifests in src/")
seen = set()
for manifest_path in manifests:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    plugin_id = manifest.get("id")
    assembly = manifest.get("assembly")
    if not isinstance(plugin_id, str) or not plugin_id or not plugin_id.replace("-", "").replace("_", "").isalnum():
        raise SystemExit(f"Invalid plugin ID in {manifest_path}")
    if not isinstance(assembly, str) or Path(assembly).name != assembly or not assembly.endswith(".dll"):
        raise SystemExit(f"Invalid DLL name in {manifest_path}")
    if plugin_id.lower() in seen:
        raise SystemExit(f"Duplicate plugin ID: {plugin_id}")
    seen.add(plugin_id.lower())
    binary = manifest_path.parent / "bin" / configuration / "net10.0" / assembly
    if not binary.is_file():
        raise SystemExit(f"Plugin not compiled: {binary}")
    output = destination / plugin_id
    output.mkdir(parents=True, exist_ok=True)
    shutil.copy2(manifest_path, output / "plugin.json")
    shutil.copy2(binary, output / assembly)
    print(f"Staged {plugin_id}: {output / assembly}")
