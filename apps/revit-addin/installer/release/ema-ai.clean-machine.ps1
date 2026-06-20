param(
    [string]$RepoRoot = "",
    [string]$ComponentFile = "",
    [string]$Profile = "pilot-core",
    [switch]$IncludeLocalAi
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$bootstrapper = Join-Path $PSScriptRoot "ema-ai.bootstrapper.ps1"
if (-not (Test-Path -LiteralPath $bootstrapper)) {
    throw "Bootstrapper not found: $bootstrapper"
}

$output = Join-Path $env:TEMP ("ema-ai-clean-check-" + [guid]::NewGuid().ToString("n") + ".json")
try {
    & powershell -ExecutionPolicy Bypass -File $bootstrapper `
        -Action Check `
        -Profile $Profile `
        -RepoRoot $RepoRoot `
        -ComponentFile $ComponentFile `
        -OutputPath $output `
        -IncludeLocalAi:$IncludeLocalAi

    $result = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    $result | ConvertTo-Json -Depth 20

    if (-not $result.ok) {
        exit 2
    }
}
finally {
    Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue
}

