# EMA AI Installer Release Architecture

This document describes the selected packaging direction for EMA AI.

## Selected Architecture

- **Bootstrapper:** Inno Setup EXE
- **Build Orchestrator:** PowerShell
- **Component Definitions:** JSON manifest
- **Backend Runtime:** Docker Compose
- **Database:** PostgreSQL 16 in a named Docker volume
- **Frontend Runtime:** Static assets, no Node.js on target machine
- **Optional AI:** Local Ollama only when explicitly enabled

## Release Layout

The release build will stage files into a deterministic tree similar to:

```text
release/
  manifest.json
  checksums.sha256
  sbom.json
  installer.exe
  backend/
  frontend/
  revit/
  scripts/
```

The installed layout will keep the reusable runtime under a local application
root and write the Revit manifest into the appropriate Revit add-in folder.

## Revit-Year Payload Contract

EMA AI stages the Revit add-in per installed host year. The release builder
checks `C:\Program Files\Autodesk\Revit <year>\RevitAPI.dll` before compiling
that year's payload.

| Year | Payload rule | Validation on this machine |
|---|---|---|
| 2022 | Stage `revit/2022/EMAExtractor.addin` and `revit/2022/EMA AI/` when Revit is installed | Present |
| 2023 | Stage `revit/2023/EMAExtractor.addin` and `revit/2023/EMA AI/` when Revit is installed | Present |
| 2024 | Stage `revit/2024/EMAExtractor.addin` and `revit/2024/EMA AI/` when Revit is installed | Present |
| 2025 | Stage `revit/2025/EMAExtractor.addin` and `revit/2025/EMA AI/` when Revit is installed | Present |
| 2026 | Stage `revit/2026/EMAExtractor.addin` and `revit/2026/EMA AI/` when Revit is installed | Present |
| 2027 | Stage `revit/2027/EMAExtractor.addin` and `revit/2027/EMA AI/` when Revit is installed | Present |

If a year is missing on the target machine, the installer should omit that
year's payload rather than failing the entire package.

## Health and Lifecycle

The release foundation provides PowerShell actions for:

- prerequisite detection
- component planning
- service start
- service stop
- health checks
- staged update handling
- rollback to last-known-good payloads

The target machine should not need a repository clone, Node.js dev server, or
manual `.addin` editing.

## Current Build Blocker

The source-based release foundation is staged and validated, but EXE compilation
is currently blocked on this machine because `ISCC.exe` is not installed.
Until Inno Setup is available, the release pipeline can stage files and compute
hashes, but it cannot emit the final installer binary.

## Signed Manifest Design

The manifest design uses a content hash plus release metadata before a future
publisher signature is applied.

Recommended fields:

- product name
- release channel
- semantic version
- commit SHA
- build timestamp UTC
- component list
- SHA256 hashes
- signing thumbprint
- signature algorithm
- anti-downgrade minimum version

This design intentionally separates the manifest from the payload so update
logic can verify metadata before executing any staged install.
