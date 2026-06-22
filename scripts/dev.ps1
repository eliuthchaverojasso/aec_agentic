<#
.SYNOPSIS
  Start the full local development environment (DB + API + frontend).

.DESCRIPTION
  Starts:
    1. PostgreSQL (via docker compose, if not already running)
    2. Backend API (via docker compose, on port ${API_PORT:-8010})
    3. Frontend dev server (Vite, on port ${WEB_PORT:-5173})

  Uses docker compose for the backend stack. The frontend runs as a background
  PowerShell job so its logs stream to the console.

.PARAMETER NoFrontend
  Skip starting the Vite frontend dev server.

.PARAMETER NoApi
  Skip building/starting the API container (DB only).

.PARAMETER Rebuild
  Force a Docker image rebuild (passes --build to docker compose).

.EXAMPLE
  pwsh .\scripts\dev.ps1
  pwsh .\scripts\dev.ps1 -NoFrontend
  pwsh .\scripts\dev.ps1 -Rebuild
#>
param(
    [switch]$NoFrontend,
    [switch]$NoApi,
    [switch]$Rebuild
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

function Write-Section($m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Write-Ok($m)       { Write-Host "  [ok]   $m" -ForegroundColor Green }
function Write-Warn2($m)    { Write-Host "  [warn] $m" -ForegroundColor Yellow }

Push-Location $root
try {
    # --------------------------------------------------------------- 1. postgres
    Write-Section "Starting PostgreSQL"
    docker compose up -d postgres
    if ($LASTEXITCODE -ne 0) { throw "docker compose failed to start postgres" }
    Write-Ok "postgres container started"

    # Wait for health
    $ready = $false
    for ($i = 1; $i -le 30; $i++) {
        docker compose exec -T postgres pg_isready -U ema -d ema_ai *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw "Postgres did not become ready within 60s" }
    Write-Ok "database accepting connections"

    # --------------------------------------------------------------- 2. api
    if (-not $NoApi) {
        Write-Section "Starting API"
        $composeArgs = @("up", "-d")
        if ($Rebuild) { $composeArgs += "--build" }
        $composeArgs += "api"
        docker compose @composeArgs
        if ($LASTEXITCODE -ne 0) { throw "docker compose failed to start api" }
        Write-Ok "API container started"

        # Wait for healthcheck
        $apiReady = $false
        for ($i = 1; $i -le 30; $i++) {
            $status = docker compose ps --format json api 2>$null | ConvertFrom-Json | Select-Object -ExpandProperty Health 2>$null
            if ($status -eq "healthy") { $apiReady = $true; break }
            Start-Sleep -Seconds 2
        }
        if ($apiReady) { Write-Ok "API is healthy" }
        else { Write-Warn2 "API healthcheck timeout — check: docker compose logs api" }
    }
    else {
        Write-Warn2 "Skipping API (-NoApi)"
    }

    # --------------------------------------------------------------- 3. frontend
    if (-not $NoFrontend) {
        Write-Section "Starting frontend dev server (Vite)"
        $frontendDir = Join-Path $root "apps/web-console"
        if (Test-Path (Join-Path $frontendDir "node_modules")) {
            $webPort = if ($env:WEB_PORT) { $env:WEB_PORT } else { "5173" }
            Write-Host "  Starting Vite on port $webPort ..." -ForegroundColor Gray
            $job = Start-Job -Name "vite-dev" -ScriptBlock {
                param($dir, $port)
                Set-Location $dir
                npx vite --host 0.0.0.0 --port $port
            } -ArgumentList $frontendDir, $webPort
            Write-Ok "Frontend dev server starting (Job ID: $($job.Id))"
            Write-Host "  Stream logs:  Receive-Job -Id $($job.Id) -Keep" -ForegroundColor Gray
            Write-Host "  Stop job:     Stop-Job -Id $($job.Id); Remove-Job -Id $($job.Id)" -ForegroundColor Gray
        }
        else {
            Write-Warn2 "node_modules not found in apps/web-console — run 'pnpm install' first"
        }
    }
    else {
        Write-Warn2 "Skipping frontend (-NoFrontend)"
    }

    # --------------------------------------------------------------- 4. summary
    Write-Section "Development environment"
    Write-Host "  API:       http://localhost:$($env:API_PORT ?? '8010')"      -ForegroundColor White
    Write-Host "  API docs:  http://localhost:$($env:API_PORT ?? '8010')/docs" -ForegroundColor White
    if (-not $NoFrontend) {
        Write-Host "  Frontend:  http://localhost:$($env:WEB_PORT ?? '5173')"  -ForegroundColor White
    }
    Write-Host "  DB:        postgresql://ema:ema_dev_pw@localhost:5432/ema_ai" -ForegroundColor Gray
    Write-Host "`nTo stop:  docker compose down"                                 -ForegroundColor Yellow
}
finally {
    Pop-Location
}
