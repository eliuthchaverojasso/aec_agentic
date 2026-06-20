# NISD Local Demo Script

## Purpose

Dry-run NISD project bootstrap flow on a local machine. No production data touches this flow.
All inputs are local-only and committed data is local-only.

---

## Pre-Demo Checklist

- [ ] Docker containers running (`docker compose -f Pipeline/pipeline/docker-compose.ai.yml up -d`)
- [ ] Backend accessible at `http://localhost:8000`
- [ ] `curl` or `Invoke-WebRequest` available in PowerShell
- [ ] Landing root folder exists: `C:\Temp\landing-root` (or your preferred path)
- [ ] No real NISD JSON files are present in the landing root
- [ ] Git working directory is clean (no uncommitted real files)

---

## Step-by-Step Demo

### Step 1: Verify Backend Health

```powershell
# Verify backend is running and responding
$response = Invoke-WebRequest -Uri "http://localhost:8000/api/v1/dev/status" -UseBasicParsing
$response.Content | ConvertFrom-Json
```

**Pass Criteria:**
- `status` is `"ok"` or `"degraded"` (not `"error"` or missing)
- `backend_health` is populated
- `database_health` is `"ok"`
- `version` field is present

**If it fails:**
1. `docker compose -f Pipeline/pipeline/docker-compose.ai.yml ps` — check containers
2. `docker compose -f Pipeline/pipeline/docker-compose.ai.yml logs backend` — check errors
3. Fix any issues before proceeding

---

### Step 2: Set Up Landing Environment

```powershell
# Create landing root if it doesn't exist
$landingPath = "C:\Temp\landing-root"
if (!(Test-Path $landingPath)) {
    New-Item -ItemType Directory -Path $landingPath -Force
    Write-Host "Created landing root at: $landingPath"
}

# Create a landing manifest (local demo only)
$manifestPath = Join-Path $landingPath "landing-manifest.json"
if (!(Test-Path $manifestPath)) {
    $manifest = @{
        manifest_version = "1"
        description = "Local demo manifest"
        projects = @()
    } | ConvertTo-Json -Depth 4
    Set-Content -Path $manifestPath -Value $manifest
    Write-Host "Created manifest at: $manifestPath"
}

# Create a project subfolder
$projectFolder = "$landingPath\NISD-Local-Demo"
if (!(Test-Path $projectFolder)) {
    New-Item -ItemType Directory -Path $projectFolder -Force
    Write-Host "Created project folder at: $projectFolder"
}

# Add some local demo files (optional, for visual completeness)
# Note: no real Revit/NISD files, just placeholders
"demo-placeholder" | Out-File "$projectFolder\.gitkeep"
Write-Host "Local demo files created."
```

---

### Step 3: Discover Landing

```powershell
$discoverPayload = @{
    landing_root = $landingPath
} | ConvertTo-Json -Depth 4

Write-Host "Discovering landing root..."
try {
    $discoverResponse = Invoke-WebRequest -Uri "http://localhost:8000/api/v1/landing/discover" `
        -Method POST `
        -ContentType "application/json" `
        -Body $discoverPayload `
        -UseBasicParsing
    $discoverJson = $discoverResponse.Content | ConvertFrom-Json
    Write-Host "Discover Result:"
    $discoverJson | ConvertTo-Json -Depth 4
} catch {
    Write-Host "Discover FAILED: $_"
    Write-Host "Response: $($_.Exception.Response)"
    exit 1
}
```

**Pass Criteria:**
- HTTP 200
- `ok` is `true`
- `projects` array is not empty (has at least one folder entry)
- `errors` array is empty
- `landing_root` matches input

**If discover returns empty `projects`:**
1. Verify files exist in `$landingPath`: `Get-ChildItem $landingPath -Recurse -Force`
2. Verify Docker mount: `docker inspect ema-backend-1 --format='{{json .Mounts}}'` (name may vary)
3. Check backend logs: `docker compose -f Pipeline/pipeline/docker-compose.ai.yml logs backend`

