<#
.SYNOPSIS
  Apply database migrations with Alembic.

.DESCRIPTION
  Alembic is the single schema-authoring mechanism (Pending Work Register Item 13).
  By default this upgrades the Docker Compose Postgres to head via the one-shot
  `migrate` service. Use -Local to run on the host (python -m alembic) against the
  DATABASE_URL in your shell/.env; that requires apps/control-plane-api's
  requirements to be installed in the active Python environment.

.PARAMETER To
  Target revision. Default: head. Pass -To base to drop the schema (downgrade),
  or a revision id (e.g. 0001_baseline).

.PARAMETER Local
  Run alembic on the host instead of the Docker `migrate` service.

.EXAMPLE
  pwsh .\scripts\migrate.ps1
  pwsh .\scripts\migrate.ps1 -To base
  pwsh .\scripts\migrate.ps1 -Local
#>
param(
    [string]$To = "head",
    [switch]$Local
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

# Alembic picks upgrade vs downgrade by command name. We only have a linear
# history, so treat 'base' as the sole downgrade target; everything else
# (head, a revision id) is an upgrade.
$cmd = if ($To -eq "base") { "downgrade" } else { "upgrade" }

if ($Local) {
    Push-Location (Join-Path $root "apps\control-plane-api")
    try {
        python -m alembic $cmd $To
        exit $LASTEXITCODE
    }
    finally { Pop-Location }
}
else {
    Push-Location $root
    try {
        docker compose run --build --rm migrate alembic $cmd $To
        exit $LASTEXITCODE
    }
    finally { Pop-Location }
}
