#!/usr/bin/env python3
"""Verify Release APK signature, metadata, and SHA-256 before upload."""
from pathlib import Path
import hashlib
import os
import re
import shutil
import subprocess
import sys

if len(sys.argv) != 5:
    raise SystemExit("Usage: verify_apk.py publish_dir output_dir version version_code")
publish_dir, output_dir = Path(sys.argv[1]), Path(sys.argv[2])
version, version_code = sys.argv[3:]
signed_apks = sorted(p for p in publish_dir.rglob("*.apk") if p.name.lower().endswith("-signed.apk"))
if len(signed_apks) != 1:
    raise SystemExit(f"Expected exactly one *-Signed.apk under {publish_dir}, found {len(signed_apks)}")
sdk_root = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT")
if not sdk_root:
    raise SystemExit("ANDROID_HOME or ANDROID_SDK_ROOT must point to Android SDK")
build_tools = Path(sdk_root) / "build-tools"
tools = sorted(
    (p for p in build_tools.iterdir() if p.is_dir()),
    key=lambda p: tuple(int(x) for x in re.findall(r"\d+", p.name)),
    reverse=True,
) if build_tools.exists() else []
if not tools:
    raise SystemExit("Android SDK build-tools not found")
apksigner = next((p / "apksigner" for p in tools if (p / "apksigner").exists()), None)
aapt = next((p / "aapt" for p in tools if (p / "aapt").exists()), None)
if apksigner is None or aapt is None:
    raise SystemExit("Android SDK apksigner or aapt missing")
apk = signed_apks[0]
verification = subprocess.run([str(apksigner), "verify", "--verbose", "--print-certs", str(apk)],
                              text=True, capture_output=True, check=True)
if "Verified using v2 scheme (APK Signature Scheme v2): true" not in verification.stdout:
    raise SystemExit("APK has no verified v2 signature")
metadata = subprocess.run([str(aapt), "dump", "badging", str(apk)],
                          text=True, capture_output=True, check=True).stdout
package = next((s for s in metadata.splitlines() if s.startswith("package: ")), "")
if not package:
    raise SystemExit("Unable to read APK package metadata")
if "name='pt.vcc.officesuite'" not in package:
    raise SystemExit("Unexpected Android package id")
if f"versionCode='{version_code}'" not in package or f"versionName='{version}'" not in package:
    raise SystemExit(f"APK version mismatch: {package}")
output_dir.mkdir(parents=True, exist_ok=True)
destination = output_dir / f"VCCOfficeSuite-Android-v{version}-Release.apk"
shutil.copy2(apk, destination)
checksum = hashlib.sha256(destination.read_bytes()).hexdigest()
(output_dir / (destination.name + ".sha256")).write_text(
    f"{checksum}  {destination.name}\n", encoding="utf-8")
print(f"Verified signed APK: {destination.name} (versionCode={version_code})")
print(f"SHA256: {checksum}")
