# Readiness Architecture

## Source of Truth
PostgreSQL records + deterministic readiness service.

## Inputs
- Requirements and coverage/evaluation state.
- QA/QC issue severity and counts.
- Sync freshness and ingest recency.

## Output
- Overall readiness score/label.
- Trade readiness rows.
- Gaps and recommended actions.

## Boundary
AI/SEION cannot approve or mutate official readiness directly.
