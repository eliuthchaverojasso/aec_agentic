$ErrorActionPreference = "Stop"

Write-Host "AEC Agentic Control Plane"
Write-Host "Workspace: $PSScriptRoot\.."
Write-Host ""
Write-Host "Migrated components:"
Write-Host "  API:          apps/control-plane-api"
Write-Host "  Web console:  apps/web-console"
Write-Host "  Revit add-in: apps/revit-addin"
Write-Host ""
Write-Host "Next:"
Write-Host "  pwsh ./scripts/test.ps1"

