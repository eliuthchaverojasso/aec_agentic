# Environment Variables

Status: Source-of-truth deployment variable checklist. Values shown here are examples or sources, not production credentials.

## Variable Checklist

| Variable | Purpose | Local Value / Example | Azure Source | Secret? | Notes |
| --- | --- | --- | --- | --- | --- |
| `POSTGRES_USER` | PostgreSQL user for local Docker Compose | `ema` | App setting or Key Vault reference if needed | Yes in shared/cloud environments | Local demo only in `.env.example`; prefer `DATABASE_URL`/managed identity patterns in cloud where possible |
| `POSTGRES_PASSWORD` | PostgreSQL password for local Docker Compose | `ema_dev_pw` | Key Vault | Yes | Never commit real shared or production passwords |
| `POSTGRES_DB` | Database name | `ema_ai` | App setting | No | Target cloud database name is `ema_ai` |
| `POSTGRES_PORT` | Local host port for PostgreSQL | `5432` | Usually not needed in Azure | No | Local Docker Compose maps host `5432` to container `5432` |
| `API_PORT` | Local host port for FastAPI container | `8010` | Container app ingress config, not usually an env var | No | Container listens on `8000`; host maps to `8010` locally |
| `LOG_LEVEL` | Runtime logging verbosity | `INFO` | App setting | No | Avoid logging secrets or sensitive payloads |
| `CORS_ORIGINS` | Approved frontend origins | `http://localhost:5173,http://127.0.0.1:5173` | App setting | No | Pilot and enterprise must restrict to approved frontend/dashboard origins |
| `LANDING_DIR` | Local or mounted landing path | `/app/landing` or `./landing` | App setting or storage-backed config | No | In Azure this may become a storage-backed landing configuration |
| `DATABASE_URL` | SQLAlchemy/PostgreSQL connection string | `postgresql+psycopg2://ema:ema_dev_pw@localhost:5432/ema_ai` | Key Vault reference or app setting using managed secret | Yes | Do not commit production URLs with credentials |
| `VITE_API_BASE_URL` | Frontend API base URL | `http://localhost:8010` | Static Web Apps/App Service frontend setting | No, unless URL itself is restricted | Frontend falls back to `http://localhost:8010` locally when unset |
| `STORAGE_ACCOUNT_NAME` | Azure Storage account name | Not used locally | App setting | No | Required for ADLS Gen2 integration when implemented |
| `STORAGE_CONTAINER_LANDING` | Landing container name | `landing` | App setting | No | Incoming exports/manifests |
| `STORAGE_CONTAINER_PROCESSED` | Processed container name | `processed` | App setting | No | Successfully processed payloads |
| `STORAGE_CONTAINER_ARCHIVE` | Archive container name | `archive` | App setting | No | Retained historical payloads |
| `STORAGE_CONTAINER_REJECTED` | Rejected/error container name | `rejected` | App setting | No | Failed validation or rejected payloads |
| `KEY_VAULT_URI` | Key Vault endpoint | Not used locally | App setting | No | Runtime identity should read secrets through Key Vault |
| `APPINSIGHTS_CONNECTION_STRING` | Application Insights telemetry connection | Not used locally | Key Vault or app setting | Yes | Treat as sensitive operational config |

## Local Rules

- `.env.example` files contain local-demo defaults only.
- Real `.env`, `.env.*`, and production secrets must not be committed.
- Local Docker Compose uses PostgreSQL user `ema`, database `ema_ai`, and demo password `ema_dev_pw`.
- Local backend URL is `http://localhost:8010`.
- Local frontend/dashboard URL is `http://localhost:5173`.

## Shokworks Azure Pilot Rules

- Store secret values in Key Vault or approved app settings.
- Set `DATABASE_URL`, `CORS_ORIGINS`, `LOG_LEVEL`, storage variables, `KEY_VAULT_URI`, `APPINSIGHTS_CONNECTION_STRING`, and `VITE_API_BASE_URL` per pilot environment.
- Restrict `CORS_ORIGINS` to the approved Static Web Apps or App Service frontend URL.
- Use Azure Database for PostgreSQL Flexible Server for official data.
- Use ADLS Gen2 containers for landing, processed, archive, and rejected payloads.

## Enterprise Rules

- Prefer Managed Identity for access to Key Vault, Storage, and other Azure resources.
- Use Key Vault references instead of plain production DB passwords in app configuration.
- Do not commit production `DATABASE_URL`, storage keys, Application Insights connection strings, or any client secrets.
- Lock CORS to approved custom domains.
- Do not allow public database access in enterprise.
