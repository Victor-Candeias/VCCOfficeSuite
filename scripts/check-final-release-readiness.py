#!/usr/bin/env python3
"""Fail-closed structural readiness checks before official release packaging."""
from pathlib import Path
import argparse
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def check_source(root: Path) -> list[str]:
    failures = []
    required = [
        "VCC.Office.Avalonia.sln",
        "src/Vcc.Office.Desktop/Vcc.Office.Desktop.csproj",
        "src/Vcc.Office.PluginHost/OfficePluginHost.cs",
        "src/Vcc.Office.PluginHost/OfficePluginPackageManager.cs",
        "src/Vcc.Office.CoreShell/SuiteUpdateService.cs",
        "src/Vcc.Office.CoreShell/OfficeDiagnosticsService.cs",
        "src/Vcc.Office.Desktop/MainWindow.Cloud.cs",
        ".github/workflows/release.yml",
        "scripts/release_security.py",
        "scripts/verify-code-signature.ps1",
        "docs/architecture/official-release-security.md",
    ]
    for relative in required:
        if not (root / relative).is_file():
            failures.append(f"Missing release component: {relative}")
    return failures


def check_package(path: Path) -> list[str]:
    if not path.is_file():
        return ["Release archive does not exist"]
    try:
        with zipfile.ZipFile(path) as archive:
            entries = archive.namelist()
            if not entries:
                return ["Release archive is empty"]
            if len(entries) != len(set(entries)):
                return ["Duplicate ZIP entries"]
            if any(name.startswith("/") or "\\" in name or ".." in name.split("/") for name in entries):
                return ["Unsafe ZIP member paths"]
            if not any(name.endswith(".dll") for name in entries):
                return ["Release archive contains no DLL assemblies"]
    except (OSError, zipfile.BadZipFile) as exc:
        return [f"Unreadable release archive: {exc}"]
    return []


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--archive", type=Path)
    args = parser.parse_args()
    failures = check_source(args.root)
    if args.archive is not None:
        failures.extend(check_package(args.archive))
    for failure in failures:
        print("FAIL:", failure, file=sys.stderr)
    if failures:
        return 1
    print("PASS release readiness: source dependencies and optional package structure")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
