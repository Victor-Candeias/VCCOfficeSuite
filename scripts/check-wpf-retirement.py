#!/usr/bin/env python3
"""Fail closed if the WPF app is removed without documented parity acceptance.

This script deliberately does not infer UI equivalence from successful builds:
cross-platform smoke tests cannot substitute real OS and accessibility testing.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "docs" / "VCC-0820-10-retirement-readiness.json"
WPF_PROJECT = ROOT / "src" / "Vcc.Office.Shell" / "Vcc.Office.Shell.csproj"
LEGACY_WORKFLOW = ROOT / ".github" / "workflows" / "build.yml"
CANONICAL_SOLUTION = ROOT / "VCC.Office.sln"
AVALONIA_PROJECT = "src\\Vcc.Office.Desktop\\Vcc.Office.Desktop.csproj"
REQUIRED_CHECKS = (
    "launcher_and_dashboards",
    "writer_and_documents",
    "sheets_and_workbooks",
    "slides_and_presentations",
    "data_forms_and_reports",
    "recent_files_and_favorites",
    "workspace_settings_and_shortcuts",
    "accessibility_and_keyboard",
    "window_geometry_and_dpi",
    "legacy_data_import_and_rollback",
    "windows_real_ui_acceptance",
    "linux_real_ui_acceptance",
    "macos_real_ui_acceptance",
)


def main() -> None:
    state = json.loads(MANIFEST.read_text(encoding="utf-8"))
    checks = state.get("checks", {})
    missing = [name for name in REQUIRED_CHECKS if name not in checks]
    if missing:
        raise SystemExit(f"Missing required retirement checks: {', '.join(missing)}")

    if WPF_PROJECT.is_file():
        print("WPF retirement gate: WPF remains available; migration is not complete.")
        if state.get("retired") is True:
            raise SystemExit("Manifest claims WPF was retired but the WPF project still exists.")
        return

    incomplete = [
        name for name in REQUIRED_CHECKS
        if not isinstance(checks[name], dict)
        or checks[name].get("passed") is not True
        or not checks[name].get("evidence")
    ]
    if incomplete:
        # An explicit owner override authorizes the retirement without
        # rewriting the factual outcome of unperformed manual tests.
        # The missing checks remain marked passed=false in the manifest.
        if state.get("acceptance_mode") != "owner_override":
            raise SystemExit("WPF removal blocked; unverified parity: " + ", ".join(incomplete))
        acknowledgements = set(state.get("acknowledged_unverified_checks") or [])
        if not set(incomplete).issubset(acknowledgements):
            raise SystemExit("Owner override must acknowledge every unverified check: " + ", ".join(incomplete))
        if not state.get("override_reason"):
            raise SystemExit("Owner override must record why validation gaps were accepted.")
        print("WPF retirement gate: owner explicitly accepted unverified manual checks: " + ", ".join(incomplete))
    if state.get("retired") is not True or not state.get("approved_by") or not state.get("approved_at"):
        raise SystemExit("WPF removal requires explicit approval, approver and approval date.")
    if LEGACY_WORKFLOW.exists():
        raise SystemExit("Remove legacy WPF workflow after verified retirement.")
    if not CANONICAL_SOLUTION.is_file():
        raise SystemExit("Canonical solution is missing.")
    solution = CANONICAL_SOLUTION.read_text(encoding="utf-8")
    if AVALONIA_PROJECT not in solution or "Vcc.Office.Shell" in solution:
        raise SystemExit("Canonical solution must be Avalonia-only after WPF retirement.")
    for path in (ROOT / "src").rglob("*.csproj"):
        if "Vcc.Office.Shell" in path.read_text(encoding="utf-8"):
            raise SystemExit(f"Remaining WPF project reference: {path}")
    print("WPF retirement gate: approved retirement, canonical Avalonia solution and no WPF project/workflow.")


if __name__ == "__main__":
    main()
