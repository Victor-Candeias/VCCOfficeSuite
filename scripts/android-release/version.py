#!/usr/bin/env python3
"""Validate an Android release semver and emit a deterministic monotonically ordered versionCode."""
import re
import sys

if len(sys.argv) != 2:
    raise SystemExit("Usage: version.py MAJOR.MINOR.PATCH")
version = sys.argv[1]
match = re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", version)
if not match:
    raise SystemExit("Invalid release version: use MAJOR.MINOR.PATCH (no prerelease or leading zeros)")
major, minor, patch = map(int, match.groups())
if major > 2099 or minor > 999 or patch > 999:
    raise SystemExit("Version outside supported range: major <= 2099, minor/patch <= 999")
version_code = major * 1000000 + minor * 1000 + patch
if version_code < 1 or version_code > 2100000000:
    raise SystemExit("Android versionCode must be between 1 and 2100000000")
print("version=" + version)
print("version_code=" + str(version_code))
