# Readiness API

## Implemented

### `GET /api/v1/projects/{project_id}/readiness`
- Deterministic computed readiness response.

### `POST /api/v1/projects/{project_id}/readiness/recalculate`
- Recalculate and persist readiness snapshot.

### `GET /api/v1/projects/{project_id}/readiness/snapshots`
- Historical snapshots.

### `GET /api/v1/projects/{project_id}/readiness/actions`
### `PATCH /api/v1/readiness/actions/{action_id}`
- Action workflow.

## Semantic Boundaries
- `requirements_evaluated` is not equivalent to covered/compliant.
- AI/SEION advisory output is excluded from official readiness calculation.
