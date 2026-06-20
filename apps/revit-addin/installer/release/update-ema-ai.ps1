param(
    [string]$CurrentRoot = "",
    [string]$StagedRoot = "",
    [string]$ManifestPath = "",
    [switch]$Apply,
    [switch]$Rollback
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-CurrentRoot {
    param([string]$RequestedRoot)

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return (Resolve-Path -LiteralPath $RequestedRoot).Path
    }

    $scriptRoot = $PSScriptRoot
    return (Resolve-Path -LiteralPath (Join-Path $scriptRoot "..")).Path
}

function Read-Json {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "File not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Compare-Version {
    param([string]$Current, [string]$Candidate)

    if ([string]::IsNullOrWhiteSpace($Current)) {
        return 1
    }

    try {
        $currentVersion = [version]($Current.Split('-')[0])
        $candidateVersion = [version]($Candidate.Split('-')[0])
        return $candidateVersion.CompareTo($currentVersion)
    }
    catch {
        return 0
    }
}

function Get-InstalledManifest {
    param([string]$Root)

    $path = Join-Path $Root "manifest.json"
    if (Test-Path -LiteralPath $path) {
        return Read-Json -Path $path
    }

    return $null
}

function Test-Checksums {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$ChecksumsPath
    )

    if (-not (Test-Path -LiteralPath $ChecksumsPath)) {
        return [pscustomobject]@{
            ok = $false
            reason = "Checksum file not found."
        }
    }

    $entries = Get-Content -LiteralPath $ChecksumsPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    foreach ($entry in $entries) {
        $parts = $entry -split '\s+', 2
        if ($parts.Count -lt 2) {
            return [pscustomobject]@{ ok = $false; reason = "Malformed checksum entry: $entry" }
        }

        $expected = $parts[0].ToLowerInvariant()
        $relative = $parts[1].TrimStart('*').Trim()
        $candidate = Join-Path $Root ($relative -replace '/', '\')

        if (-not (Test-Path -LiteralPath $candidate)) {
            return [pscustomobject]@{ ok = $false; reason = "Missing staged file: $relative" }
        }

        $actual = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $expected) {
            return [pscustomobject]@{ ok = $false; reason = "Hash mismatch for $relative" }
        }
    }

    return [pscustomobject]@{ ok = $true; reason = "All checksums matched." }
}

function Copy-DirectorySnapshot {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        if ($_.Name -in @("_lkg", "updates")) {
            return
        }

        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

$currentRoot = Resolve-CurrentRoot -RequestedRoot $CurrentRoot
$stagedRoot = if (-not [string]::IsNullOrWhiteSpace($StagedRoot)) { (Resolve-Path -LiteralPath $StagedRoot).Path } else { $currentRoot }
$candidateManifestPath = if (-not [string]::IsNullOrWhiteSpace($ManifestPath)) { $ManifestPath } else { Join-Path $stagedRoot "manifest.json" }

$installedManifest = Get-InstalledManifest -Root $currentRoot
$candidateManifest = Read-Json -Path $candidateManifestPath

$comparison = Compare-Version -Current $installedManifest.version -Candidate $candidateManifest.version
if ($comparison -gt 0) {
    Write-Host "Candidate version: $($candidateManifest.version)"
    Write-Host "Installed version: $($installedManifest.version)"
    Write-Host "Update allowed."
}
elseif ($comparison -lt 0) {
    throw "Anti-downgrade protection blocked version $($candidateManifest.version). Installed version is $($installedManifest.version)."
}

$checksumsPath = Join-Path $stagedRoot "checksums.sha256"
$checksumResult = Test-Checksums -Root $stagedRoot -ChecksumsPath $checksumsPath
if (-not $checksumResult.ok) {
    throw $checksumResult.reason
}

$snapshotRoot = Join-Path $currentRoot "_lkg"
if (-not (Test-Path -LiteralPath $snapshotRoot)) {
    New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null
}

$snapshotStamp = Get-Date -Format "yyyyMMdd_HHmmss"
$snapshotPath = Join-Path $snapshotRoot $snapshotStamp

if ($Apply) {
    Write-Host "Creating last-known-good snapshot at $snapshotPath"
    Copy-DirectorySnapshot -Source $currentRoot -Destination $snapshotPath
    Write-Host "Staging candidate payload from $stagedRoot"
    foreach ($item in Get-ChildItem -LiteralPath $stagedRoot -Force) {
        if ($item.Name -in @("_lkg", "updates")) {
            continue
        }

        Copy-Item -LiteralPath $item.FullName -Destination $currentRoot -Recurse -Force
    }
    Write-Host "Update staging complete."
}
elseif ($Rollback) {
    $latest = Get-ChildItem -LiteralPath $snapshotRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $latest) {
        throw "No last-known-good snapshot was found."
    }

    Write-Host "Rolling back from $($latest.FullName)"
    foreach ($item in Get-ChildItem -LiteralPath $latest.FullName -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $currentRoot -Recurse -Force
    }
    Write-Host "Rollback complete."
}
else {
    [pscustomobject]@{
        current_version = $installedManifest.version
        candidate_version = $candidateManifest.version
        checksum_ok = $checksumResult.ok
        checksum_reason = $checksumResult.reason
        snapshot_root = $snapshotRoot
        apply_ready = $true
    } | ConvertTo-Json -Depth 20 | Write-Host
}

