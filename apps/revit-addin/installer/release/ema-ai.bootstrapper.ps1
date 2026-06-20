param(
    [ValidateSet("Plan", "Check", "Start", "Stop", "Status", "Manifest")]
    [string]$Action = "Plan",

    [string]$Profile = "pilot-core",

    [string]$RepoRoot = "",

    [string]$ComponentFile = "",

    [string]$OutputPath = "",

    [string]$Version = "1.0.0-dev.1",

    [switch]$IncludeLocalAi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:BootstrapperRoot = $PSScriptRoot

function Resolve-EmaAiRoot {
    param([string]$RequestedRoot)

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return (Resolve-Path -LiteralPath $RequestedRoot).Path
    }

    $installedRoot = Join-Path $script:BootstrapperRoot ".."
    if (Test-Path -LiteralPath (Join-Path $installedRoot "manifest.json")) {
        return (Resolve-Path -LiteralPath $installedRoot).Path
    }

    $repoRoot = Join-Path $script:BootstrapperRoot "..\.."
    if (Test-Path -LiteralPath $repoRoot) {
        return (Resolve-Path -LiteralPath $repoRoot).Path
    }

    return (Get-Location).Path
}

function Resolve-AppRoot {
    param([string]$RequestedRoot)

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return (Resolve-Path -LiteralPath $RequestedRoot).Path
    }

    $installedRoot = Join-Path $script:BootstrapperRoot ".."
    if (Test-Path -LiteralPath (Join-Path $installedRoot "docker-compose.release.yml")) {
        return (Resolve-Path -LiteralPath $installedRoot).Path
    }

    $repoStage = Join-Path $script:BootstrapperRoot "..\..\artifacts\release\stage"
    if (Test-Path -LiteralPath $repoStage) {
        return (Resolve-Path -LiteralPath $repoStage).Path
    }

    return (Resolve-Path -LiteralPath $installedRoot).Path
}

function Read-JsonFile {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "JSON file not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-EmaAiComponentDefinitions {
    param([string]$Root, [string]$Path)

    $candidate = $Path
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $scriptDir = $script:BootstrapperRoot
        $localCandidate = Join-Path $scriptDir "ema-ai.components.json"
        if (Test-Path -LiteralPath $localCandidate) {
            $candidate = $localCandidate
        }
        else {
            $installedCandidate = Join-Path (Resolve-Path -LiteralPath (Join-Path $scriptDir "..")).Path "ema-ai.components.json"
            if (Test-Path -LiteralPath $installedCandidate) {
                $candidate = $installedCandidate
            }
            else {
                $candidate = Join-Path $Root "installer\release\ema-ai.components.json"
            }
        }
    }

    return Read-JsonFile -Path $candidate
}

function Get-RevitInstallations {
    $years = 2022..2027
    $items = foreach ($year in $years) {
        $installDir = "C:\Program Files\Autodesk\Revit $year"
        $api = Join-Path $installDir "RevitAPI.dll"
        $ui = Join-Path $installDir "RevitAPIUI.dll"
        [pscustomobject]@{
            year = $year
            install_dir = $installDir
            available = (Test-Path -LiteralPath $api) -and (Test-Path -LiteralPath $ui)
        }
    }

    return @($items)
}

function Test-WebView2Runtime {
    $locations = @(
        (Join-Path ${env:ProgramFiles} "Microsoft\EdgeWebView\Application"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft\EdgeWebView\Application")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($location in $locations) {
        if (-not (Test-Path -LiteralPath $location)) {
            continue
        }

        $runtime = Get-ChildItem -LiteralPath $location -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "msedgewebview2.exe") } |
            Select-Object -First 1

        if ($runtime) {
            return [pscustomobject]@{
                available = $true
                path = (Join-Path $runtime.FullName "msedgewebview2.exe")
            }
        }
    }

    return [pscustomobject]@{
        available = $false
        path = $null
    }
}

function Test-DockerDesktop {
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        return $false
    }

    try {
        & $docker.Source info *> $null
        return ($LASTEXITCODE -eq 0)
    }
    catch {
        return $false
    }
}

function Test-ProcessRunning {
    param([string]$Name)

    return [bool](Get-Process -Name $Name -ErrorAction SilentlyContinue)
}

function Test-PortListening {
    param([Parameter(Mandatory)][int]$Port)

    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        $listener.Stop()
        return $false
    }
    catch {
        return $true
    }
}

function Get-InstalledVersion {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $name = Split-Path -Leaf $Path
    if ($name -match '(?<version>\d+\.\d+\.\d+(?:[-+][A-Za-z0-9\.-]+)?)') {
        return $Matches.version
    }

    return "0.0.0"
}

