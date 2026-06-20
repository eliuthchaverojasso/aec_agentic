# EMA AI — Troubleshooting Matrix

## How to Use This Table

1. Identify the **Symptom** you see
2. Check the **Likely Cause** column
3. Run the **Diagnostic** command
4. Apply the **Fix** from the Remediation column

---

## Project Setup Issues

### Symptom: Project creation returns 400/422

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Missing required field in payload | Check response `detail` field | Include all required fields per `docs/api/PROJECT_CREATION_API.md` |
| Invalid `client_code` | `GET /api/v1/clients` | Use existing `code` value |
| Malformed JSON | `curl -s -v ...` | Validate JSON with `ConvertFrom-Json` round-trip in PowerShell |

### Symptom: `client_id` is null after bootstrap

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Client not in database | `GET /api/v1/clients` | Create client first via `POST /api/v1/clients` |
| `client_code` mismatch | Check bootstrap payload | Use exactly the stored `code` (case-sensitive) |

---

## Landing Discover Issues

### Symptom: Discover returns `ok: true` but `projects: []`

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Landing root path has no manifest | Check `has_manifest` field | Create `landing-manifest.json` in root first |
| Wrong landing_root in payload | Verify path exists: `Test-Path $path` | Use absolute Windows path with forward slashes |
| Backend running in Docker — path not mounted | `docker inspect ... .Mounts` | Add volume mount in `docker-compose.ai.yml` |

### Symptom: `has_manifest: false` but files exist

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Manifest file has wrong name | List files: `ls landing-root/` | Rename to `landing-manifest.json` |
| Manifest not valid JSON | `cat landing-root/landing-manifest.json \| ConvertFrom-Json` | Fix JSON syntax |

### Symptom: Discover reports `errors` with "Permission denied"

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Docker Desktop file sharing disabled | Docker Desktop → Settings → Resources → File Sharing | Add path to shared folders |
| Antivirus blocking access | Check antivirus logs | Exclude landing root from AV scanning |

---

## Bootstrap NISD Issues

### Symptom: Bootstrap returns `ok: false` with `errors`

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| `landing_root` not configured | `GET /api/v1/landing/status` | Set `LANDING_ROOT` env var and restart |
| Client does not exist | `GET /api/v1/clients?code=NISD` | Create client or use existing one |
| Project folder already exists | Check `folder_status` in response | Use different `project_folder_name` |

### Symptom: Bootstrap `ok: true` but project not in `GET /api/v1/projects`

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| DB transaction not committed | Check `project_id` in response | Refresh the list — may be a cache |
| Different database than expected | Check `DATABASE_URL` in backend logs | Ensure local and docker share same DB |

### Symptom: `project_landing_path` contains `/` and `\`

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Path normalization bug in backend | Check `project_landing_path` string | Report bug — frontend should normalize |

---

## Processing / Sync Issues

### Symptom: Export stays in `pending` status

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| No worker running | Check Docker containers: `docker ps` | Start Celery worker or processing service |
| Export failed silently | Check `logs` field in response | Fix underlying error (model access, file permissions) |

### Symptom: Export `completed` but `element_count` = 0

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| EMAExtractor not finding elements | Check extractor logs | Verify `.rvt` file is accessible |
| Element table not populated | `SELECT count(*) FROM element;` | Re-run export |

### Symptom: Readiness score is always 0

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| No active requirements linked | `GET /api/v1/projects/{id}/requirements` | Link client requirements first |
| All requirements `is_active: false` | Check requirements in DB | Activate requirements via admin |
| `client_id` missing on project | `GET /api/v1/projects/{id}` | Link project to client |

### Symptom: Sync freshness score is "Red" immediately after sync

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| `sync_freshness_score` threshold too aggressive | Check `app/readiness/scoring.py` | Adjust threshold if needed |
| `completed_at` not set on export | Check export in DB: `SELECT completed_at FROM export WHERE ...` | Fix export completion logic |

---

## Readiness / Scoring Issues

### Symptom: `overall_readiness` > 100 or < 0

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Weighted sum overflow | Check `weighted_readiness()` in `scoring.py` | Cap at 100 / floor at 0 |
| Negative penalty applied twice | Check `qaqc_health_score()` | Deduct only once in formula |

### Symptom: `trade_readiness` is empty but project has data

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| No active requirements for any discipline | `GET /api/v1/projects/{id}/requirements` | Add active requirements linked to client |
| All requirements `is_actionable: false` | Check `is_actionable` flag | Set `is_actionable = true` for relevant requirements |
| Model health fallback also empty (edge case) | Check open issues count | This is expected when no data exists |

### Symptom: `gap_summary` shows gaps but `recommended_actions` is empty

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| `recommended_actions()` logic filters them out | Check `app/readiness/rules.py` | Review action filtering criteria |
| All gaps are `not_evaluated` (no compliance data) | Check `compliance_by_requirement` | Run compliance evaluation first |

---

## Windows vs Docker Path Issues

### Symptom: Backend can't find landing files

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Docker mount path mismatch | `docker inspect <container> .Mounts` where `Destination == '/app/landing'` | Fix `Source` to match host path |
| Windows backslashes in container | Check if backend builds paths with `\` inside container | Use `os.path.normpath()` and forward slashes in containers |

### Symptom: Backend works locally but fails in Docker

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Different `DATABASE_URL` | Check backend logs on startup | Ensure Docker uses same PG instance |
| Different `.env` values | Compare `.env` locally vs Docker | Align env vars |
| CORS blocking requests | Check browser devtools Network tab | Update `cors_origins` in settings |

### Symptom: `ls /app/landing/` shows nothing in container

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Docker Desktop file sharing not configured | Docker Desktop → Settings → Resources → File Sharing | Add host path to shared list |
| Path not normalized (e.g. `C:\Documents\...` with spaces) | Use `docker run -v "C:/Documents/Hyperghaps EMA/landing:/app/landing"` | Quote the volume arg |

---

## Debug / Logs Issues

### Symptom: `/api/v1/dev/status` returns `500`

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Debug endpoint not implemented | Check `app/api/debug.py` exists | Implement per `docs/debug/` plan |
| Database unreachable | Check `database_health` field | Verify PG container is running |

### Symptom: No logs appear for a specific request

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Log level too high | Check `log_level` in settings | Set to `DEBUG` locally |
| Logs going to container stdout | `docker logs <container> --tail 100` | Redirect to file or use ELK stack |

---

## Frontend Issues

### Symptom: Readiness dashboard shows "-" for scores

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| Backend not returning scores | `GET /api/v1/projects/{id}/readiness` in browser | Verify API contract |
| Frontend data binding error | Browser devtools Console | Check computed property names |

### Symptom: Trade readiness rows missing disciplines

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| No requirements for that discipline | `GET /api/v1/projects/{id}/requirements` | Add requirements |
| Discipline name mismatch (case) | Compare `discipline` values to frontend lookup | Normalize with `.toUpperCase()` |

### Symptom: "No sync" shown after a completed sync

| Likely Cause | Diagnostic | Remediation |
|-------------|------------|-------------|
| `latest_sync_at` not passed to frontend | Check `ProjectReadinessOut` schema | Verify field mapping in backend service |
| Date parsing error in frontend | Browser devtools console | Check `formatDateTime()` and `timeAgo()` |
