# Azure Pilot Architecture

Status: Azure pilot is planned, not deployed.

This document defines the target architecture for a controlled Shokworks Azure pilot and the longer-term EMA enterprise deployment path. It does not create Azure resources or claim that Azure hosting is active.

## Recommended Shokworks Pilot Default

| Capability | Recommended Azure Service |
| --- | --- |
| Frontend/dashboard | Azure Static Web Apps |
| Backend/API/processing | Azure Container Apps |
| Database | Azure Database for PostgreSQL Flexible Server |
| Storage/landing | Azure Storage Account with Data Lake Gen2 |
| Secrets | Azure Key Vault |
| Monitoring | Application Insights + Log Analytics |
| Registry | Azure Container Registry, optional but recommended for Container Apps |

Dashboard means the same existing React frontend initially. Do not split it into a separate dashboard backend unless future requirements justify that service boundary.

## Local-to-Cloud Mapping

| Local MVP | Azure Pilot |
| --- | --- |
| Docker Compose PostgreSQL | Azure Database for PostgreSQL Flexible Server |
| Local FastAPI container | Azure Container Apps or Azure App Service |
| Local React/Vite dashboard | Azure Static Web Apps or Azure App Service |
| Local landing folder | Azure Storage Account / ADLS Gen2 |
| `.env` | Key Vault + app settings |
| Docker logs | Application Insights + Log Analytics |
| Local manual packaging | CI/CD pipeline later |
| Local static JSON/XLSX demo | Storage landing containers |

## Sources

- Revit / EMAExtractor JSON exports.
- SharePoint Owner Requirements exports.
- Local pilot JSON/XLSX demo files moved through storage landing containers for cloud validation.

## Landing

- Azure Storage Account with Data Lake Gen2 / hierarchical namespace.
- Containers:
  - `landing`
  - `processed`
  - `archive`
  - `rejected`
  - optional `raw-exports`
- Manifest file per ingestion batch.

## Processing

- Azure Container Apps for FastAPI API and processing-oriented runtime.
- Optional Container Apps Job for background processing if processing later needs a separate scheduled or event-driven boundary.
- Azure App Service is acceptable for a simpler pilot if container orchestration is not required.
- Manifest-driven ingestion routes files to the correct parser.

## Serving

- Azure Database for PostgreSQL Flexible Server.
- Database name: `ema_ai`.
- PostgreSQL remains the official source of truth.
- JSONB remains appropriate for element parameters, issue traceability, evidence metadata, and readiness summaries where currently modeled.

## API

- FastAPI application layer.
- API Management is optional future gateway scope.
- CORS must be restricted to approved dashboard origins.

## Consumers

- React Deliverable Readiness Dashboard.
- Optional future controlled AI Query experience, not part of the pilot core.

## Enterprise Target

- EMA-owned Azure resource group or subscription.
- Environment separation: `dev`, `test`, `prod`.
- VNET integration.
- Private endpoints.
- Managed identities.
- Least-privilege RBAC.
- Key Vault for secrets and sensitive configuration.
- No public database access.
- PostgreSQL backup/restore policy.
- CI/CD with environment approvals.
- Audit logging and operational retention.
- CORS locked down to approved domains.

## Azure Resource Checklist

- Resource Group.
- Storage Account with ADLS Gen2.
- Azure Database for PostgreSQL Flexible Server.
- Container Apps Environment.
- Container App for API.
- Optional Container Apps Job for processing.
- Static Web App or App Service for dashboard.
- Key Vault.
- Application Insights.
- Log Analytics Workspace.
- Optional Azure Container Registry.
- Optional API Management.

See `docs/deployment/AZURE_RESOURCE_CHECKLIST.md` for the fuller deployment checklist.

## Environment Variable Checklist

- `DATABASE_URL`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `POSTGRES_DB`
- `API_PORT`
- `LOG_LEVEL`
- `CORS_ORIGINS`
- `LANDING_DIR`
- `STORAGE_ACCOUNT_NAME`
- `STORAGE_CONTAINER_LANDING`
- `STORAGE_CONTAINER_PROCESSED`
- `STORAGE_CONTAINER_ARCHIVE`
- `STORAGE_CONTAINER_REJECTED`
- `KEY_VAULT_URI`
- `APPINSIGHTS_CONNECTION_STRING`
- `VITE_API_BASE_URL`

See `docs/deployment/ENVIRONMENT_VARIABLES.md` for source, secrecy, and environment notes.

## Deferred Scope

- Production AI Query.
- GraphRAG.
- Live ACC integration.
- UNANET integration.
- Full PDF/OCR Drawing Reel.
