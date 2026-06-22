<#
.SYNOPSIS
  Run the AEC Control Plane test suites.

.DESCRIPTION
  Default mode (-Fast):
    Fast suite only (unit/contract/conformance/architecture).
    No database required; safe for a fresh clone / no-Docker CI.

  Modes (specify exactly one):
    -Fast       : pytest with default deselect of integration markers (default).
    -Api        : migrated EMA integration tests (requires PostgreSQL up).
    -Frontend   : frontend type-check + build (requires node_modules).
    -DotNet     : .NET Revit add-in tests (requires dotnet SDK).
    -Full       : everything (fast + api + frontend + dotnet).

  Without any mode switch the default is -Fast.

.EXAMPLE
  pwsh .\scripts\test.ps1            # fast suite (default)
  pwsh .\scripts\test.ps1 -Fast      # explicit fast suite
  pwsh .\scripts\test.ps1 -Api       # API integration tests only
  pwsh .\scripts\test.ps1 -Frontend  # frontend type-check only
  pwsh .\scripts\test.ps1 -Full      # all test suites
#>
param(
    [switch]$Fast,
    [switch]$Api,
    [switch]$Frontend,
    [switch]$DotNet,
    [switch]$Full
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."

# Determine mode. If no switch given, default to -Fast.
$mode = if ($Full) { "full" }
    elseif ($Api) { "api" }
    elseif ($Frontend) { "frontend" }
    elseif ($DotNet) { "dotnet" }
    else { "fast" }

function Write-Section($m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Write-Step($m) { Write-Host "  [$m]" -ForegroundColor Yellow }

$exitCode = 0
$results = @()

Push-Location $root
try {
    # ----------------------------------------------------------- Fast (Python)
    if ($mode -in @("fast", "full")) {
        Write-Section "Fast: Python unit / contract / conformance / architecture"
        Write-Step "pytest (default: not integration)"
        python -m pytest -q @args
        $results += @{ suite = "fast"; status = if ($LASTEXITCODE -eq 0) { "PASS" } else { "FAIL" }; code = $LASTEXITCODE }
        if ($LASTEXITCODE -ne 0 -and $exitCode -eq 0) { $exitCode = $LASTEXITCODE }
    }

    # ------------------------------------------------------------- API (Python)
    if ($mode -in @("api", "full")) {
        Write-Section "API: Migrated EMA integration tests"
        Write-Step "pytest -m integration (requires Postgres)"
        python -m pytest -m integration -q @args
        $results += @{ suite = "api"; status = if ($LASTEXITCODE -eq 0) { "PASS" } else { "FAIL" }; code = $LASTEXITCODE }
        if ($LASTEXITCODE -ne 0 -and $exitCode -eq 0) { $exitCode = $LASTEXITCODE }
    }

    # --------------------------------------------------------- Frontend (TypeScript)
    if ($mode -in @("frontend", "full")) {
        Write-Section "Frontend: TypeScript type-check + build"
        $frontendDir = Join-Path $root "apps/web-console"
        if (-not (Test-Path (Join-Path $frontendDir "node_modules"))) {
            Write-Step "SKIPPED (node_modules not found — run 'pnpm install' first)"
            $results += @{ suite = "frontend"; status = "SKIP"; code = 0 }
        }
        else {
            Push-Location $frontendDir
            try {
                Write-Step "tsc --noEmit (type-check)"
                npx tsc --noEmit 2>&1
                $tsExit = $LASTEXITCODE
                if ($tsExit -eq 0) { Write-Step "tsc passed" } else { Write-Step "tsc FAILED" }

                Write-Step "vite build"
                npx vite build 2>&1
                $viteExit = $LASTEXITCODE
                if ($viteExit -eq 0) { Write-Step "vite build passed" } else { Write-Step "vite build FAILED" }

                $feExit = if ($tsExit -ne 0) { $tsExit } elseif ($viteExit -ne 0) { $viteExit } else { 0 }
                $results += @{ suite = "frontend"; status = if ($feExit -eq 0) { "PASS" } else { "FAIL" }; code = $feExit }
                if ($feExit -ne 0 -and $exitCode -eq 0) { $exitCode = $feExit }
            }
            finally { Pop-Location }
        }
    }

    # ----------------------------------------------------------- .NET (Revit add-in)
    if ($mode -in @("dotnet", "full")) {
        Write-Section "DotNet: Revit add-in tests"
        $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
        if (-not $dotnet) {
            Write-Step "SKIPPED (dotnet SDK not found)"
            $results += @{ suite = "dotnet"; status = "SKIP"; code = 0 }
        }
        else {
            $testProjs = @(
                "apps/revit-addin/EMAExtractor.Tests/EMAExtractor.Tests.csproj",
                "apps/revit-addin/EMAExtractor.Analysis.Tests/EMAExtractor.Analysis.Tests.csproj"
            )
            $dnExit = 0
            foreach ($proj in $testProjs) {
                $projPath = Join-Path $root $proj
                if (Test-Path $projPath) {
                    Write-Step "dotnet test $proj"
                    Push-Location (Split-Path $projPath -Parent)
                    try {
                        dotnet test --no-restore 2>&1
                        if ($LASTEXITCODE -ne 0 -and $dnExit -eq 0) { $dnExit = $LASTEXITCODE }
                    }
                    finally { Pop-Location }
                }
                else {
                    Write-Step "SKIPPED $proj (file not found)"
                }
            }
            $results += @{ suite = "dotnet"; status = if ($dnExit -eq 0) { "PASS" } else { "FAIL" }; code = $dnExit }
            if ($dnExit -ne 0 -and $exitCode -eq 0) { $exitCode = $dnExit }
        }
    }

    # ----------------------------------------------------------- Summary
    Write-Section "Test results"
    $totalPass = 0
    $totalFail = 0
    $totalSkip = 0
    foreach ($r in $results) {
        $color = switch ($r.status) {
            "PASS" { "Green" }
            "FAIL" { "Red" }
            "SKIP" { "Yellow" }
        }
        Write-Host "  $($r.suite.PadRight(12)) $($r.status)" -ForegroundColor $color
        switch ($r.status) {
            "PASS" { $totalPass++ }
            "FAIL" { $totalFail++ }
            "SKIP" { $totalSkip++ }
        }
    }
    Write-Host ""
    Write-Host "  Summary: $totalPass passed, $totalFail failed, $totalSkip skipped" -ForegroundColor White

    exit $exitCode
}
finally {
    Pop-Location
}
