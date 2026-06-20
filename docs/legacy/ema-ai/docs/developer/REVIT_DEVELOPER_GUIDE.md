# Revit Developer Guide

## Layout
- Project: `EMAExtractor/`
- Entry: `App.cs`
- Export path: `Core/ExportRunner.cs`, `Core/ExportUtils.cs`
- Landing folders: `Services/LandingStandardService.cs`
- Install/update scripts: `scripts/install-ema-addin.ps1`, `scripts/update-ema-addin.ps1`

## Compatibility Note
- Revit 2026 requires `ElementId.Value` handling path; compatibility helper is used for cross-year builds.

## Validation
- Build targeted years as needed.
- Runtime claims require actual Revit host smoke run.
