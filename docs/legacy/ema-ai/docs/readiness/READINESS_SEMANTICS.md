# Readiness Semantics

Status: Implemented for the current deterministic backend MVP.

## Source Of Truth

PostgreSQL is the official source of truth. The Readiness Engine computes official readiness deterministically from backend data. LLMs, AI Query, and semantic retrieval must not approve readiness or compliance.

## Requirement Status Policy

| Status | Covered? | Evaluated? | Applicable denominator? | UI/risk meaning |
| --- | --- | --- | --- | --- |
| `compliant` | Yes | Yes | Included | Requirement is covered. |
| `non_compliant` | No | Yes | Included | Requirement was reviewed and is not covered. |
| `needs_review` | No | Yes | Included | Requirement needs reviewer confirmation and must be visible separately. |
| `not_applicable` | No | No | Excluded | Requirement does not apply and should not penalize readiness. |
| Missing/no compliance row | No | No | Included | Requirement is missing. |
| Non-actionable requirement | No | No | Excluded | Reference/link row or other non-actionable catalog row. |

## Terms

| Term | Meaning |
| --- | --- |
| Covered | A requirement whose latest project compliance status is `compliant`. |
| Evaluated | A requirement whose latest project compliance status is `compliant`, `non_compliant`, or `needs_review`. |
| Applicable | Active, actionable requirements excluding `not_applicable`. |
| Missing | Applicable requirements with no qualifying latest compliance row. |
| Excluded | Inactive, non-actionable, or `not_applicable` requirements. |
| Non-actionable | Active catalog row that provides reference/link context but is not an actionable project requirement. |

## Scores

`requirement_coverage.score` means:

```text
covered compliant requirements / applicable requirements
```

It does not mean evaluated/total.

`requirements_evaluated` must not be treated as covered. It means the requirement has been evaluated as `compliant`, `non_compliant`, or `needs_review`.

Current MVP readiness formula remains:

```text
50% Requirement Coverage
30% QA/QC Health
20% Sync Freshness
```

## Test Baseline

- Service readiness suite: 7 passed.
- Combined readiness + requirements suite: 18 passed.

## Non-Goals

- No AI approval of readiness.
- No official compliance decision by an LLM.
- No GraphRAG dependency for readiness calculation.
- AI Query is deferred and is not a dependency for official readiness.
- Indexed drawing/specification PDFs are not official evidence by default and do not change readiness unless deterministic backend evidence/compliance records are created.
