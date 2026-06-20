# EMA AI Demo Runbook

## Start

```cmd
start_ema_ai_demo.cmd
```

Expected URLs:

```text
Backend Health: http://localhost:8010/health
Swagger:        http://localhost:8010/docs
Dashboard:      http://localhost:5173
```

Current local ports:

- Backend: `8010`
- Frontend: `5173`
- Swagger: `/docs`

## Seed

```cmd
seed_demo_data.cmd
```

Use dry run first when validating a new package:

```cmd
seed_demo_data.cmd -DryRun
```

## Validate

```cmd
validate_ema_ai_demo.cmd
```

## Five-Minute Narrative

Primary presenter script:

- `docs/demo/EMA_AI_5_MINUTE_DEMO_SCRIPT.md`

1. Open EMA AI Dashboard.
2. Show Projects Portfolio and at-risk status.
3. Open the project Deliverable Tracker.
4. Explain deterministic readiness score, requirement coverage, gap summary, recommended actions, and Landing Documents.
5. Show that drawing/specification PDFs are indexed as evidence candidates, not official evidence.
6. Open Requirements and show evidence/status workflow.
7. Open Issues and show QA/QC traceability.
8. Open Processing/Sync and show ingestion plus document index status.
8.1. Show the 30-second read-only heartbeat indicator and operator-controlled write guard.
8.2. Open Model / Viewer and show registered package status plus APS-not-configured placeholder.
9. Open Dev Mode and run `dev/status` plus `dev/smoke-test`.
9. Close with: Model Health shows what is wrong; Readiness shows whether the project is ready to deliver.

## Demo Safety Checks

- No dead buttons.
- No AI Query as the primary story.
- Fallback/demo fields remain centralized and labeled.
- Readiness, issues, requirements, exports, and sync status come from backend data where available.
- AI Query and GraphRAG are not implemented.
- Azure pilot is not deployed.
- Requirement coverage means covered/applicable; evaluated counts must not be described as covered.
- Indexed drawing/specification PDFs must not be described as compliant or official evidence.
- Viewer package registration (DWFx/RVT/NWD/IFC/SVF) must not be described as official evidence.
- APS viewer must be described as future/configurable unless a secure backend APS flow is active.
- OCR/vision is not production-ready and no external AI/vision service is called.
# Optional SEION Advisory Demo

The SEION path is optional and advisory:

```powershell
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
Invoke-RestMethod -Method Post http://localhost:8010/api/v1/seion/export-graph
py -3.12 -m app.seion_core.kge_train --entities .\Pipeline\pipeline\artifacts\seion\entities.jsonl --triples .\Pipeline\pipeline\artifacts\seion\triples.jsonl --out .\Pipeline\pipeline\artifacts\seion\model --dim 64 --epochs 5 --neg-k 16
```

Predictions imported into `seion_prediction` are suggestions for review only. They do not change readiness.
