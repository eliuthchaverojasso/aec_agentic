$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\.."
Push-Location $root
try {
    $env:PYTHONPATH = @(
        "$root\packages\python\control-plane-core\src",
        "$root\packages\python\policy-engine\src",
        "$root\packages\python\evidence-engine\src",
        "$root\packages\python\organism-runtime\src",
        "$root\packages\python\agent-runtime\src",
        "$root\packages\python\connector-sdk\src",
        "$root\packages\python\reporting\src",
        "$root\apps\control-plane-api"
    ) -join [IO.Path]::PathSeparator

    python -m pytest tests\architecture tests\contracts tests\conformance -q
}
finally {
    Pop-Location
}
