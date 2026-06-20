# Debug API

## Implemented (Branch Local MVP)
- `GET /api/v1/debug/logs`
- `GET /api/v1/debug/logs/{log_id}`
- `GET /api/v1/debug/logs/summary`
- `POST /api/v1/debug/logs/frontend`
- `GET /api/v1/debug/environment`
- `GET /api/v1/debug/pipeline-state`
- `GET /api/v1/debug/projects/{project_id}/timeline`
- `POST /api/v1/debug/bundle`

## Safety
- Redact secret-like keys and sensitive values.
- Exclude raw project file content.
- Diagnostics are operational, not compliance/audit sign-off records.
