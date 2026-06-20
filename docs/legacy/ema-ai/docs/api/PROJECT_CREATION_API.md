# Project Creation API

Implemented endpoints:

- `POST /api/v1/projects`
- `GET /api/v1/projects/{project_id}`
- `PATCH /api/v1/projects/{project_id}`
- `POST /api/v1/projects/{project_id}/models`
- `POST /api/v1/projects/{project_id}/landing/configure`
- `GET /api/v1/projects/{project_id}/landing/status`
- `POST /api/v1/landing/projects/discover`
- `POST /api/v1/landing/projects/bootstrap-from-folder`
- `POST /api/v1/projects/{project_id}/files/register`

Processing aliases:

- `POST /api/v1/projects/{project_id}/landing/scan`
- `POST /api/v1/projects/{project_id}/landing/rebuild-manifest`
- `POST /api/v1/projects/{project_id}/landing/ingest/dry-run`
- `POST /api/v1/projects/{project_id}/landing/ingest`

All processing responses include operation metadata, warnings/errors, and next actions.
