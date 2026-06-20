# Dashboard API Contract

Status: Current frontend/backend contract. Backend schemas in `Pipeline/pipeline/app/schemas.py` remain the source of truth. Frontend types must follow the backend response shape.

## Base URL

Local backend:

```text
http://localhost:8010
```

Frontend environment:

```text
VITE_API_BASE_URL
```

If `VITE_API_BASE_URL` is not set, the frontend falls back to `http://localhost:8010`.

## Deployment Configuration

| Environment | Frontend / Dashboard | Backend API | CORS Rule |
| --- | --- | --- | --- |
| Local | `http://localhost:5173` | `http://localhost:8010` | Allow `http://localhost:5173` and `http://127.0.0.1:5173` |
| Azure pilot | Azure Static Web Apps or App Service URL | Azure Container Apps or App Service URL | Allow only the approved pilot frontend origin |
| Enterprise | Approved custom domain | Approved API domain or private route where applicable | Lock to approved custom domains and enterprise networking policy |

For local development, `VITE_API_BASE_URL` is optional because the frontend falls back to `http://localhost:8010`. In Azure, set `VITE_API_BASE_URL` to the approved backend URL and set backend `CORS_ORIGINS` to the approved frontend/dashboard URL.

The dashboard is the existing React frontend initially. Do not introduce a separate dashboard backend unless the backend contract and deployment architecture are explicitly changed later.

## Endpoints

