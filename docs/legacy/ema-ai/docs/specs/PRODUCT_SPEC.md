# EMA AI Product Spec

EMA AI is an Engineering Intelligence / Deliverable Readiness platform.

## Canonical Flow

Revit Export -> FastAPI Ingestion -> PostgreSQL -> QA/QC Rules -> Owner Requirements -> Evidence -> Readiness Score -> React Dashboard.

## Product Boundaries

- PostgreSQL is the official source of truth.
- The Readiness Engine is deterministic.
- AI may explain, summarize, search, and draft suggestions.
- AI must not approve official readiness or compliance.
- SEION-KGE is advisory and does not change official readiness directly.

## Deferred

- Production AI Query.
- GraphRAG.
- Live ACC integration.
- UNANET integration.
- Production auth/roles.
- Full PDF/OCR Drawing Reel.

