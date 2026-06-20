# Code Compliance Loader (Local MVP)

The loader provides a review-first compliance corpus workflow for local operation.

## Principles

- Corpus/rules are imported as **candidate** by default.
- Failed critical gates produce `candidate_review_required`.
- No automatic official compliance activation.
- Deterministic/human review remains required before activating rules.

## API

- `GET /api/v1/compliance/status`
- `GET /api/v1/compliance/corpora`
- `GET /api/v1/compliance/corpora/{corpus_id}`
- `POST /api/v1/compliance/corpora/nec/preview`
- `POST /api/v1/compliance/corpora/nec/import`
- `GET /api/v1/compliance/rules`

