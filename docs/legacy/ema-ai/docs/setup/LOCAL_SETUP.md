# Local Setup

Status: Local developer and demo guidance. These commands do not create Azure resources.

## Prerequisites

- Docker Desktop.
- Node.js with `npm.cmd`.
- Python 3.11+ or 3.12 for local tests.
- PowerShell.
- Optional .NET/Revit tooling for EMAExtractor add-in work.

## Local Service Map

| Service | Local Name / URL | Notes |
| --- | --- | --- |
| PostgreSQL container | `ema_postgres` | Docker Compose service `postgres` |
| FastAPI container | `ema_api` | Docker Compose service `api` |
| Backend URL | `http://localhost:8010` | Host port maps to container port `8000` |
| Swagger | `http://localhost:8010/docs` | Local API documentation |
| Frontend/dashboard URL | `http://localhost:5173` | React/Vite dashboard |
| Database host | `localhost` | From host machine |
| Database port | `5432` | Local Docker Compose default |
| Database name | `ema_ai` | Local/demo default |
| Database user | `ema` | Local/demo default |
| Database password | `ema_dev_pw` | Local/demo only; do not reuse for shared or production environments |

## Backend

```powershell
cd Pipeline\pipeline
docker compose up -d --build
docker compose ps
curl.exe http://localhost:8010/health
```

Local backend:

```text
http://localhost:8010
```

Swagger:

```text
http://localhost:8010/docs
```

## Local Database Access

```powershell
cd Pipeline\pipeline
docker exec -it ema_postgres psql -U ema -d ema_ai
```

Inside `psql`:

```sql
\dt
```

Exit `psql`:

```sql
\q
```

## Frontend

```powershell
cd Pipeline\pipeline\frontend
npm.cmd install
npm.cmd run dev
```

Local dashboard:

```text
http://localhost:5173
```

Build:

```powershell
npm.cmd run build
```

Frontend API base URL:

- `VITE_API_BASE_URL`, when set.
- `http://localhost:8010`, when unset.

## Local Seed and Demo

Start local backend and dashboard:

```cmd
start_ema_ai_demo.cmd
```

Seed demo data from the local landing manifest:

```cmd
seed_demo_data.cmd
```

Dry run seed validation:

```cmd
seed_demo_data.cmd -DryRun
```

Scan or rebuild a project landing manifest from existing local folders:

```powershell
curl.exe -X POST http://localhost:8010/api/v1/landing/scan -H "Content-Type: application/json" -d "{\"project_folder\":\"ROCHELL ES\",\"dry_run\":true,\"include_pdf_metadata\":true}"
curl.exe -X POST http://localhost:8010/api/v1/landing/rebuild-manifest -H "Content-Type: application/json" -d "{\"project_folder\":\"ROCHELL ES\",\"preserve_existing\":true}"
```

Run a dry-run ingest before writing records:

```powershell
curl.exe -X POST http://localhost:8010/api/v1/landing/ingest -H "Content-Type: application/json" -d "{\"manifest_path\":\"ROCHELL ES/landing_manifest.json\",\"dry_run\":true}"
```

Validate demo endpoints:

```cmd
validate_ema_ai_demo.cmd
```

Optional operator checks:

```powershell
Invoke-RestMethod http://localhost:8010/api/v1/dev/status | ConvertTo-Json -Depth 20
Invoke-RestMethod -Method Post http://localhost:8010/api/v1/dev/smoke-test | ConvertTo-Json -Depth 20
```

Landing update workflow reference:

- `docs/landing/LANDING_FILE_UPDATE_WORKFLOW.md`

Use a clean reset when you need a fresh local PostgreSQL volume, such as before a new pilot validation run or after repeated ingestion of the same payload.

Warning: this removes the local database volume and all local demo data.

```powershell
cd Pipeline\pipeline
docker compose down -v
```

## Local Backup and Restore

Local/demo backup example:

```powershell
docker exec ema_postgres pg_dump -U ema -d ema_ai -F c -f /tmp/ema_ai_local.dump
docker cp ema_postgres:/tmp/ema_ai_local.dump .\ema_ai_local.dump
```

Local/demo restore example:

```powershell
docker cp .\ema_ai_local.dump ema_postgres:/tmp/ema_ai_local.dump
docker exec -it ema_postgres pg_restore -U ema -d ema_ai --clean --if-exists /tmp/ema_ai_local.dump
```

These examples are for local/demo data only. Enterprise backup and restore should use Azure Database for PostgreSQL Flexible Server backup policies and approved operational runbooks.

## Revit Add-in

Optional add-in build command for explicitly scoped Revit work:

```powershell
dotnet build EMAExtractor\EMAExtractor.csproj --configuration Debug -p:Platform=x64 --no-restore
```

### Local Landing Zone Standard

The local landing root is configured in the EMAExtractor Settings window. It is intentionally not hardcoded in the add-in.

Canonical project folder shape:

```text
landing/<PROJECT_DISPLAY_NAME>/
  Drawings/
  Owner Requirements/
  Specifications/
  Revit Exports/
  landing_manifest.json
```

Current local project folder names remain unchanged:

- `DENTON HS`
- `NORTHWEST MS 8`
- `ROCHELL ES`

Files can enter the landing zone through three paths:

- Manual local placement: PDFs to `Drawings/`, XLSX to `Owner Requirements/`, PDFs/DOCX to `Specifications/`, and JSON exports to `Revit Exports/`.
- Revit plugin export: EMAExtractor writes Revit JSON plus `.meta.json` sidecar files to `Revit Exports/`.
- Future web app upload: upload should target the same categories or the Azure Data Lake equivalent. This is not implemented in the current MVP.

Backend scan/index support registers drawing and specification PDFs as indexed documents/evidence candidates. It does not make them official evidence and does not change deterministic readiness by itself.

Revit export naming:

```text
<project_slug>__revit_export__<discipline>__<scope>__<yyyyMMdd_HHmmss>.json
<project_slug>__revit_export__<discipline>__<scope>__<yyyyMMdd_HHmmss>.meta.json
```

Project slug rules: lowercase, trimmed, spaces become underscores, invalid filename characters are removed, repeated underscores collapse, numbers are preserved, and empty values become `ema_project`.

Current MVP rule: keep original manually placed PDFs/XLSX/DOCX. Use manifest or index metadata later to map `original_filename` to `standardized_filename`; do not rename real project files during local setup.

## Common Windows Note

If CMD opens in `C:\Users\...`, use the current repo path:

```cmd
cd /d "C:\Documents\Hyperghaps EMA\EMA-AI"
```
# SEION v0.1 Local Commands

Run from repo root:

```powershell
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
py -3.12 -m pytest .\Pipeline\pipeline\tests\test_seion_core.py .\Pipeline\pipeline\tests\test_seion_kge.py .\Pipeline\pipeline\tests\test_seion_importer.py -v
py -3.12 -m app.seion_core.kge_train --entities .\Pipeline\pipeline\artifacts\seion\entities.jsonl --triples .\Pipeline\pipeline\artifacts\seion\triples.jsonl --out .\Pipeline\pipeline\artifacts\seion\model --dim 64 --epochs 5 --neg-k 16
```

SEION is advisory only and is not required for deterministic readiness scoring.
