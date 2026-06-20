# Documents API

## Implemented / Partial

### `GET /api/v1/projects/{project_id}/documents`
- List indexed landing documents.

### `GET /api/v1/projects/{project_id}/documents/{document_id}`
- Get document metadata.

### `GET /api/v1/projects/{project_id}/documents/{document_id}/preview`
- Safe preview payload.

### `GET /api/v1/projects/{project_id}/documents/{document_id}/text`
- Extracted text preview if available.

### `GET /api/v1/projects/{project_id}/documents/{document_id}/pdf`
- PDF inline route for PDF records only.

### `POST /api/v1/projects/{project_id}/files/register`
- Register existing landing files as evidence candidates.

### `POST /api/v1/projects/{project_id}/files/upload`
- Status: Partial/Planned depending on runtime branch support.

## Semantics
- Default: `evidence_status=candidate`, `official_evidence=false`.