---

### Step 4: Bootstrap NISD Project

```powershell
$bootstrapPayload = @{
    landing_root = $landingPath
    project_folder_name = "NISD-Local-Demo"
    project_display_name = "NISD Local Demo Building"
    project_code = "NISD-LOD-001"
    client_code = "NISD"
    client_name = "NISD (Local Demo)"
    environment = "development"
} | ConvertTo-Json -Depth 4

Write-Host "Bootstrapping NISD project..."
try {
    $bootstrapResponse = Invoke-WebRequest -Uri "http://localhost:8000/api/v1/landing/bootstrap" `
        -Method POST `
        -ContentType "application/json" `
        -Body $bootstrapPayload `
        -UseBasicParsing
    $bootstrapJson = $bootstrapResponse.Content | ConvertFrom-Json
    Write-Host "Bootstrap Result:"
    $bootstrapJson | ConvertTo-Json -Depth 4
} catch {
    Write-Host "Bootstrap FAILED: $_"
    if ($_.Exception.Response) {
        $errorBody = $_.Exception.Response.GetResponseStream()
        $errorReader = New-Object IO.StreamReader($errorBody)
        Write-Host "Error Response: $($errorReader.ReadToEnd())"
    }
    exit 1
}
```

**Pass Criteria:**
- HTTP 200 or 201
- `ok` is `true`
- `project_id` is a valid integer
- `client_id` matches client name
- `project_folder_name === "NISD-Local-Demo"`
- `errors` array is empty
- `project_landing_path` resolves to your path

---

### Step 5: Verify Project Created

```powershell
Write-Host "Verifying project creation..."
$projectId = $bootstrapJson.project_id

try {
    $projectResponse = Invoke-WebRequest -Uri "http://localhost:8000/api/v1/projects/$projectId" `
        -UseBasicParsing
    $projectJson = $projectResponse.Content | ConvertFrom-Json
    Write-Host "Project Verified:"
    $projectJson | ConvertTo-Json -Depth 4
} catch {
    Write-Host "Project verification FAILED: $_"
    exit 1
}
```

**Pass Criteria:**
- HTTP 200
- `project_id` matches `bootstrapJson.project_id`
- `project_title === "NISD Local Demo Building"`
- `client_name === "NISD (Local Demo)"`

---

### Step 6: Trigger an Export

```powershell
Write-Host "Triggering export on NISD demo project..."
$exportPayload = @{
    project_id = $bootstrapJson.project_id
    model_id = 1          # or use first model ID from response
    export_type = "full"
} | ConvertTo-Json -Depth 4

