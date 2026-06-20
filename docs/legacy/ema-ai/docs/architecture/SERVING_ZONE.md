# Serving Zone (Local-First)

EMA AI now uses a serving-zone pattern for indexed artifacts:

- Landing Zone: raw project files under configured landing root.
- Processing Zone: ingestion/classification/metadata extraction.
- Serving Zone: backend-controlled preview/metadata APIs by `project_id` + `document_id`.

## Security boundaries

- No arbitrary absolute path from frontend.
- Backend resolves file paths from indexed records only.
- Path traversal is rejected by landing path resolver.
- PDF inline preview is allowed only through backend `/pdf` endpoint for PDF records.
- Raw download is disabled by default (`enable_document_download=false`).

## Current MVP endpoints

- `GET /api/v1/projects/{project_id}/documents/{document_id}`
- `GET /api/v1/projects/{project_id}/documents/{document_id}/metadata`
- `GET /api/v1/projects/{project_id}/documents/{document_id}/preview`
- `GET /api/v1/projects/{project_id}/documents/{document_id}/text`
- `GET /api/v1/projects/{project_id}/documents/{document_id}/pdf`
- `GET /api/v1/projects/{project_id}/documents/{document_id}/download` (gated by config)

All indexed documents remain evidence candidates unless a deterministic evidence workflow marks official evidence.