function Compare-EmaAiVersion {
    param(
        [string]$CurrentVersion,
        [string]$CandidateVersion
    )

    if ([string]::IsNullOrWhiteSpace($CurrentVersion)) {
        return 1
    }

    try {
        $current = [version]($CurrentVersion.Split('-')[0])
        $candidate = [version]($CandidateVersion.Split('-')[0])
        return $candidate.CompareTo($current)
    }
    catch {
        return 0
    }
}

function Resolve-InstallPlan {
    param(
        [Parameter(Mandatory)]$Definitions,
        [string]$ProfileName,
        [switch]$LocalAi
    )

    $profile = $Definitions.profiles | Where-Object { $_.id -eq $ProfileName } | Select-Object -First 1
    if (-not $profile) {
        throw "Unknown install profile: $ProfileName"
    }

    $selectedIds = @($profile.components)
    if ($LocalAi -and ($selectedIds -notcontains "optional-local-ai")) {
        $selectedIds += "optional-local-ai"
    }

    $resolved = New-Object System.Collections.ArrayList
    $seen = New-Object System.Collections.ArrayList

    function Add-Component {
        param([string]$ComponentId)

        if ([string]::IsNullOrWhiteSpace($ComponentId)) {
            return
        }

        if ($seen -contains $ComponentId) {
            return
        }

        $component = @($Definitions.components | Where-Object { $_.id -eq $ComponentId }) | Select-Object -First 1
        if (-not $component) {
            throw "Component definition not found: $ComponentId"
        }

        foreach ($dependency in @($component.dependencies)) {
            Add-Component -ComponentId ([string]$dependency)
        }

        [void]$seen.Add($ComponentId)
        [void]$resolved.Add($component)
    }

    foreach ($id in $selectedIds) {
        Add-Component -ComponentId $id
    }

    return [pscustomobject]@{
        profile = $profile
        components = @($resolved)
    }
}

function Test-EmaAiPrerequisites {
    param([Parameter(Mandatory)]$Plan)

    $warnings = New-Object System.Collections.Generic.List[string]
    $blockers = New-Object System.Collections.Generic.List[string]

    $revit = Get-RevitInstallations | Where-Object { $_.available }
    $webView2 = Test-WebView2Runtime

    foreach ($component in $Plan.components) {
        switch ($component.id) {
            "revit-addin" {
                if ($revit.Count -eq 0) {
                    $blockers.Add("No supported Revit installation was detected.")
                }

                if (-not $webView2.available) {
                    $warnings.Add("WebView2 runtime not detected; report navigation will fall back to the browser.")
                }
            }
            "backend-api" {
                if (-not (Test-DockerDesktop)) {
                    $blockers.Add("Docker Desktop is required for the backend API component.")
                }
            }
            "database" {
                if (-not (Test-DockerDesktop)) {
                    $blockers.Add("Docker Desktop is required for the PostgreSQL component.")
                }
            }
            "frontend-dashboard" {
                if (Test-PortListening -Port 5173) {
                    $warnings.Add("Port 5173 is already in use.")
                }
            }
            "optional-local-ai" {
                if (-not (Get-Command ollama -ErrorAction SilentlyContinue)) {
                    $warnings.Add("Ollama CLI was not detected; local AI remains optional.")
                }
            }
        }
    }

    return [pscustomobject]@{
        blockers = @($blockers)
        warnings = @($warnings)
        revit = @($revit)
        webview2 = $webView2
        docker = (Test-DockerDesktop)
    }
}

function Get-ReleaseManifest {
    param(
        [Parameter(Mandatory)]$Plan,
        [string]$Version = "1.0.0-dev.1",
        [string]$Channel = "pilot",
        [string]$GitCommit = "",
        [string]$BuiltAtUtc = ""
    )

    if ([string]::IsNullOrWhiteSpace($BuiltAtUtc)) {
        $BuiltAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }

    return [pscustomobject]@{
        product = "EMA AI"
        channel = $Channel
        version = $Version
        build = [pscustomobject]@{
            git_commit = $GitCommit
            built_at_utc = $BuiltAtUtc
        }
        profile = $Plan.profile.id
        components = @(
            $Plan.components | ForEach-Object {
                [pscustomobject]@{
                    id = $_.id
                    display_name = $_.display_name
                    kind = $_.kind
                    payload_root = $_.payload_root
                    dependencies = @($_.dependencies)
                    prerequisites = @($_.prerequisites)
                    data_retention = $_.data_retention
                }
            }
        )
        signature = [pscustomobject]@{
            algorithm = "rsa-sha256"
            thumbprint = ""
            state = "pending"
        }
        anti_downgrade = [pscustomobject]@{
            minimum_version = $Version
            allow_same_version = $false
        }
    }
}