try {
    $exportResponse = Invoke-WebRequest -Uri "http://localhost:8000/api/v1/exports/" `
        -Method POST `
        -ContentType "application/json" `
        -Body $exportPayload `
        -UseBasicParsing
    $exportJson = $exportResponse.Content | ConvertFrom-Json
    Write-Host "Export Triggered:"
    $exportJson | ConvertTo-Json -Depth 4
} catch {
    Write-Host "Export FAILED: $_"
    exit 1
}
```

**Pass Criteria:**
- HTTP 201 or 200
- `id` is assigned
- `status` is `"pending"` or `"completed"`
- `started_at` is populated

---

### Step 7: Verify Readiness Scoring

```powershell
Write-Host "Checking readiness scoring..."
try {
    $readinessResponse = Invoke-WebRequest -Uri "http://localhost:8000/api/v1/projects/$projectId/readiness" `
        -UseBasicParsing
    $readinessJson = $readinessResponse.Content | ConvertFrom-Json
    Write-Host "Readiness Score:"
    $readinessJson | ConvertTo-Json -Depth 4
    
    Write-Host "Overall Readiness: $($readinessJson.overall_readiness)"
    Write-Host "Label: $($readinessJson.label)"
} catch {
    Write-Host "Readiness FAILED: $_"
    exit 1
}
```

**Pass Criteria:**
- HTTP 200
- `overall_readiness` is between 0 and 100
- `label` is `"Green"`, `"Amber"`, or `"Red"`
- `requirement_coverage`, `qaqc_health`, `sync_freshness` all have scores
- `trade_readiness` is an array
- `gap_summary` has `critical`, `high`, `medium`, `low` keys

---

### Step 8: Trigger Readiness Recalculate (Snapshot)

```powershell
Write-Host "Recalculating readiness snapshot..."
try {
    $snapshotResponse = Invoke-WebRequest -Uri "http://localhost:8000/api/v1/projects/$projectId/readiness/recalculate" `
        -Method POST `
        -ContentType "application/json" `
        -Body '{}' `
        -UseBasicParsing
    $snapshotJson = $snapshotResponse.Content | ConvertFrom-Json
    Write-Host "Snapshot Created:"
    $snapshotJson | ConvertTo-Json -Depth 4
} catch {
    Write-Host "Snapshot FAILED: $_"
    exit 1
}
```

**Pass Criteria:**
- HTTP 200 or 201
- `id` is assigned
- `overall_score` matches current readiness score
- `created_at` is recent
- `trade_readiness` is an array

---

### Step 9: Verify No NISD Files Were Committed

```powershell
Write-Host "Verifying no NISD JSON files are committed..."
$committedNisd = git ls-files | Where-Object { $_ -match "NISD.*\.json" }
if ($committedNisd -and $committedNisd.Count -gt 0) {
    Write-Host "ERROR: NISD JSON files are committed!"
    Write-Host $committedNisd
    exit 1
}
Write-Host "OK: No NISD JSON files committed."
```

**Pass Criteria:**
- Empty result (no output from `git ls-files`)

---

## Summary of Pass Criteria

| Step | Endpoint | Key Pass Criterion |
|------|----------|--|------|------|---------|
| 1 | `GET /api/v1/dev/status` | `status === "ok"`, `database_health === "ok"` |
| 2 | File creation | All directories and manifest exist |
| 3 | `POST /landing/discover` | `ok === true`, `projects[].has_manifest === true` |
| 4 | `POST /landing/bootstrap` | `ok === true`, `project_id > 0`, no `errors` |
| 5 | `GET /projects/{id}` | `project_title`, `client_name` match inputs |
| 6 | `POST /exports/` | `id` assigned, `status` not empty |
| 7 | `GET /projects/{id}/readiness` | `overall_readiness` between 0–100, `label` valid |
| 8 | `POST /readiness/recalculate` | `id` assigned, `overall_score` valid |
| 9 | `git ls-files` | No `NISD*.json` tracked |

---

## Cleanup

After demo, optionally clean up:

```powershell
# Delete the test project folder locally
Remove-Item "$landingPath\NISD-Local-Demo" -Recurse -Force -ErrorAction SilentlyContinue

# Optionally delete the test project from the database
# curl -X DELETE http://localhost:8000/api/v1/projects/$projectId
```

---

## Troubleshooting Quick References

| Issue | Command |
|-------|-|------|------|
| Backend not responding | `docker compose -f Pipeline/pipeline/docker-compose.ai.yml logs backend` |
| Docker mount missing | `docker inspect ema-backend-1 --format='{{json .Mounts}}'` |
| DB not connecting | `docker compose -f Pipeline/pipeline/docker-compose.ai.yml pg_isready` |
| CORS issue | Check browser Network tab for `Access-Control-Allow-Origin` |
| File not visible in container | `docker exec -it ema-backend-1 ls -la /app/landing/` |
| Wrong database_url | Check env vars: `docker compose config --services backend` |
| Export stuck | Check worker logs: `docker compose -f Pipeline/pipeline/docker-compose.ai.yml logs worker` |