| Endpoint | Purpose | Frontend usage | High-level response shape |
| --- | --- | --- | --- |
| `GET /health` | Backend and database health check. | Startup/demo validation. | `{ status, database, version }` |
| `GET /api/v1/projects` | Portfolio project list with summary KPIs. | `ProjectsPage`, project selector, dashboard bootstrap. | `ProjectSummary[]` |
| `GET /api/v1/issues` | Paginated issue list with filters. | `IssuesPage`, overview issue counts. | `{ total, page, page_size, items }` |
| `GET /api/v1/projects/{project_id}/readiness` | Deterministic computed readiness for one project. | Overview, trade readiness, requirements summary. | `ProjectReadinessOut` |
| `POST /api/v1/projects/{project_id}/readiness/recalculate` | Recalculate and persist a readiness snapshot. | Traceability workflow; not used as an automatic frontend mutation in the current cleanup. | `ReadinessSnapshotOut` |
| `GET /api/v1/projects/{project_id}/readiness/snapshots` | List persisted readiness snapshots. | Trend/history views. | `ReadinessSnapshotOut[]` |
| `GET /api/v1/projects/{project_id}/readiness/actions` | List readiness actions for a project. | Recommended actions. | `ReadinessActionOut[]` |
| `PATCH /api/v1/readiness/actions/{action_id}` | Update readiness action status, owner, priority, or description. | Future action workflow. | `ReadinessActionOut` |
| `GET /api/v1/clients/{client_id}/requirements` | Owner requirement catalog for a client. | Requirements page and requirement search. | `{ client_id, total, page, page_size, items }` |
| `GET /api/v1/exports` | Export records. | Latest export and processing context. | `ExportOut[]` |
| `GET /api/v1/exports/{export_id}/sync-logs` | Sync log rows for an export. | Processing page. | `SyncLogOut[]` |
| `GET /api/v1/models/{model_id}/health` | Model health summary. | Model Health page. | `ModelHealth` |
| `POST /api/v1/landing/scan` | Scan local landing folders and preview optional manifest updates. | Processing/admin workflow. | `LandingScanReport` |
| `POST /api/v1/landing/rebuild-manifest` | Rebuild one project `landing_manifest.json` from discovered local files. | Processing/admin workflow. | `LandingScanReport` |
| `POST /api/v1/landing/ingest` | Manifest-driven ingestion for JSON, Excel, PDF metadata, and bindings. | Processing/admin workflow. | `LandingIngestReport` |
| `GET /api/v1/landing/projects` | Discover all landing-root project folders and classify files. | Processing / Sync landing inventory + Project Setup binding. | `LandingProjectsDiscoveryResponse` |
| `POST /api/v1/landing/rebuild-all-manifests` | Rebuild manifests for all or selected landing projects (dry-run default). | Processing / Sync global actions. | `LandingManifestBatchResponse` |
| `POST /api/v1/landing/ingest-all` | Batch ingest all or selected landing projects (dry-run default, partial-failure safe). | Processing / Sync global actions. | `LandingIngestAllResponse` |
| `POST /api/v1/landing/projects/{project_folder}/bind` | Explicitly bind landing folder to project/client/milestone. | Project Setup binding workflow. | `LandingProjectBindResponse` |
| `GET /api/v1/projects/{project_id}/documents` | List indexed landing documents for one project. | Overview and Processing document panels. | `LandingDocumentOut[]` |
| `GET /api/v1/projects/{project_id}/documents/{document_id}` | Get project-scoped document metadata. | Document viewer context. | `LandingDocumentOut` |
| `GET /api/v1/projects/{project_id}/documents/{document_id}/metadata` | Explicit project-scoped metadata route. | Document viewer metadata tab. | `LandingDocumentOut` |
| `GET /api/v1/projects/{project_id}/documents/{document_id}/preview` | Safe serving-zone preview metadata. | Document preview drawer. | `DocumentPreviewOut` |
| `GET /api/v1/projects/{project_id}/documents/{document_id}/text` | Project-scoped extracted text preview. | Document preview drawer text tab. | `DocumentTextPreviewOut` |
| `GET /api/v1/projects/{project_id}/documents/{document_id}/pdf` | Inline PDF route for PDF records only. | Embedded PDF viewer iframe. | `application/pdf` |
| `GET /api/v1/projects/{project_id}/documents/{document_id}/download` | Raw file download route, config-gated. | Optional operator workflow. | file stream or 403 |
| `GET /api/v1/projects/{project_id}/drawings` | List indexed drawing PDFs/sheets. | Drawing table. | `LandingDocumentOut[]` |
| `GET /api/v1/projects/{project_id}/specifications` | List indexed specification PDFs. | Specification table. | `LandingDocumentOut[]` |
| `GET /api/v1/debug/logs` | Query pipeline/frontend diagnostic logs. | Debug / Logs page. | `{ items, count, limit, offset }` |
| `GET /api/v1/debug/logs/{log_id}` | Get one diagnostic log record. | Debug detail drawer. | `DebugLogOut` |
| `GET /api/v1/debug/logs/summary` | Summary counters for latest diagnostics. | Debug summary cards + System Health. | `{ total, errors, warnings, ... }` |
| `POST /api/v1/debug/logs/frontend` | Persist user-triggered frontend operation event. | Processing/Project Setup operation instrumentation. | `{ ok, log_id, request_id, run_id }` |
| `GET /api/v1/debug/environment` | Runtime diagnostics and path-mapping warnings. | System Health + Debug / Logs environment panel. | `{ landing_dir, file_path_mode, warnings, ... }` |
| `GET /api/v1/debug/pipeline-state` | Snapshot of recent operations and latest scan/ingest state. | Debug / Logs pipeline state panel. | `{ summary, latest_scan, latest_ingest, ... }` |
| `GET /api/v1/debug/projects/{project_id}/timeline` | Project-scoped operation timeline. | Project debug timeline workflows. | `{ project_id, items }` |
| `POST /api/v1/debug/bundle` | Redacted local debug bundle payload. | Debug bundle export/copy workflow. | redacted JSON diagnostics bundle |
| `GET /api/v1/projects/{project_id}/viewpoints` | List viewpoint metadata documents. | 3D / Viewpoints workflow. | `LandingDocumentOut[]` |
| `GET /api/v1/projects/{project_id}/viewpoints/{viewpoint_id}` | Get viewpoint metadata document detail. | Viewpoint detail drawer. | `LandingDocumentOut` |
| `GET /api/v1/documents/{document_id}` | Get document metadata. | Document detail/drilldown. | `LandingDocumentOut` |
| `GET /api/v1/documents/{document_id}/text-preview` | Get capped local text preview, if stored. | Preview workflow only. | `DocumentTextPreviewOut` |
| `GET /api/v1/dev/status` | Local aggregated operator status. | Dev Mode + System Health. | `DevStatusOut` |
| `POST /api/v1/dev/smoke-test` | Safe read-only local endpoint checks. | Dev Mode smoke operation. | `DevSmokeTestOut` |
| `POST /api/v1/seion/export-graph` | Export PostgreSQL facts to SEION-KGE JSONL. | Admin/reviewer tooling, not automatic dashboard scoring. | `SeionGraphExportOut` |
| `GET /api/v1/projects/{project_id}/seion/suggestions` | List advisory SEION-KGE suggestions. | Overview advisory panel. | `SeionPredictionOut[]` |
| `POST /api/v1/seion/suggestions/{prediction_id}/accept` | Mark an advisory suggestion accepted. | Reviewer action; does not create official compliance by itself. | `SeionPredictionOut` |
| `POST /api/v1/seion/suggestions/{prediction_id}/reject` | Mark an advisory suggestion rejected. | Reviewer action. | `SeionPredictionOut` |
| `GET /api/v1/compliance/status` | Local compliance module status. | Compliance overview cards. | `ComplianceStatusOut` |
| `GET /api/v1/compliance/corpora` | List imported compliance corpora. | Code Corpus page. | `ComplianceCorpusOut[]` |
| `GET /api/v1/compliance/corpora/{corpus_id}` | Get corpus detail. | Code Corpus detail. | `ComplianceCorpusOut` |
| `POST /api/v1/compliance/corpora/nec/preview` | Preview NEC structured corpus before import. | Code Loader preview. | `ComplianceLoaderPreviewOut` |
| `POST /api/v1/compliance/corpora/nec/import` | Import NEC corpus as candidate rules. | Code Loader import action. | `ComplianceImportOut` |
| `GET /api/v1/compliance/rules` | List compliance candidate/active rules. | Rule Catalog page. | `ComplianceRuleOut[]` |

