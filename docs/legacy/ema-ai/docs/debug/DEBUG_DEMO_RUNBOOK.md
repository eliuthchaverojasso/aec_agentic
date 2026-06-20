# EMA AI — Debug & Demo Runbook

## Overview

This runbook covers how to test and verify every stage of the EMA AI pipeline
from **Project Setup** through **Readiness Scoring** on a local Windows machine.
Use it for onboarding, regression verification, and NISD demos.

---

## Prerequisites

| Item | Minimum |
|------|---------|
| Windows 10 / 11 | — |
| Docker Desktop | 24+ |
| PostgreSQL | via Docker (see `docker-compose.ai.yml`) |
| Python 3.11+ | for `scripts/qa/*.ps1` |
| npm 10+ | for frontend build verification |
| Local backend | running on `http://localhost:8000` |

---

## 1. Test Project Setup

### 1.1 Verify Project Creation API

```powershell
curl -s -X POST http://localhost:8000/api/v1/landing/discover `
  -H "Content-Type: application/json" `
  -d '{}'
```

**Expected:** `200` or `201` with `ok: true` and `counts` object.

### 1.2 Verify Project Summary Response

```powershell
curl -s http://localhost:8000/api/v1/projects | ConvertFrom-Json
```

**Verify:**
- `project_title` is populated
- `project_code` / `job_number` are present
- `client_id` matches expected client
- `active_models` >= 0

### 1.3 Verify Landing Configuration

```powershell
curl -s http://localhost:8000/api/v1/landing/status | ConvertFrom-Json
```

**Verify:**
- `landing_root_configured` is `true`
- `default_project_folder` is set
- No `errors` in response

---

## 2. Test Landing Discover

### 2.1 Run Landing Discover

```powershell
$payload = @{
  landing_root = "C:\Temp\landing-root"
  project_folder_name = "NISD-Test-01"
}

curl -s -X POST http://localhost:8000/api/v1/landing/discover `
  -H "Content-Type: application/json" `
  -d ($payload | ConvertTo-Json -Depth 4)
```

**Expected fields in response:**

| Field | Expected Value |
|-------|----------------|
| `ok` | `true` |
| `projects` | array with at least one entry |
| `has_manifest` | `true` if manifest exists |
| `latest_revit_export` | `.rvt` file path or `$null` |
| `errors` | empty array |
| `warnings` | may contain non-critical notes |

### 2.2 Verify File Discovery

The response `projects[].counts` should include:

| Key | Meaning |
|-----|---------|
| `revit_files` | `.rvt` count |
| `dwfx_files` | `.dwfx` count |
| `pdf_files` | `.pdf` count |
| `spec_files` | specification count |

---

## 3. Test Bootstrap NISD

### 3.1 Bootstrap a NISD Project

```powershell
$payload = @{
  landing_root = "C:\Temp\landing-root"
  project_folder_name = "NISD-Test-01"
  project_display_name = "NISD Demo Building"
  project_code = "NISD-DEM-001"
  client_code = "NISD"
  client_name = "NISD (Demo)"
  environment = "development"
}

curl -s -X POST http://localhost:8000/api/v1/landing/bootstrap `
  -H "Content-Type: application/json" `
  -d ($payload | ConvertTo-Json -Depth 4)
```

**Expected response fields:**

| Field | Expected |
|-------|----------|
| `ok` | `true` |
| `project_id` | integer >= 1 |
| `client_id` | integer or `$null` |
| `project_folder_name` | `"NISD-Test-01"` |
| `project_landing_path` | full Windows path |
| `discovered_files` | >= 0 |
| `warnings` | may be non-empty |
| `errors` | empty array |

### 3.2 Verify Database Persistence

```powershell
curl -s http://localhost:8000/api/v1/projects/1 | ConvertFrom-Json | Select-Object project_title, client_name
```

**Verify:** `project_id === 1`, title matches bootstrap input.

### 3.3 Verify No Real NISD JSON Is Committed

```powershell
git ls-files | Select-String -Pattern 'NISD.*\.json'
```

**Expected:** empty output. Real NISD JSON files must never be committed.

---

## 4. Test Processing / Sync

### 4.1 Trigger an Export

```powershell
curl -s -X POST http://localhost:8000/api/v1/exports/ `
  -H "Content-Type: application/json" `
  -d '{"project_id": 1, "model_id": 1, "export_type": "full"}' | ConvertFrom-Json
```

**Verify:**
- `status` is `"pending"` or `"completed"`
- `started_at` is populated
- `id` is assigned

### 4.2 Check Export Status

```powershell
curl -s http://localhost:8000/api/v1/exports | ConvertFrom-Json
```

**Verify:**
- Latest export for project has `status === "completed"`
- `completed_at` is not `$null`
- `element_count` > 0

### 4.3 Verify Readiness Computation

