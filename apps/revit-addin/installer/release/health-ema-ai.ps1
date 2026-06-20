param(
    [string]$RepoRoot = "",
    [string]$ComponentFile = "",
    [string]$Profile = "pilot-core",
    [switch]$IncludeLocalAi
)

$installRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$manifestPath = Join-Path $installRoot "manifest.json"
$manifest = $null
if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}

$componentFile = $ComponentFile
if ([string]::IsNullOrWhiteSpace($componentFile)) {
    $componentFile = Join-Path $installRoot "ema-ai.components.json"
}

$bootstrapper = Join-Path $PSScriptRoot "ema-ai.bootstrapper.ps1"
$status = & powershell -ExecutionPolicy Bypass -File $bootstrapper `
    -Action Status `
    -RepoRoot $installRoot `
    -ComponentFile $componentFile `
    -Profile $Profile `
    -IncludeLocalAi:$IncludeLocalAi | ConvertFrom-Json

$health = [pscustomobject]@{
    product_version = if ($manifest) { $manifest.version } else { "1.0.0-dev.1" }
    git_sha = if ($manifest) { $manifest.build.git_commit } else { "unknown" }
    install_root = $installRoot
    database_status = if ($status.backend_health.available -and $status.backend_health.status -eq 200) { "ok" } else { "unavailable" }
    backend_health = $status.backend_health
    frontend_health = $status.frontend_health
    prerequisites = $status
}

$health | ConvertTo-Json -Depth 20