## Readiness Response Notes

`ProjectReadinessOut` includes:

- `overall_readiness`
- `label`
- `requirement_coverage`
- `qaqc_health`
- `sync_freshness`
- `open_issues`
- `trade_readiness`
- `gap_summary`
- `top_gaps`
- `recommended_actions`

For live `trade_readiness[]`, the current response exposes `requirements_evaluated`, not a true covered count. The frontend must label this field as Evaluated.

Readiness snapshots expose `trade_readiness[].requirements_covered` separately through `TradeReadinessSnapshotOut`. Do not assume that field exists in the live readiness response unless the backend schema adds it.

## Deferred

- AI Query endpoints are not implemented.
- GraphRAG endpoints are not implemented.
- New dashboard backend fields should be added only through backend schema changes and tests.

## SEION-KGE Advisory Contract

SEION-KGE suggestions are advisory. They are not official readiness, not official compliance, and not included in readiness scoring. Accepting a suggestion currently updates only the `seion_prediction` status; official evidence/compliance writes require a separate deterministic workflow.

## Upload Contract Notes

Future web app upload should land files into the same logical categories used by the local landing zone:

- `Drawings`
- `Owner Requirements`
- `Specifications`
- `Revit Exports`
- `Supporting` when a file does not fit a primary category

Recommended upload metadata:

- `client_code`
- `project_code`
- `project_display_name`
- `project_folder_name`
- `project_slug`
- `source_system`
- `document_category`
- `discipline`
- `upload_method`
- `received_at` or `exported_at`
- `original_filename`
- `standardized_filename`
- `status`

Web upload is not implemented in this pass. The current Revit add-in path remains file-based export to the configured local `Revit Exports` folder, with backend ingestion performed manually.

## Landing Documents Contract

Landing document endpoints are project-scoped serving-zone routes. They do not accept arbitrary filesystem paths. PDF rows may be labeled `Evidence candidate`; they must not be displayed as official evidence unless a backend source-of-truth evidence record says so. Indexed PDFs do not change readiness scoring.
# SEION Advisory API

SEION endpoints are advisory and are not part of official readiness scoring:

- `POST /api/v1/seion/export-graph`
- `POST /api/v1/seion/import-predictions`
- `GET /api/v1/projects/{project_id}/seion/suggestions`
- `POST /api/v1/seion/suggestions/{prediction_id}/accept`
- `POST /api/v1/seion/suggestions/{prediction_id}/reject`

`import-predictions` accepts server-local `.jsonl` files only from the SEION artifacts directory and stores rows in `seion_prediction` with `status='suggested'`. Accept/reject updates review status only; it does not modify compliance, evidence, issue status, or readiness scores.
# Project Setup + Landing Sync Additions

New/confirmed workflow endpoints:

- `POST /api/v1/projects`
- `GET /api/v1/projects/{project_id}`
- `PATCH /api/v1/projects/{project_id}`
- `POST /api/v1/projects/{project_id}/models`
- `POST /api/v1/projects/{project_id}/landing/configure`
- `GET /api/v1/projects/{project_id}/landing/status`
- `POST /api/v1/landing/projects/discover`
- `POST /api/v1/landing/projects/bootstrap-from-folder`
- `POST /api/v1/projects/{project_id}/files/register`
- `POST /api/v1/projects/{project_id}/landing/scan`
- `POST /api/v1/projects/{project_id}/landing/rebuild-manifest`
- `POST /api/v1/projects/{project_id}/landing/ingest/dry-run`
- `POST /api/v1/projects/{project_id}/landing/ingest`

Processing responses include: `ok`, `operation`, `project_id`, `project_name`, `project_folder_name`, `endpoint`, `dry_run`, `counts`, `warnings`, `errors`, `next_actions`.
