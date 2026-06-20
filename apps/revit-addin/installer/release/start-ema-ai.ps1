param(
    [string]$RepoRoot = "",
    [string]$ComponentFile = "",
    [string]$Profile = "pilot-core",
    [switch]$IncludeLocalAi
)

$installRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$manifestPath = Join-Path $installRoot "manifest.json"
if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $env:EMA_AI_PRODUCT_VERSION = [string]$manifest.version
    $env:EMA_AI_GIT_SHA = [string]$manifest.build.git_commit
}
else {
    $env:EMA_AI_PRODUCT_VERSION = "1.0.0-dev.1"
    $env:EMA_AI_GIT_SHA = "unknown"
}

$env:EMA_AI_INSTALL_ROOT = $installRoot
$env:EMA_AI_MANIFEST_PATH = $manifestPath

$componentFile = $ComponentFile
if ([string]::IsNullOrWhiteSpace($componentFile)) {
    $componentFile = Join-Path $installRoot "ema-ai.components.json"
}

$bootstrapper = Join-Path $PSScriptRoot "ema-ai.bootstrapper.ps1"
& powershell -ExecutionPolicy Bypass -File $bootstrapper `
    -Action Start `
    -RepoRoot $installRoot `
    -ComponentFile $componentFile `
    -Profile $Profile `
    -IncludeLocalAi:$IncludeLocalAi
