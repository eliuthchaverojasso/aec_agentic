# Processing / Sync API

## Implemented

### `POST /api/v1/projects/{project_id}/landing/scan`
### `POST /api/v1/projects/{project_id}/landing/rebuild-manifest`
### `POST /api/v1/projects/{project_id}/landing/ingest/dry-run`
### `POST /api/v1/projects/{project_id}/landing/ingest`
### `POST /api/v1/projects/{project_id}/readiness/snapshots`

## Response Shape (Target)
- `ok`
- `operation`
- `project_id`
- `project_name`
- `project_folder_name`
- `endpoint`
- `dry_run` (where applicable)
- `counts`
- `warnings`
- `errors`
- `next_actions`
