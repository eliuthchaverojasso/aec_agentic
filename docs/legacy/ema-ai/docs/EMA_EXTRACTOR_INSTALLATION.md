# EMAExtractor Installation & Deployment Runbook

## 1. Purpose
The `EMAExtractor` is the Revit Add-in component of the EMA AI platform. Its primary purpose is to export model data into a standardized **Landing Zone** architecture, producing JSON exports and corresponding `.meta.json` metadata files that allow for deterministic backend ingestion and readiness scoring.

## 2. Prerequisites
- **Revit installed** (2024 recommended for this version).
- **.NET SDK** (for building from source).
- **PowerShell 5.1+** with execution policy allowing scripts.
- **Administrator privileges** (required for `Scope AllUsers` installations).

## 3. Folder Structure
The installer manages files in the following locations:
- **User Scope:** `%AppData%\Autodesk\Revit\Addins\<Year>\EMAExtractor.addin`
- **AllUsers Scope:** `C:\ProgramData\Autodesk\Revit\Addins\<Year>\EMAExtractor.addin`
- **Binaries:** Typically deployed to a dedicated `EMA AI` folder within the scope root.

## 4. Build Validation
Before installation, ensure the binaries are correctly compiled and target the specific Revit version.

### Build Command
```powershell
dotnet restore EMAExtractor\EMAExtractor.csproj

dotnet build EMAExtractor\EMAExtractor.csproj `
  --configuration Debug `
  -p:Platform=x64 `
  -p:RevitYear=2024 `
  -p:RevitInstallDir="C:\Program Files\Autodesk\Revit 2024"
```

## 5. Installation Instructions

### 5.1 Install User Scope (Revit 2024)
Installs the add-in only for the current Windows user.
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 `
  -Scope User `
  -RevitYears 2024 `
  -BuildFirst
```

### 5.2 Install User Scope (All Detected Versions)
Scans the system for installed Revit versions and deploys the add-in to all of them.
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 `
  -Scope User `
  -InstallAllKnownVersions `
  -BuildFirst
```

### 5.3 Install AllUsers Scope
Deploys the add-in for every user on the machine. Requires Administrator privileges.
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 `
  -Scope AllUsers `
  -InstallAllKnownVersions `
  -BuildFirst
```

## 6. Advanced Installer Options

### 6.1 DryRun (Validation)
Use this to verify what the installer *would* do (paths, versions, etc.) without actually modifying the filesystem.
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-ema-addin.ps1 `
  -Scope User `
  -RevitYears 2024 `
  -DryRun
```

### 6.2 BuildFirst
Adding the `-BuildFirst` flag ensures that the latest source code is compiled into the staging area before the installation process begins.

## 7. Uninstallation
To remove the EMAExtractor add-in and its associated manifest files:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall-ema-addin.ps1 `
  -Scope User `
  -RevitYears 2024
```

## 8. Manual Revit Validation Checklist
After installation, perform the following checks to ensure functionality:
- [ ] **Close Revit** completely before running the installer.
- [ ] **Run installer** using one of the methods above.
- [ ] **Open Revit 2024**.
- [ ] **Confirm Ribbon:** The EMA AI tab appears in the top ribbon.
- [ ] **Confirm Panels:** The following panels are visible:
    - Project
    - Export
    - Data / Sync
    - Document Intake
- [ ] **Confirm AI State:** The AI Query panel explicitly states that AI Query is **Deferred**.
- [ ] **Configure Landing:** Set a valid **Landing Root** and **Project Folder**.
- [ ] **Test Export:** Execute a discipline export.
- [ ] **Verify Output:**
    - [ ] Confirm a `.json` export file was created.
    - [ ] Confirm a `.meta.json` file was created in the same directory.
- [ ] **Verify Workflow:** Confirm that backend ingestion remains a manual step (as per MVP).

## 9. Troubleshooting
- **Missing DLLs:** Ensure the `RevitInstallDir` parameter in the build command matches your actual Revit installation path.
- **Permission Errors:** If installing for `AllUsers`, ensure your PowerShell terminal is running as **Administrator**.
- **Manifest Not Found:** Verify the `.addin` file exists in the Autodesk Addins folder for the specified Revit year.

## 10. Safety Notes
- **Installer Boundaries:** The installer is designed to touch only the `.addin` manifest and the `EMA AI` binary folder. It will not modify Revit system files or other third-party plugins.
- **Configuration:** Settings are stored in local configuration files and are not overwritten by the installer.

## 11. Deployment Workflows

### 11.1 Recommended Pilot Workflow (Developer/Small Group)
1. Build locally.
2. Use `Scope User` for quick iteration.
3. Use `DryRun` to validate paths before final deployment.
4. Validate using the Manual Checklist.

### 11.2 Recommended IT Deployment Workflow (Enterprise)
1. Build a stable release in `Release` configuration.
2. Package binaries into a deployable share.
3. Use `Scope AllUsers` via a GPO or centralized deployment script (e.g., SCCM/Intune).
4. Specify explicit `-RevitYears` to ensure compatibility across different department standards.
