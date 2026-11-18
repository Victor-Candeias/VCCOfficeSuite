"""Regression tests for VCC-0828 release archive hashing and NuGet gates."""
import importlib.util
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

MODULE = Path(__file__).resolve().parents[2] / "scripts" / "release_security.py"
SPEC = importlib.util.spec_from_file_location("release_security", MODULE)
security = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(security)


class ReleaseSecurityTests(unittest.TestCase):
    def test_pack_verify_detects_tampering(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "publish"
            source.mkdir()
            (source / "VCC Office Suite.exe").write_bytes(b"test executable")
            (source / "Plugins").mkdir()
            (source / "Plugins" / "Writer.dll").write_bytes(b"test plugin")
            archive = root / "release.zip"
            security.pack(source, archive)
            security.verify(archive)
            with zipfile.ZipFile(archive, "a") as handle:
                handle.writestr("unexpected-file", b"malicious addition")
            with self.assertRaisesRegex(ValueError, "Unlisted"):
                security.verify(archive)

    def test_reject_symbolic_link(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "real").write_bytes(b"data")
            try:
                (root / "link").symlink_to(root / "real")
            except (OSError, NotImplementedError):
                self.skipTest("Symlinks unavailable")
            with self.assertRaisesRegex(ValueError, "Symbolic links"):
                security.inputs(root)

    def test_nuget_audit_fails_on_vulnerabilities(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "audit.json"
            data = {"projects": [{"path": "src/example.csproj", "frameworks": [
                {"topLevelPackages": [{"id": "Unsafe", "vulnerabilities": [
                    {"severity": "Critical", "advisoryurl": "https://example.invalid"}]}]}]}]}
            path.write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "1 known vulnerable"):
                security.audit(path)
            data["projects"][0]["frameworks"][0]["topLevelPackages"][0]["vulnerabilities"] = []
            path.write_text(json.dumps(data), encoding="utf-8")
            security.audit(path)


if __name__ == "__main__":
    unittest.main()
