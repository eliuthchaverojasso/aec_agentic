# Processing / Sync Architecture

## Purpose
Operator-controlled pipeline from landing discovery to deterministic ingest and readiness snapshots.

## Operations
- Discover/bootstrap
- Scan landing
- Rebuild manifest
- Dry-run ingest
- Real ingest
- Snapshot creation

## Observability
Each operation should provide structured response metadata and debug log links (`request_id`, `run_id`, `operation_log_id` where available).
