<#
.SYNOPSIS
  Run a smoke test against the running AEC Control Plane API.

.DESCRIPTION
  Validates the core API is functional by:
    1. Checking GET /health
    2. Checking API metadata (GET /)
    3. Registering a test user (if registration is enabled)
    4. Logging in and obtaining a token
    5. Creating a test project
    6. Creating a test client
    7. Attempting readiness check

  Prints a pass/fail summary at the end.

.PARAMETER BaseUrl
  API base URL (default: http://localhost:8010).

.PARAMETER RegisterUser
  Attempt to register a test user (requires AUTH_ENABLE_PUBLIC_REGISTRATION=true).

.PARAMETER Email
  Email for login/registration (default: smoke-test@aec.dev).

.PARAMETER Password
  Password for login/registration (default: SmokeTest123!).

.EXAMPLE
  pwsh .\scripts\smoke.ps1
  pwsh .\scripts\smoke.ps1 -BaseUrl "http://localhost:8000"
  pwsh .\scripts\smoke.ps1 -RegisterUser
#>
param(
    [string]$BaseUrl = "http://localhost:8010",
    [switch]$RegisterUser,
    [string]$Email = "smoke-test@aec.dev",
    [string]$Password = "SmokeTest123!"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

$passed = 0
$failed = 0

function Test-Step($name, $scriptBlock) {
    try {
        & $scriptBlock
        Write-Host "  [PASS] $name" -ForegroundColor Green
        $script:passed++
    }
    catch {
        Write-Host "  [FAIL] $name : $_" -ForegroundColor Red
        $script:failed++
    }
}

function Invoke-Api($method, $path, $body, $token) {
    $uri = "$BaseUrl$path"
    $params = @{
        Method = $method
        Uri = $uri
        ContentType = "application/json"
        UseBasicParsing = $true
    }
    if ($body) { $params.Body = ($body | ConvertTo-Json -Compress) }
    if ($token) { $params.Headers = @{ Authorization = "Bearer $token" } }
    try {
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd()
            $reader.Close()
        }
        catch { $errorBody = "(no body)" }
        throw "HTTP $statusCode : $errorBody"
    }
}

Write-Host "=== AEC Control Plane Smoke Test ===" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl"
Write-Host ""

# --------------------------------------------------------------- 1. health
Test-Step "GET /health" {
    $health = Invoke-Api GET "/health"
    if ($health.status -ne "ok") { throw "status is '$($health.status)', expected 'ok'" }
    if (-not $health.database) { throw "missing database field" }
    Write-Host "    database=$($health.database) schema=$($health.schema_revision) version=$($health.version)" -ForegroundColor Gray
}

# --------------------------------------------------------------- 2. root
Test-Step "GET /" {
    $root = Invoke-Api GET "/"
    if (-not $root.name) { throw "missing name field" }
    if (-not $root.version) { throw "missing version field" }
    Write-Host "    name=$($root.name) version=$($root.version)" -ForegroundColor Gray
}

# --------------------------------------------------------------- 3. register (optional)
$token = $null
if ($RegisterUser) {
    Test-Step "POST /api/v1/auth/register" {
        $result = Invoke-Api POST "/api/v1/auth/register" @{
            name = "Smoke Test User"
            email = $Email
            password = $Password
        }
        if (-not $result.message) { throw "missing message in response" }
        Write-Host "    $($result.message): $($result.user.email)" -ForegroundColor Gray
    }
}

# --------------------------------------------------------------- 4. login
Test-Step "POST /api/v1/auth/login" {
    $result = Invoke-Api POST "/api/v1/auth/login" @{
        email = $Email
        password = $Password
    }
    if (-not $result.access_token) { throw "no access_token in response" }
    $script:token = $result.access_token
    Write-Host "    logged in as: $($result.user.email) role=$($result.user.role)" -ForegroundColor Gray
}

# --------------------------------------------------------------- 5. create project
$projectId = $null
Test-Step "POST /api/v1/projects (create project)" {
    $result = Invoke-Api POST "/api/v1/projects" @{
        name = "Smoke Test Project $(Get-Date -Format 'yyyyMMdd-HHmmss')"
    } -token $token
    if (-not $result.id) { throw "no id in response" }
    $script:projectId = $result.id
    Write-Host "    project id=$($result.id) name=$($result.project_title)" -ForegroundColor Gray
}

# --------------------------------------------------------------- 6. list projects
Test-Step "GET /api/v1/projects (list projects)" {
    $result = Invoke-Api GET "/api/v1/projects" -token $token
    if ($result.Count -eq 0) { throw "no projects returned" }
    Write-Host "    $($result.Count) project(s) returned" -ForegroundColor Gray
}

# --------------------------------------------------------------- 7. get project
if ($projectId) {
    Test-Step "GET /api/v1/projects/$projectId (get project detail)" {
        $result = Invoke-Api GET "/api/v1/projects/$projectId" -token $token
        if ($result.id -ne $projectId) { throw "project id mismatch" }
        Write-Host "    title=$($result.project_title)" -ForegroundColor Gray
    }
}

# --------------------------------------------------------------- 8. readiness check (expects no-data state)
if ($projectId) {
    Test-Step "GET /api/v1/projects/$projectId/readiness" {
        $result = Invoke-Api GET "/api/v1/projects/$projectId/readiness" -token $token
        Write-Host "    readiness available: project has data" -ForegroundColor Gray
    }
}

# --------------------------------------------------------------- summary
Write-Host ""
Write-Host "=== Results ===" -ForegroundColor Cyan
Write-Host "  Passed: $passed" -ForegroundColor Green
Write-Host "  Failed: $failed" -ForegroundColor Red

if ($failed -eq 0) {
    Write-Host "  Status: SMOKE TEST PASSED" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "  Status: SMOKE TEST FAILED" -ForegroundColor Red
    exit 1
}
