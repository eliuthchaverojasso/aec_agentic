# EMA AI Architecture Spec

## Implemented MVP

- FastAPI backend in `Pipeline/pipeline/app`.
- PostgreSQL data model through SQLAlchemy models and local `db/init.sql`.
- Deterministic readiness services in `app/readiness`.
- React/Vite dashboard in `Pipeline/pipeline/frontend`.

## Official Path

Official readiness comes from PostgreSQL records and deterministic engines. Suggested or assistant-generated data must remain separate until accepted by a human or deterministic backend workflow.

## SEION-KGE Placement

SEION-KGE consumes exported graph facts from PostgreSQL and writes advisory predictions to `seion_prediction`. The dashboard may display these as suggestions, but readiness scoring ignores them until an accepted suggestion is converted into official evidence/compliance/action records through a deterministic workflow.

## Deployment Direction

The Azure pilot is planned, not deployed. Recommended services are Static Web Apps, Container Apps, Azure PostgreSQL Flexible Server, ADLS Gen2, Key Vault, Application Insights, and Log Analytics.

