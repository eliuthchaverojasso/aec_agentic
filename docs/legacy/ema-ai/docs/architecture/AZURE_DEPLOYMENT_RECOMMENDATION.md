# Azure Deployment Recommendation

## Purpose

This document summarizes a conservative Azure pilot path for EMA-AI. It is not a claim that the system is already deployed to Azure.

## Recommendation

- Frontend: Azure Static Web Apps or App Service.
- Backend: Azure Container Apps or App Service for FastAPI.
- Database: Azure PostgreSQL Flexible Server.
- File store: Azure Storage or Data Lake Gen2 for landing/archive handoff.
- Secrets: Azure Key Vault.
- Observability: Application Insights and Log Analytics.
- Identity: Managed Identity with scoped RBAC.
- Registry: Azure Container Registry if container delivery is adopted.

## Operating Model

The local MVP remains the source for development and demo validation. Azure should be introduced only after the local documentation, API contracts, and smoke tests remain stable.

## Caveats

- No production security claims.
- No official compliance claims.
- No APS production viewer claim.
- No automatic continuous sync claim unless explicitly implemented and tested.

