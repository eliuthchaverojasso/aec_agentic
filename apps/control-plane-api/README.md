# Control Plane API

This app currently hosts the migrated EMA AI FastAPI backend under the transitional `app` package. Future refactors should move business logic into `packages/python/control-plane-core` and keep this app focused on HTTP, auth, request validation, command dispatch, queries, webhooks, health checks, and OpenAPI publication.

Run legacy API tests from the repository root after installing dependencies:

```powershell
$env:PYTHONPATH="apps/control-plane-api"
python -m pytest apps/control-plane-api/tests -q
```

