<#
.SYNOPSIS
  Manage the local PostgreSQL database via Docker Compose.

.DESCRIPTION
  Convenience wrapper around the docker compose postgres service.
  Commands: start, stop, restart, status, reset, psql, wait, logs

.PARAMETER Command
  - start    : Start the Postgres container (if not running)
  - stop     : Stop the Postgres container
  - restart  : Restart the Postgres container
  - status   : Show container status and health
  - reset    : Stop, delete volume, and restart from scratch (DESTROYS DATA)
  - psql     : Open an interactive psql shell
  - wait     : Wait until Postgres accepts connections
  - logs     : Tail container logs
  - migrate  : Run Alembic migrations via the migrate service

.EXAMPLE
  pwsh .\scripts\db.ps1 start
  pwsh .\scripts\db.ps1 status
  pwsh .\scripts\db.ps1 reset
  pwsh .\scripts\db.ps1 psql
#>
param(
    [Parameter(Position=0)]
    [ValidateSet("start", "stop", "restart", "status", "reset", "psql", "wait", "logs", "migrate")]
    [string]$Command = "status"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

Push-Location $root
try {
    switch ($Command) {
        "start" {
            Write-Host "Starting PostgreSQL ..." -ForegroundColor Cyan
            docker compose up -d postgres
            if ($LASTEXITCODE -eq 0) { Write-Host "  [ok] postgres started" -ForegroundColor Green }
            exit $LASTEXITCODE
        }
        "stop" {
            Write-Host "Stopping PostgreSQL ..." -ForegroundColor Cyan
            docker compose stop postgres
            if ($LASTEXITCODE -eq 0) { Write-Host "  [ok] postgres stopped" -ForegroundColor Green }
            exit $LASTEXITCODE
        }
        "restart" {
            Write-Host "Restarting PostgreSQL ..." -ForegroundColor Cyan
            docker compose restart postgres
            if ($LASTEXITCODE -eq 0) { Write-Host "  [ok] postgres restarted" -ForegroundColor Green }
            exit $LASTEXITCODE
        }
        "status" {
            Write-Host "PostgreSQL status:" -ForegroundColor Cyan
            $ps = docker compose ps --format json postgres 2>$null | ConvertFrom-Json
            if ($ps) {
                Write-Host "  State:   $($ps.State)"
                Write-Host "  Health:  $($ps.Health)"
                Write-Host "  Ports:   $($ps.Publishers)"
            }
            else {
                Write-Host "  (not running)" -ForegroundColor Yellow
            }
        }
        "reset" {
            Write-Warning "This will DESTROY all local database data!"
            $confirm = Read-Host "Type 'reset' to confirm"
            if ($confirm -eq "reset") {
                Write-Host "Resetting PostgreSQL (destroying data) ..." -ForegroundColor Cyan
                docker compose down -v
                docker compose up -d postgres
                Write-Host "  Waiting for readiness ..."
                Start-Sleep -Seconds 3
                for ($i = 1; $i -le 30; $i++) {
                    docker compose exec -T postgres pg_isready -U ema -d ema_ai *> $null
                    if ($LASTEXITCODE -eq 0) { break }
                    Start-Sleep -Seconds 2
                }
                Write-Host "  Running migrations ..."
                docker compose run --build --rm migrate
                Write-Host "  [ok] database reset complete" -ForegroundColor Green
            }
            else {
                Write-Host "Reset cancelled." -ForegroundColor Yellow
            }
        }
        "psql" {
            Write-Host "Opening psql shell ... (exit with \q)" -ForegroundColor Cyan
            docker compose exec -it postgres psql -U ema -d ema_ai
        }
        "wait" {
            Write-Host "Waiting for database health ..." -ForegroundColor Cyan
            $ready = $false
            for ($i = 1; $i -le 30; $i++) {
                docker compose exec -T postgres pg_isready -U ema -d ema_ai *> $null
                if ($LASTEXITCODE -eq 0) { $ready = $true; break }
                Write-Host "  waiting ($i/30) ..." -ForegroundColor Gray
                Start-Sleep -Seconds 2
            }
            if ($ready) { Write-Host "  [ok] database ready" -ForegroundColor Green; exit 0 }
            else { Write-Host "  [FAIL] database did not become ready" -ForegroundColor Red; exit 1 }
        }
        "logs" {
            docker compose logs -f postgres
        }
        "migrate" {
            Write-Host "Running Alembic migrations ..." -ForegroundColor Cyan
            docker compose run --build --rm migrate
            if ($LASTEXITCODE -eq 0) { Write-Host "  [ok] migrations applied" -ForegroundColor Green }
            exit $LASTEXITCODE
        }
    }
}
finally {
    Pop-Location
}