function Invoke-EmaAiCheck {
    param([Parameter(Mandatory)]$Plan)

    $result = Test-EmaAiPrerequisites -Plan $Plan
    $backendHealth = [pscustomobject]@{ available = $false; status = "unknown" }
    $frontendHealth = [pscustomobject]@{ available = $false; status = "unknown" }

    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8010/health" -UseBasicParsing -TimeoutSec 5
        $backendHealth = [pscustomobject]@{
            available = $true
            status = $response.StatusCode
        }
    }
    catch {
        $backendHealth = [pscustomobject]@{
            available = $false
            status = "unavailable"
        }
    }

    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5173/health" -UseBasicParsing -TimeoutSec 5
        $frontendHealth = [pscustomobject]@{
            available = $true
            status = $response.StatusCode
        }
    }
    catch {
        $frontendHealth = [pscustomobject]@{
            available = $false
            status = "unavailable"
        }
    }

    [pscustomobject]@{
        ok = ($result.blockers.Count -eq 0)
        blockers = $result.blockers
        warnings = $result.warnings
        revit = $result.revit
        webview2 = $result.webview2
        docker = $result.docker
        backend_health = $backendHealth
        frontend_health = $frontendHealth
    }
}

function Invoke-EmaAiStart {
    param([Parameter(Mandatory)]$Plan)

    if ($Plan.components.id -contains "backend-api") {
        $appRoot = Resolve-AppRoot -RequestedRoot $RepoRoot
        $compose = Join-Path $appRoot "docker-compose.release.yml"

        if (-not (Test-Path -LiteralPath $compose)) {
            throw "Release compose file not found: $compose"
        }

        $docker = Get-Command docker -ErrorAction SilentlyContinue
        if (-not $docker) {
            throw "Docker CLI was not found."
        }

        Write-Host "Starting EMA AI services from: $compose"
        & $docker.Source compose -f $compose up -d
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose up failed with exit code $LASTEXITCODE."
        }

        Start-Sleep -Seconds 3
        try {
            Invoke-WebRequest -Uri "http://localhost:8010/health" -UseBasicParsing -TimeoutSec 10 | Out-Null
            Write-Host "Backend health check passed." -ForegroundColor Green
        }
        catch {
            Write-Warning "Backend health check could not be confirmed immediately."
        }
    }
}

function Invoke-EmaAiStop {
    param([Parameter(Mandatory)]$Plan)

    if ($Plan.components.id -contains "backend-api") {
        $appRoot = Resolve-AppRoot -RequestedRoot $RepoRoot
        $compose = Join-Path $appRoot "docker-compose.release.yml"

        if (-not (Test-Path -LiteralPath $compose)) {
            throw "Release compose file not found: $compose"
        }

        $docker = Get-Command docker -ErrorAction SilentlyContinue
        if (-not $docker) {
            throw "Docker CLI was not found."
        }

        Write-Host "Stopping EMA AI services from: $compose"
        & $docker.Source compose -f $compose down
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose down failed with exit code $LASTEXITCODE."
        }
    }
}

function Write-EmaAiOutput {
    param(
        [Parameter(Mandatory)]$Value,
        [string]$Path
    )

    $json = $Value | ConvertTo-Json -Depth 32
    if ([string]::IsNullOrWhiteSpace($Path)) {
        Write-Output $json
        return
    }

    Set-Content -LiteralPath $Path -Value $json -Encoding UTF8
    Write-Host "Wrote: $Path"
}

$root = Resolve-EmaAiRoot -RequestedRoot $RepoRoot
$componentDefinitions = Get-EmaAiComponentDefinitions -Root $root -Path $ComponentFile
$plan = Resolve-InstallPlan -Definitions $componentDefinitions -ProfileName $Profile -LocalAi:$IncludeLocalAi

switch ($Action) {
    "Plan" {
        Write-EmaAiOutput -Value $plan -Path $OutputPath
    }
    "Check" {
        Write-EmaAiOutput -Value (Invoke-EmaAiCheck -Plan $plan) -Path $OutputPath
    }
    "Manifest" {
        $manifest = Get-ReleaseManifest -Plan $plan -Version $Version
        Write-EmaAiOutput -Value $manifest -Path $OutputPath
    }
    "Start" {
        Invoke-EmaAiStart -Plan $plan
    }
    "Stop" {
        Invoke-EmaAiStop -Plan $plan
    }
    "Status" {
        Write-EmaAiOutput -Value (Invoke-EmaAiCheck -Plan $plan) -Path $OutputPath
    }
}
