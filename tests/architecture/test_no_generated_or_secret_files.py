from __future__ import annotations

import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

# Things that must never be *committed*. They may (and after bootstrap/build will)
# exist locally as gitignored files — the rule is about what is tracked in Git,
# not what is present in the working tree.
FORBIDDEN_FILE_NAMES = {".env"}
FORBIDDEN_DIR_NAMES = {"node_modules", "dist"}
FORBIDDEN_SUFFIXES = {".tsbuildinfo", ".rvt", ".dwfx", ".dump"}


def _git_tracked_files() -> list[str]:
    result = subprocess.run(
        ["git", "ls-files"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    )
    return [line for line in result.stdout.splitlines() if line]


def test_no_obvious_generated_or_secret_files() -> None:
    """No generated or secret files may be tracked in Git.

    Checks git-tracked files (not the working tree): a developer's local,
    gitignored ``.env`` / ``node_modules`` / ``dist`` are expected to exist after
    bootstrap or a build — they simply must never be committed. ``.env.example``
    is allowed because the forbidden name is exactly ``.env``.
    """
    offenders: list[str] = []
    for rel in _git_tracked_files():
        parts = rel.split("/")
        name = parts[-1]
        if name in FORBIDDEN_FILE_NAMES:
            offenders.append(rel)
        elif any(part in FORBIDDEN_DIR_NAMES for part in parts):
            offenders.append(rel)
        elif Path(name).suffix.lower() in FORBIDDEN_SUFFIXES:
            offenders.append(rel)

    assert offenders == [], f"Forbidden files are tracked in Git: {offenders}"
