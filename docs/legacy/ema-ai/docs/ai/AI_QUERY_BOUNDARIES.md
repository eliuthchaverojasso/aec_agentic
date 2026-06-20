# AI Query Boundaries

Status: Future scope. AI Query is not implemented.

## Source Of Truth

PostgreSQL remains the official source of truth.

The deterministic engines remain the official calculators:

- Rule Engine
- Evidence Engine
- Readiness Engine

Qdrant is semantic retrieval only. BGE-M3 is embeddings only.

SEION-KGE is advisory only. It may suggest evidence links or likely relationships, but predictions are not official compliance and are ignored by deterministic readiness until accepted into official records through human review or deterministic backend workflow.

## Future AI Query Role

AI Query may:

- Explain readiness scores.
- Summarize readiness gaps.
- Search evidence and supporting records.
- Draft suggestions for reviewers.
- Help classify or organize review work as a suggestion.
- Help users understand readiness with evidence-backed explanations.

## AI Query Must Not

- Approve readiness.
- Decide official compliance.
- Close issues.
- Modify source-of-truth data automatically.
- Override deterministic readiness scoring.
- Replace PostgreSQL as the official data source.

Any future LLM output must be traceable to evidence and clearly marked as assistant-generated.

## Deferred

- Production AI Query.
- GraphRAG.
- Autonomous compliance approval.
- SEION-KGE-driven automatic compliance writes.
# SEION Boundary

SEION/KGE is not AI Query and is not GraphRAG. It may provide advisory graph rankings and numerical audits, but it must not approve readiness, decide compliance, close issues, or automatically mutate PostgreSQL source-of-truth records.

AI Query and GraphRAG remain deferred unless separately scoped and tested.
