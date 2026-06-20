# PDF Viewer (Serving Zone)

The dashboard now opens PDF previews through backend project-scoped routes:

- `GET /api/v1/projects/{project_id}/documents/{document_id}/pdf`
- `GET /api/v1/projects/{project_id}/documents/{document_id}/text`
- `GET /api/v1/projects/{project_id}/documents/{document_id}/metadata`

Viewer UX shows:

- Evidence Candidate badge
- parser status
- extracted text preview availability

If extraction is unavailable, UI explicitly states that status.

