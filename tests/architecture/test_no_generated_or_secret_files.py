from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
FORBIDDEN_NAMES = {".env", "node_modules", "dist"}
IGNORED_DIR_NAMES = {"__pycache__", ".pytest_cache"}
FORBIDDEN_SUFFIXES = {".tsbuildinfo", ".rvt", ".dwfx", ".dump"}


def test_no_obvious_generated_or_secret_files() -> None:
    offenders: list[str] = []
    for path in ROOT.rglob("*"):
        relative = path.relative_to(ROOT)
        if ".git" in relative.parts:
            continue
        if any(part in IGNORED_DIR_NAMES for part in relative.parts):
            continue
        if path.name in FORBIDDEN_NAMES or path.suffix.lower() in FORBIDDEN_SUFFIXES:
            offenders.append(str(relative))
    assert offenders == []