```powershell
curl -s http://localhost:8000/api/v1/projects/1/readiness | ConvertFrom-Json
```

**Verify response structure:**

| Field | Expected Type |
|-------|---------------|
| `overall_readiness` | number 0–100 |
| `label` | `"Green"`, `"Amber"`, or `"Red"` |
| `requirement_coverage.score` | number 0–100 |
| `qaqc_health.score` | number 0–100 |
| `sync_freshness.score` | number 0–100 |
| `trade_readiness` | array |
| `gap_summary` | object with `critical`, `high`, `medium`, `low` |
| `recommended_actions` | array |

### 4.4 Verify Readiness Snapshot Persistence

```powershell
curl -s -X POST http://localhost:8000/api/v1/projects/1/readiness/recalculate | ConvertFrom-Json
```

**Verify:**
- Response is a `ReadinessSnapshotOut` object (has `id`, `overall_score`, `created_at`)
- Subsequent GET at `GET /api/v1/projects/1/readiness/snapshots` returns at least 1 item

---

## 5. Test Debug / Logs (Once Implemented)

### 5.1 Verify Debug Endpoint

```powershell
curl -s http://localhost:8000/api/v1/dev/status | ConvertFrom-Json
```

**Verify:**
- `status` is `"ok"` or `"degraded"`
- `backend_health` is not empty
- `database_health` is `"ok"`
- `counts` object has all expected keys

### 5.2 Verify Debug API Contract

```powershell
curl -s http://localhost:8000/docs | Select-String -Pattern 'debug'
```

**Verify:** `/api/v1/dev/*` endpoints appear in Swagger docs.

### 5.3 Verify Error Logs Are Captured

Simulate an error:

```powershell
curl -s http://localhost:8000/api/v1/projects/999999/readiness
```

**Verify:**
- Response is `404` with `"Project not found"`
- No unhandled exceptions in backend logs
- Error is not echoed to frontend

---

## 6. Diagnose Windows Landing Path vs Docker /app/landing Mismatch

### 6.1 The Problem

The backend runs inside Docker at `/app/landing/...`
Host Windows mounts a path like `C:\Temp\landing-root` to that container path.
If the mount is misconfigured, the discover/bootstrap endpoints fail silently.

### 6.2 Check Docker Mount

```powershell
docker inspect ema-pipeline-backend-1 --format='{{json .Mounts}}' | ConvertFrom-Json
```

**Verify:**
- At least one mount with `Destination === '/app/landing'`
- `Source` shows the Windows path
- `Driver === 'local'`

### 6.3 Check Backend Path Resolution

```powershell
docker exec -it ema-pipeline-backend-1 pwd
```

**Verify:** the working directory contains `landing/` as a subdirectory.

### 6.4 Check File Visibility

```powershell
docker exec ema-pipeline-backend-1 ls -la /app/landing/
```

**Expected:** files from `C:\Temp\landing-root` appear inside the container.

### 6.5 Common Mismatch Scenarios

| Symptom | Likely Cause | Fix |
|---------|-----|-----|
| `landing_root_configured: false` | Docker mount missing | Fix `docker-compose.ai.yml` volumes |
| Discover returns empty `projects` | Wrong mount path or permissions | Verify `Source` in mount inspect |
| Bootstrap fails with file-not-found | Windows path not normalized | Use forward slashes: `C:/Temp/landing-root` |
| Export fails silently | Element count = 0 in DB | Check Element table for matching rows |

---

## 7. Verify Real NISD JSON Remains Uncommitted

### 7.1 Add NISD Files to .gitignore

Ensure `.gitignore` includes:

```
NISD-*.json
NISD-*.rvt
NISD-*.dwfx
NISD-*.pdf
landing-root/NISD-*/
```

### 7.2 Verify No NISD Files Are Tracked

```powershell
git ls-files | Select-String -Pattern 'NISD'
```

**Expected:** empty output.

### 7.3 Verify .gitignore Is Active

```powershell
git check-ignore NISD-test.json
```

**Expected:** `NISD-test.json` is printed (meaning it is ignored).

---

## 8. Quick Smoke Test Run Order

1. `POST /api/v1/landing/discover` — Landing Discover
2. `POST /api/v1/landing/bootstrap` — Bootstrap NISD
3. `GET /api/v1/projects/1` — Verify project created
4. `POST /api/v1/exports/` — Trigger export
5. `GET /api/v1/exports` — Verify completed
6. `GET /api/v1/projects/1/readiness` — Verify readiness
7. `POST /api/v1/projects/1/readiness/recalculate` — Verify snapshot
8. `GET /api/v1/dev/status` — Verify debug
9. `GET /api/v1/projects/999999/readiness` — Verify error handling

Run `scripts/qa/smoke-debug-local.ps1` to automate steps 3-9.
