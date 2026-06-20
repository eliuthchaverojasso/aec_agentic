param(
    [string]$RepoRoot = "",
    [string]$ComponentFile = "",
    [string]$Profile = "pilot-core",
    [switch]$IncludeLocalAi
)

$installRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$componentFile = $ComponentFile
if ([string]::IsNullOrWhiteSpace($componentFile)) {
    $componentFile = Join-Path $installRoot "ema-ai.components.json"
}

$bootstrapper = Join-Path $PSScriptRoot "ema-ai.bootstrapper.ps1"
& powershell -ExecutionPolicy Bypass -File $bootstrapper `
    -Action Stop `
    -RepoRoot $installRoot `
    -ComponentFile $componentFile `
    -Profile $Profile `
    -IncludeLocalAi:$IncludeLocalAi
