# EMA AI — Build & Release Guide

**Last updated:** 2026-06-08

---

## Build Commands

### Revit Add-in (C#)
```powershell
# Revit 2023
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2023 /p:Platform=x64 /p:Configuration=Release

# Revit 2024
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2024 /p:Platform=x64 /p:Configuration=Release
```

**Output:** `EMAExtractor/bin/x64/Release/`

### Backend (Python/FastAPI)
```powershell
cd Pipeline\pipeline
docker compose up -d --build
```

**Output:** Docker images

### Frontend (React/TypeScript)
```powershell
cd Pipeline\pipeline\frontend
npm install
npm run build
```

**Output:** `Pipeline/pipeline/frontend/dist/`

---

## Test Commands

### Backend Tests
```powershell
cd Pipeline\pipeline
py -3.12 -m pytest tests -v
```

### Frontend TypeScript Check
```powershell
cd Pipeline\pipeline\frontend
npx tsc -b --noEmit
```

### Frontend Build
```powershell
npm run build
```

### Revit Add-in Tests
```powershell
cd EMAExtractor.Tests
dotnet test
```

---

## Installer Build

```powershell
# See scripts/install-ema-addin.ps1 for details
# The installer deploys the built add-in DLLs to the Revit Addins folder
```

**Installer output:** See `installer/` and `scripts/` directories

---

## Artifact Paths

| Artifact | Path |
|----------|------|
| Revit add-in DLLs | `EMAExtractor/bin/x64/Release/` |
| Frontend bundle | `Pipeline/pipeline/frontend/dist/` |
| Installer | `installer/` |
| Build scripts | `scripts/` |

---

## What NOT to Commit

- `artifacts/` — build output
- `*.exe`, `*.zip`, `*.log` — binaries
- `bin/`, `obj/` — .NET build artifacts
- `dist/` — frontend bundle
- `node_modules/` — dependencies
- `TestWorkflow.cs`, `TestWorkflowApp/` — test scaffolding
- `test_*.ps1` — test scripts (unless versioned)
- `installer_comand.txt` — notes
- `*.aux`, `*.fdb_latexmk`, `*.fls`, `*.out`, `*.pdf` — LaTeX build artifacts
- Real client XLSX/RVT files

---

## Release Process

1. Run backend tests (`py -3.12 -m pytest tests -v`)
2. Run frontend typecheck (`npx tsc -b --noEmit`)
3. Run frontend build (`npm run build`)
4. Build Revit add-in for target Revit year
5. Run Revit add-in tests
6. Verify installer script
7. Check git status for unintended files
8. Tag release if applicable

> Note: Build commands may not be needed during docs-only passes. Only run builds if source code has changed.
