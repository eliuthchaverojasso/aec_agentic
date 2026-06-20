# Local Demo Checklist

## Pre-Demo Validation
- [x] Frontend compiles clean: `npm run build` in `frontend/` ✅ (verified 2026-05-24, chunk size warning only)
- [x] Backend compiles clean: `python -m compileall app/` ✅ (verified 2026-05-23)

## Demo Steps

### 1. Start Environment
- [x] Start Docker compose from `Pipeline/pipeline/` ✅ (verified 2026-05-24, postgres + api containers healthy)
- [x] Verify backend health: `GET /api/v1/health` ✅ (status=ok, database=ok, version=0.1.0)
- [ ] Start frontend dev server or serve build output
- [ ] Open Processing page

### 2. Project Selection
- [x] Landing Status shows folder/file counts before project selected ✅ (verified via API for project 1 ROCHELL ES)
- [ ] Select or create project via project selector (top bar)
- [x] Section A shows Selected Project Context (name, ID, client) ✅ (verified via API)

### 3. Environment Check
- [x] Section B shows environment snapshot ✅ (verified: landing_dir=/app/landing, container_hint=true)
- [x] Path mapping status displayed (landing_dir, file_path_mode, container_hint) ✅
- [x] Warning banner shown if container path / host OS mismatch ✅ (backend returns warning)

### 4. Run Operations (Safe → Preparation → Write)
- [x] Run Health Check — confirms backend reachable ✅
- [x] Run Landing Status — shows folder checklist and file counts ✅ (2 revit, 6 drawings, 104 specs, 2 owner reqs)
- [x] Run Scan Landing — updates manifest status in section C ✅ (114 files found, 114 documents)
- [x] Run Rebuild Manifest — writes manifest (write guard warns if active) ✅ (114 files, manifest regenerated)
- [x] Run Dry Run Ingest — shows expected document counts without writing ✅ (113/114 processed, 1 known gap: owner_requirements needs client_code)
- [x] Run Ingest — ingests documents (write guard warns if active) ✅ (113/114 processed, 2 non-blocking errors)
- [x] Run Create Snapshot — creates readiness snapshot (write guard warns if active) ✅ (score 48.45 Behind)

### 5. Verify Results
- [x] Each operation displays result in section F (name, duration, response JSON) ✅ (verified via API)
- [ ] Pipeline status visualization updates after each operation (requires frontend browser)
- [x] Section G shows Sync Step Details (backend logs) and Session History (current tab) ✅ (verified via API)
- [ ] Document index metrics update after ingest/snapshot (requires frontend browser)
- [x] Next-actions CTAs suggest appropriate follow-up operations ✅ (verified in API response next_actions)

### 6. Write Guard
- [ ] Toggle write guard on (requires frontend browser)
- [ ] Confirm modal appears for write operations (requires frontend browser)
- [ ] Confirm cancel aborts the operation (requires frontend browser)
- [ ] Confirm accept proceeds with operation (requires frontend browser)
- [ ] Toggle write guard off (requires frontend browser)

### 7. Debug / Logs
- [x] Debug page shows timeline of operations with request_id, endpoint, status, duration ✅ (verified via API: /api/v1/debug/logs, /api/v1/debug/pipeline-state, /api/v1/debug/projects/1/timeline)
- [x] Filter by project ID works ✅ (verified via API)
- [x] Filter by operation type works ✅ (verified via API)
- [x] Filter by status works ✅ (verified via API)

### 8. Readiness Snapshots
- [x] Confirm evidence candidate semantics are visible ✅ (snapshot created: overall 48.45, MECHANICAL 99.89, ELECTRICAL 91.16)
