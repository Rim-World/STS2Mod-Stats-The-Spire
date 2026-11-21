#!/usr/bin/env python3
"""Release compliance checks for Stats the Spire.

Checks version consistency, tracked-file hygiene, and release artifact privacy.
Exit code 0 means all checks passed. Exit code 1 means at least one check failed.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "9.SRC"

FORBIDDEN_TRACKED = [
    (re.compile(r"^1\.PRD/用户prompt/"), "user prompt must stay untracked"),
    (re.compile(r"^TMP/"), "TMP must stay untracked"),
    (re.compile(r"^PRIVACY/"), "PRIVACY must stay untracked"),
    (re.compile(r"^8\.TEST/.*\.(log|stdout|stderr)$"), "test logs must stay untracked"),
    (re.compile(r"^8\.TEST/.*/deploy/"), "test deployments must stay untracked"),
    (re.compile(r"/(bin|obj)/"), "build intermediates must stay untracked"),
]

SECRET_PATTERNS = [
    (re.compile(r"-----BEGIN (?:RSA |OPENSSH |EC |DSA )?PRIVATE KEY-----"), "private key block"),
    (re.compile(r"\bghp_[A-Za-z0-9]{36}\b"), "GitHub personal access token"),
    (re.compile(r"\bgithub_pat_[A-Za-z0-9_]{20,}\b"), "GitHub fine-grained token"),
    (re.compile(r"\bAKIA[0-9A-Z]{16}\b"), "AWS access key id"),
    (re.compile(r"\bxox[baprs]-[A-Za-z0-9-]{10,}\b"), "Slack token"),
]

USER_PATH_PATTERNS = [
    re.compile(rb"C:\\Users\\", re.IGNORECASE),
    re.compile(rb"C:/Users/", re.IGNORECASE),
    re.compile(rb"Desktop\\Mod", re.IGNORECASE),
    re.compile(rb"Desktop/Mod", re.IGNORECASE),
    re.compile(rb"obj\\Debug", re.IGNORECASE),
    re.compile(rb"obj/Debug", re.IGNORECASE),
]

failures: list[str] = []
passes: list[str] = []


def ok(message: str) -> None:
    passes.append(message)
    print(f"PASS {message}")


def fail(message: str) -> None:
    failures.append(message)
    print(f"FAIL {message}")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def git_lines(*args: str) -> list[str]:
    proc = subprocess.run(
        ["git", *args],
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if proc.returncode != 0:
        fail(f"git {' '.join(args)}: {proc.stderr.strip()}")
        return []
    return [line for line in proc.stdout.splitlines() if line]


def detect_version() -> str:
    manifest = json.loads(read_text(SRC / "manifest.json"))
    version = str(manifest.get("version", "")).strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        fail(f"manifest version is not semver: {version!r}")
        return ""
    ok(f"manifest version {version}")
    return version


def check_version_sources(version: str) -> None:
    if not version:
        return

    csproj = read_text(SRC / "sts2_statsthespire_local.csproj")
    for tag in ("Version", "AssemblyVersion"):
        match = re.search(rf"<{tag}>([^<]+)</{tag}>", csproj)
        if not match:
            fail(f"csproj missing <{tag}>")
        elif tag == "AssemblyVersion":
            if match.group(1) != f"{version}.0":
                fail(f"csproj AssemblyVersion {match.group(1)} != {version}.0")
            else:
                ok(f"csproj AssemblyVersion {match.group(1)}")
        elif match.group(1) != version:
            fail(f"csproj Version {match.group(1)} != {version}")
        else:
            ok(f"csproj Version {match.group(1)}")

    config = read_text(SRC / "src" / "Config" / "ModConfig.cs")
    match = re.search(r'ModVersion\s*=\s*"([^"]+)"', config)
    if not match:
        fail("ModConfig.ModVersion not found")
    elif match.group(1) != version:
        fail(f"ModConfig.ModVersion {match.group(1)} != {version}")
    else:
        ok(f"ModConfig.ModVersion {match.group(1)}")

    changelog = read_text(SRC / "CHANGELOG.md")
    if f"## [{version}]" not in changelog:
        fail(f"CHANGELOG.md missing heading for {version}")
    else:
        ok(f"CHANGELOG.md heading {version}")


def check_tracked_files() -> None:
    tracked = git_lines("ls-files")
    if not tracked:
        fail("git ls-files returned no files")
        return
    tracked = [p for p in tracked if p]
    bad = []
    for path in tracked:
        normalized = path.replace("\\", "/")
        for pattern, reason in FORBIDDEN_TRACKED:
            if pattern.search(normalized):
                bad.append(f"{path} ({reason})")
                break
    if bad:
        for item in bad[:20]:
            fail(f"forbidden tracked file: {item}")
    else:
        ok(f"tracked files clean ({len(tracked)} files)")

    gitignore = read_text(ROOT / ".gitignore")
    required = [
        "/1.PRD/用户prompt/",
        "/TMP/",
        "/8.TEST/**/*.log",
        "/8.TEST/**/*.stdout",
        "/8.TEST/**/*.stderr",
    ]
    missing = [entry for entry in required if entry not in gitignore]
    for entry in missing:
        fail(f".gitignore missing {entry}")
    if not missing:
        ok(".gitignore has prompt, TMP, and test-log exclusions")


def check_secrets() -> None:
    tracked = [p for p in git_lines("ls-files") if p]
    hits = []
    for path in tracked:
        full = ROOT / path
        if not full.is_file():
            continue
        try:
            data = full.read_bytes()
        except OSError as exc:
            fail(f"cannot read {path}: {exc}")
            continue
        if b"\x00" in data[:4096]:
            continue
        text = data.decode("utf-8-sig", errors="replace")
        for pattern, label in SECRET_PATTERNS:
            if pattern.search(text):
                hits.append(f"{path}: {label}")
    if hits:
        for hit in hits[:20]:
            fail(f"secret-like content: {hit}")
    else:
        ok("no high-signal secret patterns in tracked text")


def check_source_paths() -> None:
    hits = []
    for path in sorted((SRC / "src").rglob("*.cs")):
        text = read_text(path)
        for pattern in (r"C:\\Users\\", r"C:/Users/", r"Desktop[\\/]Mod"):
            if re.search(pattern, text):
                hits.append(str(path.relative_to(ROOT)))
                break
    if hits:
        for hit in hits:
            fail(f"machine path in source: {hit}")
    else:
        ok("no machine paths in source (.cs)")


def check_output(version: str, output_arg: str | None) -> None:
    if not version:
        return
    output = Path(output_arg) if output_arg else ROOT / "10.OUTPUT" / version
    if not output.is_absolute():
        output = (ROOT / output).resolve()
    if not output.is_dir():
        fail(f"output directory missing: {output}")
        return

    required = [
        "manifest.json",
        "settings.cfg",
        "sts2_statsthespire_local.dll",
        "README_ZH.md",
        "README_EN.md",
    ]
    for name in required:
        if not (output / name).is_file():
            fail(f"output missing {name}")
    if any(f.startswith("output missing") for f in failures):
        return

    manifest = json.loads(read_text(output / "manifest.json"))
    if str(manifest.get("version")) != version:
        fail(f"output manifest version {manifest.get('version')} != {version}")
    else:
        ok(f"output manifest version {version}")

    dll = output / "sts2_statsthespire_local.dll"
    data = dll.read_bytes()
    hits = [label for pattern in USER_PATH_PATTERNS for label in [pattern.pattern.decode("latin1")] if pattern.search(data)]
    # .NET user strings are UTF-16LE; check both encodings.
    utf16_hits = []
    for pattern in USER_PATH_PATTERNS:
        ascii_text = pattern.pattern.decode("latin1")
        encoded = ascii_text.replace("\\\\", "\\")
        if encoded.encode("utf-16-le", errors="ignore") in data:
            utf16_hits.append(encoded)
    if hits or utf16_hits:
        fail(f"machine path in DLL: {sorted(set(hits + utf16_hits))}")
    else:
        ok("no machine path in release DLL")

    readme_zh = read_text(output / "README_ZH.md")
    readme_en = read_text(output / "README_EN.md")
    if "许可证与授权" not in readme_zh:
        fail("output README_ZH.md missing license section")
    if "License and authorization" not in readme_en:
        fail("output README_EN.md missing license section")
    if "许可证与授权" in readme_zh and "License and authorization" in readme_en:
        ok("output READMEs record license status")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", help="release output directory; defaults to 10.OUTPUT/<version>")
    args = parser.parse_args()

    version = detect_version()
    check_version_sources(version)
    check_tracked_files()
    check_secrets()
    check_source_paths()
    check_output(version, args.output)

    print()
    print(f"passes={len(passes)} failures={len(failures)}")
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
