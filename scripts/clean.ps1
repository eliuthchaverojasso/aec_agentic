<#
.SYNOPSIS
  Clean up local development artifacts and restore a clean state.

.DESCRIPTION
  Removes:
    - Docker Compose containers + volumes (DB data)
    - Python __pycache__ directories and .pyc files
    - Python .pytest_cache and .ruff_cache
    - node_modules directories
    - vite/tsbuildinfo output
    - .env file (optional, use -RemoveEnv)

  Does NOT remove:
    - Docker images (cache stays warm)
    - Git-tracked files
    - pip-installed packages

.PARAMETER RemoveEnv
  Also delete the .env file (re-created by bootstrap.ps1).

.PARAMETER RemoveVolumes
  Also delete Docker volumes (use with caution — destroys all local DB data).

.EXAMPLE
  pwsh .\scripts\clean.ps1
  pwsh .\scripts\clean.ps1 -RemoveVolumes
  pwsh .\scripts\clean.ps1 -RemoveEnv -RemoveVolumes
#>
param(
    [switch]$RemoveEnv,
    [switch]$RemoveVolumes
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

function Write-Section($m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Write-Ok($m)       { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Write-Warn2($m)    { Write-Host "  [warn] $m" -ForegroundColor Yellow }

Push-Location $root
try {
    Write-Section "Docker"
    if ($RemoveVolumes) {
        Write-Warn2 "Stopping all containers and removing volumes ..."
        docker compose down -v 2>$null
        Write-Ok "containers stopped, volumes removed"
    }
    else {
        docker compose down 2>$null
        Write-Ok "containers stopped (volumes preserved; use -RemoveVolumes to destroy DB data)"
    }

    Write-Section "Python cache"
    Get-ChildItem -Path "." -Directory -Filter "__pycache__" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
    Get-ChildItem -Path "." -Filter "*.pyc" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
    }
    @(".pytest_cache", ".ruff_cache", ".mypy_cache", ".coverage", "htmlcov") | ForEach-Object {
        $target = Join-Path $root $_
        if (Test-Path $target) { Remove-Item -Path $target -Recurse -Force -ErrorAction SilentlyContinue; Write-Ok "removed $_" }
    }
    Write-Ok "Python cache cleaned"

    Write-Section "Node / frontend"
    Get-ChildItem -Path "." -Directory -Filter "node_modules" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Ok "removed $($_.FullName)"
    }
    @("dist", ".vite", "tsconfig.tsbuildinfo") | ForEach-Object {
        Get-ChildItem -Path "." -Directory -Filter $_ -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
            Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            Write-Ok "removed $($_.FullName)"
        }
    }

    Write-Section "Docker build artifacts"
    $artifacts = @(
        "apps/control-plane-api/__pycache__"
    )
    foreach ($a in $artifacts) {
        $target = Join-Path $root $a
        if (Test-Path $target) { Remove-Item -Path $target -Recurse -Force -ErrorAction SilentlyContinue }
    }

    if ($RemoveEnv) {
        Write-Section "Environment file"
        $envFile = Join-Path $root ".env"
        if (Test-Path $envFile) {
            Remove-Item -Path $envFile -Force
            Write-Ok ".env deleted"
        }
    }

    Write-Section "Clean complete"
    Write-Host "To restore:  pwsh .\scripts\bootstrap.ps1" -ForegroundColor White
}
finally {
    Pop-Location
}
