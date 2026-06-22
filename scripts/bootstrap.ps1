<#
.SYNOPSIS
  Bootstrap the AEC Agentic Control Plane local development environment.

.DESCRIPTION
  Idempotent and safe to re-run. Performs:
    1. Prerequisite checks (python, docker, pnpm, node, .NET).
    2. .env generation from .env.example (only if missing).
    3. PostgreSQL startup via docker compose.
    4. Wait for the database to become healthy.
    5. Apply Alembic migrations (`alembic upgrade head`) via the `migrate` service.
    6. Verify the schema loaded.
    7. Install backend Python dependencies.
    8. Install frontend Node dependencies (pnpm install).

  After this completes:  pwsh .\scripts\test.ps1 -All   should be green.

.PARAMETER Clean
  Tear down the Postgres volume first (DROPS all local database data) so the
  schema is re-loaded from scratch.

.PARAMETER NoDocker
  Skip Docker/Postgres entirely. Only checks prerequisites and generates .env.
  Use when you point DATABASE_URL at an external Postgres that already has the
  EMA schema.

.PARAMETER SkipFrontend
  Skip frontend dependency installation (pnpm install). Use when you only need
  the backend/API.

.EXAMPLE
  pwsh .\scripts\bootstrap.ps1
  pwsh .\scripts\bootstrap.ps1 -Clean
  pwsh .\scripts\bootstrap.ps1 -NoDocker
  pwsh .\scripts\bootstrap.ps1 -SkipFrontend
#>
param(
    [switch]$Clean,
    [switch]$NoDocker,
    [switch]$SkipFrontend
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
    $optional = @()

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

    # Node and pnpm are required for the web frontend (skip with -SkipFrontend).
    if (-not $SkipFrontend) {
        $node = Get-Command node -ErrorAction SilentlyContinue
        if ($node) { Write-Ok "node    $(node --version)" } else { Write-Fail "node not found (required for web-console; use -SkipFrontend to skip)"; $missing += "node" }

        $pnpm = Get-Command pnpm -ErrorAction SilentlyContinue
        if ($pnpm) {
            $pnpmVer = pnpm --version 2>&1
            Write-Ok "pnpm   $pnpmVer"
        } else { Write-Fail "pnpm not found (required for web-console; install via: npm install -g pnpm@9.15.0)"; $missing += "pnpm" }
    }
    else {
        Write-Warn2 "frontend checks skipped (-SkipFrontend)"
    }

    # Optional toolchains — warn only; not needed for API + tests.
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
        Write-Warn2 "Skipped Postgres. Point DATABASE_URL in .env at a reachable Postgres,"
        Write-Warn2 "then apply the schema with:  pwsh .\scripts\migrate.ps1 -Local"
        Write-Warn2 "(runs 'alembic upgrade head' from apps/control-plane-api)."
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

    # ----------------------------------------------------------- 5. migrations
    Write-Section "Database migrations (Alembic)"
    docker compose run --build --rm migrate
    if ($LASTEXITCODE -ne 0) { throw "alembic upgrade head failed. Check: docker compose run --rm migrate" }
    Write-Ok "alembic upgrade head applied"

    # -------------------------------------------------------------- 6. verify
    Write-Section "Schema verification"
    $count = (docker compose exec -T postgres psql -U ema -d ema_ai -tAc `
        "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'").Trim()
    if ([int]$count -gt 0) {
        Write-Ok "public schema has $count tables"
    }
    else {
        Write-Fail "no tables found — migrations did not apply."
        Write-Warn2 "Inspect: docker compose run --rm migrate ; docker compose logs postgres"
        throw "Schema verification failed."
    }
    $rev = (docker compose exec -T postgres psql -U ema -d ema_ai -tAc `
        "SELECT version_num FROM alembic_version").Trim()
    if ($rev) { Write-Ok "alembic revision: $rev" }
    else { Write-Warn2 "alembic_version is empty — migrations may not have run." }

    # ----------------------------------------------------------- 7. backend deps
    Write-Section "Backend Python dependencies"
    $apiReq = Join-Path $root "apps/control-plane-api/requirements.txt"
    if (Test-Path $apiReq) {
        pip install -r $apiReq --quiet 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Ok "Python dependencies installed from apps/control-plane-api/requirements.txt" }
        else { Write-Warn2 "pip install had warnings (check output above)" }
    }
    else {
        Write-Warn2 "requirements.txt not found at $apiReq — skipping"
    }

    # -------------------------------------------------------- 8. frontend deps
    if (-not $SkipFrontend) {
        Write-Section "Frontend dependencies (pnpm install)"
        if (Test-Path "package.json") {
            pnpm install 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) { Write-Ok "pnpm install completed" }
            else { Write-Warn2 "pnpm install had issues (check output above)" }
        }
        else {
            Write-Warn2 "package.json not found at root — skipping pnpm install"
        }
    }

    # ------------------------------------------------------------- 9. summary
    Write-Section "Bootstrap complete"
    Write-Host "Next:  pwsh .\scripts\test.ps1          # fast suite (no DB)"        -ForegroundColor White
    Write-Host   "       pwsh .\scripts\test.ps1 -All   # full suite (uses the DB)"   -ForegroundColor White
    Write-Host   "       pwsh .\scripts\dev.ps1          # start API + frontend"      -ForegroundColor White
    Write-Host   "       docker compose up -d api         # API only on :8010"         -ForegroundColor White
    Write-Host   "       pnpm run web:dev                 # frontend on :5173"         -ForegroundColor White
}
finally {
    Pop-Location
}
