# Landing API Contract

## Purpose

Landing APIs scan, classify, bind, and ingest local landing-root project folders in a deterministic way.

## Core Endpoints

- `GET /api/v1/landing/projects`
- `POST /api/v1/landing/rebuild-all-manifests`
- `POST /api/v1/landing/ingest-all`
- `POST /api/v1/landing/projects/{project_folder}/bind`
- `POST /api/v1/landing/scan`
- `POST /api/v1/landing/rebuild-manifest`
- `POST /api/v1/landing/ingest`

## Boundaries

- Dry-run should be the default for batch actions.
- Project failures must not stop the entire batch.
- Owner requirements require client binding or a clear actionable error.
- Sidecars are metadata, not documents.
- PDFs/specs/drawings are evidence candidates only.

