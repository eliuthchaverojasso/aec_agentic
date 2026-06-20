# Projects API

## Implemented

### `GET /api/v1/projects`
- Purpose: list projects.

### `POST /api/v1/projects`
- Purpose: create project with client bind/create behavior.
- Status: Implemented.

### `GET /api/v1/projects/{project_id}`
- Purpose: fetch project summary.

### `PATCH /api/v1/projects/{project_id}`
- Purpose: update project metadata/binding fields.

### `POST /api/v1/projects/{project_id}/models`
- Purpose: create/bind model record.

## Safety Notes
- Keep IDs and model bindings traceable.
- Do not infer official compliance/readiness from creation operations.
