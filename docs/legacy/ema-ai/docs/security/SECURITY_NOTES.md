# Security Notes

## Local MVP

- Local MVP has no production authentication.
- Do not commit real client landing files, logs, generated packages, `.env`, `.env.*`, or local credentials.
- `.env.example` files are safe templates only.
- Local `.env` values are local/demo only and must not be reused for shared or production environments.
- Landing path ingestion must use safe path resolution and reject traversal outside the configured landing directory.
- CORS should be configured through environment variables and restricted for pilot deployments.
- PostgreSQL is the official source of truth.
- Deterministic engines calculate official readiness and compliance state.
- LLMs may explain, summarize, search, and draft suggestions, but must not approve readiness, decide compliance, close issues, or modify source-of-truth data automatically.
- Qdrant is semantic retrieval only.
- SEION-KGE predictions are advisory only and must be stored separately from official compliance/evidence.
- SEION exports must not include secrets or raw sensitive document text unless the data has already been intentionally normalized and approved for export.
- Do not send sensitive client/project data to third-party LLM services without explicit approval and a scoped security review.
- Logs must not include secrets, connection strings, storage keys, access tokens, or production payloads.
- Landing document scan/index endpoints return metadata only, reject path traversal, do not serve raw PDFs, and do not expose arbitrary local filesystem reads.
- Any stored PDF text preview must be capped. Drawing/specification PDFs are evidence candidates only unless official backend evidence records say otherwise.
- OCR/vision remains a future local adapter boundary. The current implementation does not upload PDFs or images to external AI/vision providers.

## Pilot/Azure Direction

- Store cloud secrets in Key Vault.
- Managed Identity is preferred for cloud resource access.
- Apply least privilege RBAC to storage, database, and deployment resources.
- Restrict network access with firewall/private networking where possible.
- Azure PostgreSQL should not be publicly open in enterprise.
- Storage should use RBAC and private endpoints in enterprise.
- Use Application Insights and Log Analytics for operational visibility.
- Keep client data in controlled storage, not source control.
- Delivery packages must not include production secrets.
- Application settings may reference Key Vault, but real production credentials must not be committed.

## Explicitly Deferred

- Broad tenant-wide access.
- Production AI Query over uncontrolled data.
- AI Query is deferred and assistant-only.
- AI approval of official readiness or compliance.
- GraphRAG as an official calculator.
- GraphRAG is deferred.
- SEION-KGE as an automatic compliance approver.
- Live ACC/UNANET integrations without scoped security review.
# SEION Prediction Import Security

`POST /api/v1/seion/import-predictions` rejects arbitrary filesystem paths. Prediction imports must resolve under the SEION artifacts directory and must use `.jsonl`.

SEION prediction import does not call third-party LLMs, does not touch secrets, and does not mutate official readiness, compliance, evidence, or issue records.
