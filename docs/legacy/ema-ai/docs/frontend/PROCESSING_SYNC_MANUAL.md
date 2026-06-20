# Processing / Sync Manual

## Purpose

Operator guide for the local landing workflow, ingestion controls, and readiness refresh loop.

## Main Tasks

- Scan landing root
- Rebuild manifests
- Dry-run ingest
- Run ingest with confirmation
- Create readiness snapshots
- Review logs and warnings

## Caveats

- Local demo only.
- No automatic ingest.
- Backend connection can be unavailable; the UI should show that honestly.

