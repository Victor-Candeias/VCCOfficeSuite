#!/usr/bin/env python3
"""VCC-0828: deterministic archives, integrity manifests and NuGet vulnerability gating."""
import argparse
import hashlib
import json
import sys
import zipfile
from pathlib import Path


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def inputs(root):
    if not root.is_dir():
        raise ValueError("Release directory does not exist")
    result = []
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            raise ValueError("Symbolic links are forbidden in published packages: " + str(path))
        if path.is_file():
            relative = path.relative_to(root).as_posix()
            if relative in {"SHA256SUMS.txt", "RELEASE-METADATA.json"}:
                raise ValueError("Reserved release filename: " + relative)
            result.append((relative, path))
    if not result:
        raise ValueError("Release directory is empty")
    return result


def pack(source, output):
    source = source.resolve()
    files = inputs(source)
    output.parent.mkdir(parents=True, exist_ok=True)
    hashes = "".join(f"{digest(path)}  {relative}\n" for relative, path in files)
    temp = output.with_suffix(output.suffix + ".tmp")
    try:
        with zipfile.ZipFile(temp, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for relative, path in files:
                entry = zipfile.ZipInfo(relative, (2020, 1, 1, 0, 0, 0))
                entry.compress_type = zipfile.ZIP_DEFLATED
                entry.external_attr = 0o100644 << 16
                archive.writestr(entry, path.read_bytes(), compress_type=zipfile.ZIP_DEFLATED)
            entry = zipfile.ZipInfo("SHA256SUMS.txt", (2020, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, hashes.encode("utf-8"))
        verify(temp)
        temp.replace(output)
    finally:
        temp.unlink(missing_ok=True)
    print("Release archive verified:", output, digest(output))


def verify(filename):
    with zipfile.ZipFile(filename) as archive:
        expected = archive.read("SHA256SUMS.txt").decode("utf-8").splitlines()
        known = {}
        for line in expected:
            if len(line) < 67 or line[64:66] != "  ":
                raise ValueError("Invalid checksum manifest")
            name = line[66:]
            if not name or name in known:
                raise ValueError("Missing or duplicate path in checksum manifest")
            known[name] = line[:64]
        contents = [entry.filename for entry in archive.infolist()]
        if len(contents) != len(set(contents)):
            raise ValueError("Duplicate archive names")
        if sorted(contents) != sorted([*known, "SHA256SUMS.txt"]):
            raise ValueError("Unlisted archive entry")
        for name, expected_sha in known.items():
            if name.startswith("/") or "\\" in name or ".." in name.split("/"):
                raise ValueError("Unsafe archive path")
            actual = hashlib.sha256(archive.read(name)).hexdigest()
            if actual != expected_sha:
                raise ValueError("Checksum mismatch: " + name)
    print("PASS archive checksum manifest:", filename)


def checksums(directory, output):
    files = sorted(path for path in directory.iterdir() if path.is_file() and path != output)
    if not files:
        raise ValueError("No release files to hash")
    output.write_text("".join(f"{digest(path)}  {path.name}\n" for path in files), encoding="utf-8")
    print("PASS release SHA256SUMS:", output)


def audit(report):
    parsed = json.loads(report.read_text(encoding="utf-8"))
    if not isinstance(parsed.get("projects"), list):
        raise ValueError("Expected NuGet JSON vulnerability report")
    findings = []
    for project in parsed["projects"]:
        for framework in project.get("frameworks", []):
            for group in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(group, []):
                    for item in package.get("vulnerabilities", []):
                        findings.append((project.get("path"), package.get("id"),
                                         item.get("severity"), item.get("advisoryurl")))
    if findings:
        for finding in findings:
            print("VULNERABLE:", *finding, file=sys.stderr)
        raise ValueError(f"{len(findings)} known vulnerable NuGet dependencies")
    print("PASS NuGet vulnerability report:", len(parsed["projects"]), "projects")


def main():
    parser = argparse.ArgumentParser()
    actions = parser.add_subparsers(dest="action", required=True)
    p = actions.add_parser("pack")
    p.add_argument("--source", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    v = actions.add_parser("verify")
    v.add_argument("archive", type=Path)
    q = actions.add_parser("checksums")
    q.add_argument("--directory", type=Path, required=True)
    q.add_argument("--output", type=Path, required=True)
    a = actions.add_parser("audit")
    a.add_argument("report", type=Path)
    args = parser.parse_args()
    try:
        if args.action == "pack":
            pack(args.source, args.output)
        elif args.action == "verify":
            verify(args.archive)
        elif args.action == "checksums":
            checksums(args.directory, args.output)
        else:
            audit(args.report)
    except (OSError, ValueError, zipfile.BadZipFile, KeyError) as exc:
        parser.exit(1, str(exc) + "\n")


if __name__ == "__main__":
    main()
