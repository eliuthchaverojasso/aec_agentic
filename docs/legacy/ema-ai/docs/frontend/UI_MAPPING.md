# Frontend UI Mapping

Status: Current dashboard mapping guide.

Backend schemas in `Pipeline/pipeline/app/schemas.py` remain the source of truth.

## Readiness Field Mapping

| Backend field | UI label | Notes |
| --- | --- | --- |
| `overall_readiness` | Overall Readiness | Composite deterministic score. |
| `requirement_coverage.score` | Requirement Coverage | Covered/applicable, not evaluated/total. |
| `qaqc_health.score` | QA/QC Health | Model issue health component. |
| `sync_freshness.score` | Sync Freshness | Latest completed sync freshness. |
| `trade_readiness[].discipline` | Discipline | Trade/discipline grouping. |
| `trade_readiness[].readiness` | Trade Readiness | Trade-level readiness score. |
| `trade_readiness[].requirements_total` | Applicable Requirements | Active/actionable requirements excluding not applicable. |
| `trade_readiness[].requirements_evaluated` | Evaluated Requirements | Evaluated only; not necessarily covered. |
| `trade_readiness[].missing_requirements` | Missing Requirements | Applicable requirements without qualifying latest compliance. |
| `trade_readiness[].needs_review` | Needs Review | Evaluated but not covered. |
| `trade_readiness[].critical_issues` | Critical Issues | Open critical QA/QC issues mapped to trade context. |
| `trade_readiness[].high_issues` | High Issues | Open high QA/QC issues mapped to trade context. |
| `top_gaps[]` | Top Readiness Gaps | Deterministic readiness rule findings. |
| `recommended_actions[]` | Recommended Actions | Deterministic suggested review actions. |
| `seionSuggestions[]` | SEION Suggestions | Advisory only; not official readiness/compliance. |
| `documents[]` | Landing Documents | Indexed local files; PDFs are evidence candidates unless official evidence exists. |
| `documents[].document_category = drawing` | Drawings | Drawing PDFs registered from landing folders. |
| `documents[].document_category = specification` | Specifications | Specification PDFs registered from landing folders. |

## Anti-Pattern

Do not display `requirements_evaluated` as Covered.

The current readiness response does not expose a trade-level true covered count. If the UI needs covered counts by trade, add a backend field with tests first.

Do not present `demoFallback` or `demoMilestone` values as official readiness output.

Do not show AI Query as implemented.

Do not imply GraphRAG is required for current readiness scoring.

Do not display SEION-KGE suggestions as compliance. They must be labeled advisory and excluded from readiness score displays.

Do not display indexed drawing/specification PDFs as compliant or official evidence by default. Use "Evidence candidate" unless backend evidence records explicitly say otherwise.

## Demo Placeholder Fields

`demoFallback` and `demoMilestone` are demo placeholders.

They are not official readiness output and should be labeled as demo when displayed in the main dashboard path.
# SEION Suggestions Panel

The dashboard may show `SEION Suggestions` as an advisory panel only.

Required UI mapping:

- `relation`: suggested relationship type.
- `head_uid` / `tail_uid`: show friendly labels when available, with UID fallback.
- `score` / `rank`: model ranking only, not confidence in official compliance.
- `model_version`: visible for traceability.
- `status`: `suggested`, `accepted`, `rejected`, `stale`, or `superseded`.

Do not display SEION suggestions as official coverage, evaluated requirements, evidence, or readiness approval.
