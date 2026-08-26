#!/usr/bin/env python3
"""Fail closed on tracked secrets, private identity fixtures, and high-confidence PII."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path


MAX_FILE_BYTES = 5 * 1024 * 1024
IGNORED_PARTS = {
    ".git",
    ".svelte-kit",
    "bin",
    "obj",
    "node_modules",
    "christian-think-pink-land-clearing",
}
IGNORED_TRACKED_PATHS = {
    Path("api/scripts/scan-repository-security.py"),
}
BINARY_SUFFIXES = {
    ".7z", ".avi", ".bin", ".bmp", ".class", ".dll", ".dylib", ".exe", ".gif",
    ".gz", ".ico", ".jpeg", ".jpg", ".mov", ".mp3", ".mp4", ".pdf", ".png",
    ".pdb", ".so", ".tar", ".tiff", ".ttf", ".woff", ".woff2", ".zip",
}
PLACEHOLDER_MARKERS = (
    "$(", "${", "example.com", "example.invalid", "not-a-real", "placeholder",
    "replace-me", "test-key", "your-", "0123456789abcdef",
)


RULES: tuple[tuple[str, re.Pattern[str]], ...] = (
    ("private-key", re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----")),
    ("aws-access-key", re.compile(r"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b")),
    ("github-token", re.compile(r"\bgh[pousr]_[A-Za-z0-9]{36,}\b")),
    ("slack-token", re.compile(r"\bxox[baprs]-[A-Za-z0-9-]{20,}\b")),
    ("stripe-live-secret", re.compile(r"\bsk_live_[A-Za-z0-9]{16,}\b")),
    ("openai-secret", re.compile(r"\bsk-(?:proj-)?[A-Za-z0-9_-]{32,}\b")),
    ("jwt", re.compile(r"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b")),
    ("azure-account-key", re.compile(r"AccountKey=([^;\s\"']{20,})", re.IGNORECASE)),
    ("credential-assignment", re.compile(
        r"(?:api[_-]?key|client[_-]?secret|password|signing[_-]?key|webhook[_-]?secret)"
        r"[\"']?\s*[:=]\s*[\"']([^\"'\n]{16,})[\"']",
        re.IGNORECASE,
    )),
    ("pii-email-field", re.compile(
        r"[\"'](?:primary|secondary)?email[\"']\s*:\s*[\"']([^\"']+@[^\"']+)[\"']",
        re.IGNORECASE,
    )),
    ("pii-phone-field", re.compile(
        r"[\"'](?:primary|secondary)?phone[\"']\s*:\s*[\"']([^\"']*\d[^\"']{6,})[\"']",
        re.IGNORECASE,
    )),
    ("pii-address-field", re.compile(
        r"[\"']address(?:line)?1[\"']\s*:\s*[\"'](\d{1,6}\s+[^\"']{3,})[\"']",
        re.IGNORECASE,
    )),
    ("pii-destination-field", re.compile(
        r"[\"']destination[\"']\s*:\s*[\"']([^\"']*(?:@|\d{7,})[^\"']*)[\"']",
        re.IGNORECASE,
    )),
)


def git(root: Path, *args: str) -> bytes:
    return subprocess.run(
        ["git", *args], cwd=root, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE
    ).stdout


def ignored(path: Path) -> bool:
    return any(part in IGNORED_PARTS for part in path.parts) or path.suffix.lower() in BINARY_SUFFIXES


def forbidden_name(path: Path) -> str | None:
    lowered = path.as_posix().lower()
    if ".azurite" in path.parts:
        return "tracked-local-emulator-artifact"
    if path.suffix.lower() == ".log":
        return "tracked-log-file"
    if path.name.lower() == "user.json":
        return "private-user-fixture"
    if path.name == "user-secrets.json":
        return "tracked-local-secrets"
    if path.name.startswith(".env") and path.name not in {".env.example", ".env.template"}:
        return "tracked-env-file"
    if "/.local/" in f"/{lowered}" and "secret" in path.name.lower() and ".example." not in path.name.lower():
        return "tracked-local-secrets"
    return None


def is_placeholder(value: str) -> bool:
    lowered = value.lower()
    return any(marker.lower() in lowered for marker in PLACEHOLDER_MARKERS) or "555" in lowered


def scan_text(label: str, text: str, findings: set[tuple[str, str, int]]) -> None:
    for line_number, line in enumerate(text.splitlines(), start=1):
        for rule_id, pattern in RULES:
            match = pattern.search(line)
            if match and not is_placeholder(match.group(1) if match.lastindex else match.group(0)):
                findings.add((rule_id, label, line_number))


def tracked_files(root: Path) -> list[Path]:
    return [Path(item.decode()) for item in git(root, "ls-files", "-z").split(b"\0") if item]


def scan_current(root: Path, findings: set[tuple[str, str, int]]) -> None:
    for relative in tracked_files(root):
        if relative in IGNORED_TRACKED_PATHS or ignored(relative):
            continue
        target = root / relative
        try:
            if not target.is_file():
                continue
            name_rule = forbidden_name(relative)
            if name_rule:
                findings.add((name_rule, relative.as_posix(), 0))
            if target.stat().st_size > MAX_FILE_BYTES:
                continue
            scan_text(relative.as_posix(), target.read_text(encoding="utf-8", errors="ignore"), findings)
        except OSError:
            continue


def scan_path(target: Path, findings: set[tuple[str, str, int]]) -> None:
    candidates = target.rglob("*") if target.is_dir() else [target]
    for path in candidates:
        relative = Path(*path.parts[-min(len(path.parts), 8):])
        if not path.is_file() or ignored(relative):
            continue
        label = f"artifact:{path}"
        name_rule = forbidden_name(relative)
        if name_rule:
            findings.add((name_rule, label, 0))
        try:
            if path.stat().st_size <= MAX_FILE_BYTES:
                scan_text(label, path.read_text(encoding="utf-8", errors="ignore"), findings)
        except OSError:
            continue


def scan_history(root: Path, findings: set[tuple[str, str, int]]) -> None:
    log = git(root, "log", "--all", "--format=commit:%H", "-p", "--no-color", "--", ".")
    current_file = "git-history"
    for line_number, raw in enumerate(log.decode("utf-8", errors="ignore").splitlines(), start=1):
        if raw.startswith("+++ b/") or raw.startswith("--- a/"):
            current_file = raw[6:]
            historical_path = Path(current_file)
            name_rule = forbidden_name(historical_path)
            if name_rule:
                findings.add((f"history-{name_rule}", current_file, 0))
            continue
        if raw.startswith(("+", "-")) and not raw.startswith(("+++", "---")):
            scan_text(f"history:{current_file}", raw[1:], findings)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--history", action="store_true", help="also inspect reachable Git patch history")
    parser.add_argument("--path", action="append", default=[], help="also inspect a build or publish directory")
    args = parser.parse_args()
    root = Path(git(Path.cwd(), "rev-parse", "--show-toplevel").decode().strip())
    findings: set[tuple[str, str, int]] = set()
    scan_current(root, findings)
    for requested_path in args.path:
        scan_path(Path(requested_path).resolve(), findings)
    if args.history:
        scan_history(root, findings)

    if findings:
        print(f"security scan failed with {len(findings)} finding(s); values are intentionally suppressed")
        for rule_id, path, line in sorted(findings):
            suffix = f":{line}" if line else ""
            print(f"- {rule_id}: {path}{suffix}")
        return 1

    scopes = ["tracked tree"]
    if args.path:
        scopes.append("requested artifacts")
    if args.history:
        scopes.append("reachable history")
    scope = ", ".join(scopes)
    print(f"security scan passed: {scope}; no configured high-confidence secrets or PII found")
    return 0


if __name__ == "__main__":
    sys.exit(main())
