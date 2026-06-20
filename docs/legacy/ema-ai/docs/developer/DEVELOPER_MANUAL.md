# Developer Manual

## Repo Areas
- Backend: `Pipeline/pipeline/app`
- Frontend: `Pipeline/pipeline/frontend/src`
- Revit add-in: `EMAExtractor`
- Tests: `Pipeline/pipeline/tests`
- Docs: `docs/` and `.ai/`

## Protected/Scoped Areas
- Do not modify secrets or `.env*`.
- Do not change protected infra/database files unless explicitly scoped.
- Do not commit real landing/project artifacts.

## PR Workflow
1. Audit + targeted change.
2. Validation commands.
3. Safe staging (exclude protected/generated data).
4. Update docs + `.ai` memory.
