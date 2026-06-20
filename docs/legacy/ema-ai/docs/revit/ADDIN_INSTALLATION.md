# EMA AI Revit Add-in Installation

This repository already includes installer scripts:

- `scripts/install-ema-addin.ps1`
- `scripts/uninstall-ema-addin.ps1`

These scripts install per-user by default under `%APPDATA%` and do not install to `ProgramData` unless explicitly requested.

## Build First

From repo root:

```powershell
dotnet msbuild EMAExtractor\EMAExtractor.csproj /p:Configuration=Debug /p:Platform=x64
```

## Install

Install for explicit Revit versions:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -RevitYears 2024,2025,2026 -BuildFirst
```

Also supported (space-delimited years):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -RevitYears 2024 2025 2026 -BuildFirst
```

Detect installed Revit versions and install to all detected:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -InstallAllKnownVersions -BuildFirst
```

Dry run / what-if preview:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -RevitYears 2024 -WhatIf
```

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-ema-addin.ps1 -RevitYears 2024,2025,2026
```

Dry run / what-if preview:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-ema-addin.ps1 -RevitYears 2024 -WhatIf
```

## Install Paths

For each version, the installer writes:

- Manifest:
  `%APPDATA%\Autodesk\Revit\Addins\<version>\EMAExtractor.addin`
- Payload folder:
  `%APPDATA%\Autodesk\Revit\Addins\<version>\EMA AI\`

The manifest registers:

- `Type="Application"`
- `Name="EMA AI"`
- `Assembly=<full path to EMAExtractor.dll>`
- `FullClassName=EMAExtractor.App`
- `VendorId=EMA`
- `VendorDescription=EMA AI Engineering`

## Verify in Revit

1. Close all Revit instances before install.
2. Run installer.
3. Open Revit (matching installed year).
4. Confirm the **EMA AI** tab appears.
5. Open add-in settings and confirm API/dashboard URLs as needed.

## Updating The Add-in

Recommended update flow:

1. Close Revit.
2. Pull latest branch changes.
3. Run a dry-run update first:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -Scope User -RevitYears 2026 -BuildFirst -DryRun
```

4. Run actual update:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -Scope User -RevitYears 2026 -BuildFirst
```

5. Open Revit and verify the EMA AI tab.

Accepted multi-version syntax:

```powershell
# Comma-delimited
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -Scope User -RevitYears 2023,2024,2025,2026 -BuildFirst

# Space-delimited
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 -Scope User -RevitYears 2023 2024 2025 2026 -BuildFirst
```

Optional wrapper:

```powershell
.\scripts\update-ema-addin.ps1 -RevitYears 2026
```

## Landing Folder Behavior

The add-in can export to project landing structure when configured:

`<LandingRoot>\<ProjectFolder>\Revit Exports\`

Related landing folders are created by the add-in workflow (when configured), including Drawings, Specifications, Owner Requirements, and Revit Exports.

## Troubleshooting

- If installer reports missing DLL, run with `-BuildFirst` or run the msbuild command manually first.
- If install is skipped, close Revit and rerun.
- If no versions are detected, pass explicit `-RevitYears`.
- If tab does not appear, verify the `.addin` file points to the actual `EMAExtractor.dll` path.
- If you see `ElementId.IntegerValue` compile failures for Revit 2026, use the latest branch with the compatibility fix (`ElementId.Value` for newer Revit versions via helper).
- If a target Revit install directory is missing, pass the correct `-RevitYears` value or verify `C:\Program Files\Autodesk\Revit <year>`.

## Known Limitation

Backend ingest remains operator-controlled from the web app via **Dev Mode** or **Processing / Sync**. Installer only deploys the Revit add-in.

## Known Build Warnings

MSB3277 assembly conflict warnings can appear due to Autodesk Revit API and .NET assembly resolution differences. If build output shows **0 errors**, warnings alone are not considered a build blocker for this workflow.
