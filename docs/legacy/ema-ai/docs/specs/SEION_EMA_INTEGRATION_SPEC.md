# SEION EMA Integration Spec

## Flow

```text
PostgreSQL facts
-> SEION graph export
-> advisory KGE scoring
-> predictions.jsonl
-> seion_prediction import
-> reviewer accept/reject status
```

## Implemented

- `POST /api/v1/seion/export-graph`
- `POST /api/v1/seion/import-predictions`
- `GET /api/v1/projects/{project_id}/seion/suggestions`
- `POST /api/v1/seion/suggestions/{prediction_id}/accept`
- `POST /api/v1/seion/suggestions/{prediction_id}/reject`

## Import Safety

The API imports only `.jsonl` files under the configured SEION artifacts directory. Path traversal and arbitrary filesystem import are rejected.

## Official Data Boundary

Importing or accepting a SEION prediction does not create `RequirementCompliance`, does not create official `RequirementEvidence`, does not close issues, and does not change readiness scores.
