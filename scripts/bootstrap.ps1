<#
.SYNOPSIS
  Bootstrap the AEC Agentic Control Plane local development environment.

.DESCRIPTION
  Idempotent and safe to re-run. Performs:
    1. Prerequisite checks (python + docker required; node/.NET optional).
    2. .env generation from .env.example (only if missing).
    3. PostgreSQL startup via docker compose, with the EMA schema auto-loaded
       from infra/database on a fresh volume.
    4. Wait for the database to become healthy.
    5. Verify the schema loaded.

  After this completes:  pwsh .\scripts\test.ps1 -All   should be green.

.PARAMETER Clean
  Tear down the Postgres volume first (DROPS all local database data) so the
  schema is re-loaded from scratch.

.PARAMETER NoDocker
  Skip Docker/Postgres entirely. Only checks prerequisites and generates .env.
  Use when you point DATABASE_URL at an external Postgres that already has the
  EMA schema.

.EXAMPLE
  pwsh .\scripts\bootstrap.ps1
  pwsh .\scripts\bootstrap.ps1 -Clean
  pwsh .\scripts\bootstrap.ps1 -NoDocker
#>
param(
    [switch]$Clean,
    [switch]$NoDocker
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

function Write-Section($m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Write-Ok($m)       { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Write-Warn2($m)    { Write-Host "  [warn] $m" -ForegroundColor Yellow }
function Write-Fail($m)     { Write-Host "  [FAIL] $m" -ForegroundColor Red }

Push-Location $root
try {
    # ---------------------------------------------------------------- 1. prereqs
    Write-Section "Prerequisites"
    $missing = @()

    $py = Get-Command python -ErrorAction SilentlyContinue
    if ($py) { Write-Ok "python  $((python --version 2>&1) -replace 'Python ','')" }
    else     { Write-Fail "python not found (required)"; $missing += "python" }

    if (-not $NoDocker) {
        $docker = Get-Command docker -ErrorAction SilentlyContinue
        if ($docker) {
            docker info *> $null
            if ($LASTEXITCODE -eq 0) { Write-Ok "docker  daemon reachable" }
            else { Write-Fail "docker is installed but the daemon is not reachable (start Docker Desktop)"; $missing += "docker-daemon" }
        }
        else { Write-Fail "docker not found (required; or re-run with -NoDocker)"; $missing += "docker" }
    }

    # Optional toolchains — warn only; not needed for API + tests.
    $node = Get-Command node -ErrorAction SilentlyContinue
    if ($node) { Write-Ok "node    $(node --version)" } else { Write-Warn2 "node not found (needed for web-console only)" }
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) { Write-Ok "dotnet  $(dotnet --version)" } else { Write-Warn2 "dotnet not found (needed for Revit add-in only)" }

    if ($missing.Count -gt 0) {
        throw "Missing required prerequisites: $($missing -join ', '). Resolve the [FAIL] items above and re-run."
    }

    # ------------------------------------------------------------------ 2. .env
    Write-Section "Environment file"
    if (Test-Path ".env") {
        Write-Ok ".env already exists (left unchanged)"
    }
    else {
        Copy-Item ".env.example" ".env"
        Write-Ok "created .env from .env.example"
    }

    if ($NoDocker) {
        Write-Section "Done (-NoDocker)"
        Write-Warn2 "Skipped Postgres. Ensure DATABASE_URL in .env points to a reachable"
        Write-Warn2 "Postgres that already has the EMA schema (infra/database/ema-db)."
        Write-Host "`nNext:  pwsh .\scripts\test.ps1        # fast suite (no DB)" -ForegroundColor White
        Write-Host   "       pwsh .\scripts\test.ps1 -All   # + integration (needs the DB)" -ForegroundColor White
        exit 0
    }

    # -------------------------------------------------------------- 3. postgres
    Write-Section "PostgreSQL (docker compose)"
    if ($Clean) {
        Write-Warn2 "tearing down existing volume (-Clean): all local DB data will be lost"
        docker compose down -v *> $null
    }
    docker compose up -d postgres
    if ($LASTEXITCODE -ne 0) { throw "docker compose failed to start postgres (is port 5432 already in use?)." }
    Write-Ok "postgres container started"

    # -------------------------------------------------------------- 4. wait
    Write-Section "Waiting for database health"
    $ready = $false
    for ($i = 1; $i -le 30; $i++) {
        docker compose exec -T postgres pg_isready -U ema -d ema_ai *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw "Postgres did not become ready within 60s. Check: docker compose logs postgres" }
    Write-Ok "database accepting connections"

    # -------------------------------------------------------------- 5. verify
    Write-Section "Schema verification"
    $count = (docker compose exec -T postgres psql -U ema -d ema_ai -tAc `
        "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'").Trim()
    if ([int]$count -gt 0) {
        Write-Ok "public schema has $count tables"
    }
    else {
        Write-Fail "no tables found — schema did not auto-load."
        Write-Warn2 "If the volume pre-existed an earlier (empty) boot, re-run with -Clean."
        throw "Schema verification failed."
    }

    Write-Section "Bootstrap complete"
    Write-Host "Next:  pwsh .\scripts\test.ps1        # fast suite (no DB)"      -ForegroundColor White
    Write-Host   "       pwsh .\scripts\test.ps1 -All   # full suite (uses the DB)" -ForegroundColor White
    Write-Host   "       docker compose up -d api       # run the API on :8010"    -ForegroundColor White
}
finally {
    Pop-Location
}
