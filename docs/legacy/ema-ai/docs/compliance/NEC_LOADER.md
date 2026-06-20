# NEC Loader (Structured Corpus)

EMA AI supports local NEC structured-corpus preview/import through backend endpoints.

## Inputs (landing-relative paths)

- `blocks.jsonl`
- `edges.csv`
- `structure_audit.json` (optional)
- `research_grade_gates.json` (optional)

## Behavior

- Preview reports block/edge counts, health score (if provided), and failed gates.
- Import creates candidate corpus + candidate rules from sampled references.
- Failed gates force review-required corpus state.
- Import never marks rules active automatically.

