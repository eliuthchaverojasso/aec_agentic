# Roadmap

## Completed Backend Readiness Hardening

- Phase 1 Owner Requirements hardening:
  - reference/link rows classified as `is_actionable=False`
  - `is_actionable` recalculated on re-ingest
  - non-actionable requirements excluded from readiness
- Phase 2 Readiness status semantics:
  - compliant = covered
  - non-compliant and needs-review = evaluated but not covered
  - not-applicable = excluded from applicable denominator
  - missing/no compliance row = missing
- Service readiness tests: 7 passed.
- Combined readiness + requirements suite: 18 passed.

## Current Product Focus

- Establish local-to-Azure deployment foundation for stakeholder review.
- Frontend / Dashboard Readiness MVP semantic cleanup.
- Align dashboard labels with backend readiness semantics.
- Do not label `requirements_evaluated` as Covered.
- Keep AI Query deferred until the deterministic dashboard semantics are clear.

## Pilot Candidate Hardening

- Repo hygiene and GitHub readiness
- Manifest-based landing ingestion
- Landing document scan/index foundation for Revit JSON, owner requirement Excel, drawing PDFs, specification PDFs, and project extract JSON
- Project/client binding without manual SQL
- Readiness snapshots and actions
- Requirement evidence MVP
- Modular rule engine foundation
- Revit non-blocking UX foundation
- Clean package validation

## Next Product Layer

- Persistent action workflow
- Rule execution audit trail
- More discipline rule packs
- Evidence engine improvements for model/manual evidence
- Trend views from readiness snapshots
- Frontend trend/history UX from readiness snapshots
- Local OCR/vision/title-block extraction adapter after security and dependency review
- Import/review workflow for SEION-KGE advisory predictions
- Deterministic conversion path from accepted SEION suggestions to official evidence/actions

## Deferred

- Full Drawing Reel / PDF OCR
- Live ACC integration
- UNANET integration
- GraphRAG
- Production AI Query

## SEION-KGE Status

SEION-KGE foundation is advisory only. Graph export, prediction storage, review status, and a dashboard advisory panel are allowed. SEION is not an official calculator and does not alter readiness until accepted suggestions are converted through deterministic backend workflows.
# SEION v0.1 Status

Implemented:

- SEION core mathematical primitives and audits.
- SEION-KGE JSONL loader, deterministic NumPy scorer/trainer, metrics, artifact save, candidate scoring, and prediction export.
- Advisory prediction importer and reviewer accept/reject status.

Deferred:

- Real E8 kernel.
- Production model serving.
- Converting accepted suggestions into official evidence through a reviewed deterministic workflow.
- AI Query and GraphRAG as production features.
