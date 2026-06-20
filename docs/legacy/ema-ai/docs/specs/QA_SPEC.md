# EMA AI QA Spec

## Required Checks

- PostgreSQL remains source of truth.
- Readiness remains deterministic.
- SEION predictions remain advisory.
- AI Query and GraphRAG remain deferred unless explicitly scoped.
- Frontend labels do not confuse covered and evaluated requirements.
- No real client data, secrets, `.env`, or protected files are modified.

## Backend Validation

```powershell
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
py -3.12 -m pytest .\Pipeline\pipeline\tests\test_readiness_scoring.py `
  .\Pipeline\pipeline\tests\test_service_readiness.py `
  .\Pipeline\pipeline\tests\test_api_readiness.py `
  .\Pipeline\pipeline\tests\test_requirements_loader.py `
  .\Pipeline\pipeline\tests\test_seion_exporter.py `
  .\Pipeline\pipeline\tests\test_api_seion.py -v
```

## Frontend Validation

```powershell
cd Pipeline\pipeline\frontend
npm.cmd install
npm.cmd run build
```

