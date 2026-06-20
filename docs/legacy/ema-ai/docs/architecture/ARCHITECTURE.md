# EMA AI Architecture

For agent-oriented architecture memory, see `.ai/ARCHITECTURE_MAP.md`.

## Local MVP

```text
EMAExtractor Revit Add-in
-> standardized local landing zone
-> Revit JSON export + metadata sidecar
-> landing manifest / future index metadata
-> FastAPI ingestion
-> PostgreSQL
-> QA/QC rule findings
-> owner requirements
-> evidence
-> readiness score
-> React dashboard
```

## Backend Services

- `app/ingestion/` handles Revit JSON, owner requirements, manifest-based landing ingestion, and local document indexing for drawing/specification PDFs.
- `app/readiness/` computes readiness, gaps, actions, snapshots, and MVP evidence.
- `app/rules/` introduces the modular rule architecture while preserving legacy R001-R004 output compatibility.
- `app/api/` exposes projects, exports, issues, clients, readiness, landing, and document metadata endpoints.
- `app/seion/` exports official PostgreSQL facts for advisory SEION-KGE scoring and stores suggestions separately from official readiness.

## Data Model Highlights

- `project`, `model`, `export`, `element`, `issue`
- `client`, `requirement`, `requirement_compliance`
- `requirement_evidence`
- `landing_document`, `drawing_sheet`, `document_text_snippet`
- `readiness_snapshot`, `trade_readiness_snapshot`, `readiness_action`
- `rule_execution_log`

## Design Principle

Model Health explains what is technically wrong. Deliverable Readiness explains whether the project is ready for the next milestone and what actions are blocking it.

## Landing Zone Standard

Local project data lands under:

```text
landing/<PROJECT_DISPLAY_NAME>/
  Drawings/
  Owner Requirements/
  Specifications/
  Revit Exports/
  landing_manifest.json
```

Recommended normalized identity and file metadata fields:

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
- `checksum` optional/future
- `status`: `landing`, `processed`, `archived`, or `rejected`

Per-project `landing_manifest.json` is the intended project index shape, but the Revit add-in does not rewrite the full manifest in the current MVP. EMAExtractor writes a small `.meta.json` sidecar next to each Revit export with project identity, source system, export profile, output path, relative landing path, element count, and `backend_ingestion_status = not_submitted`.

Manual files keep their original filenames for now. Future manifest/index processing should map `original_filename` to a standardized name without renaming real project files by default.

Current backend scan/index support can register Revit JSON, owner requirement Excel, drawing PDFs, specification PDFs, and auxiliary project extract JSON from existing landing folders. PDF handling is local metadata/page-count/text-preview only when lightweight local parser dependencies are installed. OCR/vision extraction is a future local adapter and no external AI/vision provider is called. Indexed drawings/specifications are evidence candidates, not official evidence.

## Current Readiness State

Implemented:

- Owner requirement reference/link rows are classified as non-actionable.
- Non-actionable requirements are excluded from readiness calculations.
- Requirement coverage means covered/applicable, not evaluated/total.

Current product focus:

- Local-to-Azure deployment foundation documentation.
- Frontend / Dashboard Readiness MVP semantic cleanup.

Deferred:

- AI Query.
- GraphRAG.
- Azure deployment execution. Azure pilot is planned, not deployed.

## SEION-KGE Advisory Layer

SEION-KGE is an advisory integration foundation, not an official readiness calculator. EMA AI can export `entities.jsonl` and `triples.jsonl` from PostgreSQL facts, then store scored relationship suggestions in `seion_prediction`.

Predictions are not official compliance and are ignored by readiness scoring until a reviewer or deterministic backend workflow converts an accepted suggestion into official evidence, compliance, or action records.
# SEION v0.1 Advisory Layer

SEION v0.1 adds a separate mathematical/KGE package under `Pipeline/pipeline/app/seion_core/` and keeps the EMA bridge under `Pipeline/pipeline/app/seion/`.

Boundary:

- PostgreSQL remains the official source of truth.
- The deterministic readiness engine remains the official readiness calculator.
- SEION graph exports, KGE rankings, audits, and imported predictions are advisory only.
- SEION does not create official compliance, evidence, issue closure, or readiness records automatically.
- AI Query and GraphRAG remain deferred.
