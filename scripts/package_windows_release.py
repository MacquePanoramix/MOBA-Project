#!/usr/bin/env python3
"""
Package prototype/Builds/WindowsLane into WoodlandLane-Windows.zip and emit matching latest.json.
Verifies archive integrity (testzip, root WoodlandLane.exe, version.txt) and prints size & SHA-256.
"""

import argparse
import datetime
import hashlib
import json
import os
import pathlib
import re
import zipfile


EXCLUDED_DIR_MARKERS = ("_DoNotShip", "_BackUpThisFolder_")


def compute_sha256(path: pathlib.Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 256), b""):
            h.update(chunk)
    return h.hexdigest().lower()


def main() -> int:
    parser = argparse.ArgumentParser(description="Package WoodlandLane Windows release ZIP and manifest.")
    parser.add_argument("--build-dir", default="prototype/Builds/WindowsLane")
    parser.add_argument("--out-zip", default="prototype/Builds/WoodlandLane-Windows.zip")
    parser.add_argument("--out-manifest", default="prototype/Builds/latest.json")
    parser.add_argument("--owner", default="MacquePanoramix")
    parser.add_argument("--repo", default="MOBA-Project")
    args = parser.parse_args()

    build_dir = pathlib.Path(args.build_dir).resolve()
    out_zip = pathlib.Path(args.out_zip).resolve()
    out_manifest = pathlib.Path(args.out_manifest).resolve()

    exe_file = build_dir / "WoodlandLane.exe"
    version_file = build_dir / "version.txt"
    if not exe_file.is_file():
        raise SystemExit(f"Missing Windows executable: {exe_file}")
    if not version_file.is_file():
        raise SystemExit(f"Missing version.txt: {version_file}")

    version = version_file.read_text(encoding="utf-8").strip()
    if not re.match(r"^v?\d+(?:\.\d+)*$", version):
        raise SystemExit(f"Invalid version.txt value: '{version}'")

    out_zip.parent.mkdir(parents=True, exist_ok=True)
    tmp_zip = out_zip.with_suffix(".zip.tmp")
    if tmp_zip.exists():
        tmp_zip.unlink()

    with zipfile.ZipFile(tmp_zip, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as zf:
        for root, dirs, files in os.walk(build_dir):
            dirs[:] = sorted(
                d for d in dirs if not any(marker in d for marker in EXCLUDED_DIR_MARKERS)
            )
            for fn in sorted(files):
                if fn.endswith(".pdb"):
                    continue
                full = pathlib.Path(root) / fn
                rel = full.relative_to(build_dir).as_posix()
                zf.write(full, rel)

    os.replace(tmp_zip, out_zip)

    with zipfile.ZipFile(out_zip, "r") as zf:
        bad = zf.testzip()
        if bad is not None:
            raise SystemExit(f"Zip integrity check failed at {bad}")
        names = zf.namelist()
        if "WoodlandLane.exe" not in names or "version.txt" not in names:
            raise SystemExit("Packed archive missing root WoodlandLane.exe or version.txt")
        embedded_ver = zf.read("version.txt").decode("utf-8").strip()
        if embedded_ver != version:
            raise SystemExit(f"Embedded version '{embedded_ver}' mismatch with '{version}'")
        entry_count = len(names)

    size_bytes = out_zip.stat().st_size
    sha256 = compute_sha256(out_zip)

    manifest = {
        "schemaVersion": 1,
        "version": version,
        "assetName": out_zip.name,
        "downloadUrl": f"https://github.com/{args.owner}/{args.repo}/releases/download/{version}/{out_zip.name}",
        "releaseUrl": f"https://github.com/{args.owner}/{args.repo}/releases/tag/{version}",
        "sizeBytes": size_bytes,
        "sha256": sha256,
        "publishedAtUtc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "notes": f"Ultimate Free MOBA {version}",
    }
    out_manifest.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print(
        f"PACKAGED version={version} entries={entry_count} bytes={size_bytes} "
        f"mb={size_bytes / (1024 * 1024):.2f} sha256={sha256} zip={out_zip} manifest={out_manifest}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
