# Ribbon to Landing Workflow

## Core Flow

Revit command
-> Local landing folder operations
-> Export JSON plus metadata sidecar
-> Backend scan/rebuild/ingest through web app or API
-> PostgreSQL updates
-> Deterministic readiness refresh

## Document Intake

Folder-opening commands support:
- Drawings
- Owner Requirements
- Specifications
- Revit Exports / supporting

The add-in does not parse PDFs and does not run OCR/vision.

## Sync Boundary

- Revit add-in remains safe/local-first.
- Backend ingest is explicit operator action.
- Dev Mode and Processing pages are the recommended execution surface for scan/rebuild/ingest.
# Auto-Submit Decision (Current MVP)

Backend ingest remains operator-controlled from the web app (`Processing / Sync` and `Dev Mode`).
Revit export metadata may show `backend_ingestion_status = not_submitted` until an operator explicitly runs dry-run/ingest.
This is intentional for deterministic traceability in local-first workflows.
