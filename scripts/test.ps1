<#
.SYNOPSIS
  Run the AEC Control Plane test suites.

.DESCRIPTION
  Default            : fast suite only (unit/contract/conformance/architecture).
                       No database required; safe for a fresh clone / no-Docker CI.
  -Integration       : only the migrated EMA suite (requires PostgreSQL up).
  -All               : every test (fast + integration; requires PostgreSQL up).

  Test discovery, paths, and the default `-m "not integration"` deselect live in
  pyproject.toml, so a bare `pytest` behaves the same as this script's default.

.EXAMPLE
  pwsh .\scripts\test.ps1
  pwsh .\scripts\test.ps1 -Integration
  pwsh .\scripts\test.ps1 -All
#>
param(
    [switch]$Integration,
    [switch]$All
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
Push-Location $root
try {
    if ($All) {
        # Override the default deselect in pyproject's addopts to run everything.
        python -m pytest -o "addopts=--strict-markers" -q @args
    }
    elseif ($Integration) {
        python -m pytest -m integration -q @args
    }
    else {
        python -m pytest -q @args
    }
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
