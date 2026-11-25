#!/usr/bin/env python3
"""Decode a base64 Android keystore without logging secret values."""
import base64
import binascii
import os
from pathlib import Path
import sys

if len(sys.argv) != 2:
    raise SystemExit("Usage: prepare_keystore.py /secure/temporary/path.keystore")
keys = ["ANDROID_KEYSTORE_BASE64", "ANDROID_KEYSTORE_PASSWORD", "ANDROID_KEY_ALIAS", "ANDROID_KEY_PASSWORD"]
missing = [name for name in keys if not os.environ.get(name)]
if missing:
    raise SystemExit("Missing GitHub Actions secrets: " + ", ".join(missing))
try:
    data = base64.b64decode(os.environ["ANDROID_KEYSTORE_BASE64"].strip(), validate=True)
except binascii.Error as exc:
    raise SystemExit("ANDROID_KEYSTORE_BASE64 is not valid base64") from exc
if len(data) < 100:
    raise SystemExit("Decoded keystore is unexpectedly small")
path = Path(sys.argv[1])
path.parent.mkdir(parents=True, exist_ok=True)
fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
with os.fdopen(fd, "wb") as output:
    output.write(data)
print("Signing keystore restored in a temporary private file")
