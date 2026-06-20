# Architecture Mermaid Diagrams

## Overall System Architecture
```mermaid
flowchart LR
  RevitAddin["Revit Add-in"] --> Landing["Local Landing Zone"]
  Docs["Drawings / Specifications / Owner Requirements"] --> Landing
  Landing --> Backend["FastAPI Backend"]
  Backend --> Postgres["PostgreSQL Source of Truth"]
  Backend --> Processing["Processing / Sync Services"]
  Processing --> Postgres
  Postgres --> Dashboard["EMA AI Web Dashboard"]
  Backend --> Debug["Debug / Logs API"]
  Debug --> Dashboard
```

## Local Project Setup Flow
```mermaid
flowchart TD
  A["Create / Bootstrap Project"] --> B["Bind Client"]
  B --> C["Create Model Record"]
  C --> D["Configure Landing"]
  D --> E["Discover Files"]
  E --> F["Register Evidence Candidates"]
  F --> G["Select Project"]
  G --> H["Processing / Sync"]
  H --> I["Dashboard Views"]
```

## Milestone Criteria Architecture
```mermaid
flowchart TD
  OR["Owner Requirements"] --> Draft["Draft Criteria"]
  Specs["SpecLink / Specifications"] --> Draft
  Sheets["Drawing Sheets"] --> Draft
  Model["Model Evidence"] --> Draft
  Issues["QA/QC Issues"] --> Eval["Deterministic Evaluation"]
  Draft --> Review["Human Review"]
  Review --> Accepted["Accepted Criteria"]
  Accepted --> Eval
  Eval --> Score["Milestone Readiness"]
  Score --> Gaps["Gaps and Actions"]
```

## Revit Add-in Workflow
```mermaid
flowchart TD
  Connect["Connect Project"] --> Settings["Landing Settings"]
  Settings --> Validate["Validate Landing"]
  Validate --> Export["Export JSON"]
  Export --> Meta["Write Metadata Sidecar"]
  Meta --> Landing["Project Landing Folder"]
  Landing --> Web["Processing / Sync in Web App"]
```

## Local / Azure / Production Evolution
```mermaid
flowchart TD
  Local["Local MVP: Docker + Local Landing + PostgreSQL"] --> Pilot["Azure Pilot: Storage/Data Lake + PostgreSQL Flexible + App Service/Container Apps"]
  Pilot --> Prod["Production: Auth/RBAC + Key Vault + Monitoring + Governance + Backups"]
```
