# Readiness Dashboard MVP

Status: Current frontend exists; this is not a greenfield dashboard.

## Current Stack

- React
- Vite
- TypeScript
- Tailwind
- Recharts
- Lucide

Frontend path:

```text
Pipeline/pipeline/frontend
```

## Current Pages

- `ProjectsPage`
- `ProjectOverviewPage`
- `TradeReadinessPage`
- `RequirementsPage`
- `IssuesPage`
- `ModelHealthPage`
- `ProcessingPage`

## Current API Client

The existing frontend API client consumes:

- `/api/v1/projects`
- `/api/v1/exports`
- `/api/v1/issues`
- `/api/v1/clients`
- `/api/v1/clients/{client_id}/requirements`
- `/api/v1/projects/{project_id}/readiness`
- `/api/v1/projects/{project_id}/readiness/snapshots`
- `/api/v1/projects/{project_id}/readiness/actions`
- `/api/v1/exports/{export_id}/sync-logs`
- `/api/v1/models/{model_id}/health`

## First MVP Cleanup Objective

Align UI labels with backend readiness semantics.

Current backend semantics:

- `compliant` = covered.
- `non_compliant` = evaluated but not covered.
- `needs_review` = evaluated, visible, not covered.
- `not_applicable` = excluded from applicable denominator.
- Missing/no compliance row = missing.
- Non-actionable requirements are excluded entirely.

## Critical UI Rule

`requirements_evaluated` must be labeled Evaluated, not Covered.

Only use Covered when the backend exposes a true covered/compliant value.

## First Frontend Build Pass

Recommended commit:

```text
fix(frontend): align readiness dashboard labels with backend semantics
```

Scope should remain frontend-only unless a confirmed API contract gap is found.

## Do Not Add Yet

- AI Query.
- GraphRAG.
- New backend endpoints.
- Backend schema changes.
- Mutation workflows for official readiness/compliance approval.

## SEION Advisory Panel

The overview page may show `SEION Suggestions` when the advisory API is available. These rows must be labeled "Advisory suggestion - requires reviewer acceptance." They are not official readiness, not compliance, and must not be included in score displays.
