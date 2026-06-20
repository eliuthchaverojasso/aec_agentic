# EMA AI Documentation Index

Last refreshed: 2026-06-15

## Purpose

This index is the navigation hub for EMA-AI documentation, validation notes, and publication sources. It is written for stakeholders, operators, reviewers, and future agents who need a fast read on what the repository currently supports.

## Product Truth

- EMA-AI is a local MVP for engineering deliverable readiness.
- PostgreSQL is the source of truth.
- Readiness is deterministic.
- AI, SEION, and semantic retrieval are advisory only.
- Indexed drawings/specifications/PDFs are evidence candidates, not official evidence.
- Local demo users and roles are not production auth.
- EMA-AI is not production-ready and not official compliance software.
- C# add-in tests are currently 246/246.
- Backend pytest is 127 pass / 47 fail without a running Docker/PostgreSQL stack; full suite has 174 tests.

## Reading Order

1. [docs/README.md](./README.md)
2. [docs/api/DASHBOARD_API_CONTRACT.md](./api/DASHBOARD_API_CONTRACT.md)
3. [docs/architecture/ARCHITECTURE.md](./architecture/ARCHITECTURE.md)
4. [docs/landing/LANDING_FULL_ROOT_PROCESSING.md](./landing/LANDING_FULL_ROOT_PROCESSING.md)
5. [docs/frontend/APP_NAVIGATION_MAP.md](./frontend/APP_NAVIGATION_MAP.md)
6. [docs/readiness/READINESS_SEMANTICS.md](./readiness/READINESS_SEMANTICS.md)
7. [docs/revit/RIBBON_TO_LANDING_WORKFLOW.md](./revit/RIBBON_TO_LANDING_WORKFLOW.md)
8. [docs/demo/EMA_AI_5_MINUTE_DEMO_SCRIPT.md](./demo/EMA_AI_5_MINUTE_DEMO_SCRIPT.md)
9. [docs/security/SECURITY_NOTES.md](./security/SECURITY_NOTES.md)
10. [docs/papers/README.md](./papers/README.md)

## Architecture

| Document | Path | Purpose |
|---|---|---|
| EMA-AI Architecture | [docs/architecture/ARCHITECTURE.md](./architecture/ARCHITECTURE.md) | Primary system overview |
| Azure Deployment Recommendation | [docs/architecture/AZURE_DEPLOYMENT_RECOMMENDATION.md](./architecture/AZURE_DEPLOYMENT_RECOMMENDATION.md) | Azure pilot path and resource guidance |
| Data Flow | [docs/architecture/DATA_FLOW.md](./architecture/DATA_FLOW.md) | Canonical data movement and lifecycle |
| Security and Data Boundaries | [docs/architecture/SECURITY_AND_DATA_BOUNDARIES.md](./architecture/SECURITY_AND_DATA_BOUNDARIES.md) | Local demo safety and governance |

## API

| Document | Path | Purpose |
|---|---|---|
| Dashboard API Contract | [docs/api/DASHBOARD_API_CONTRACT.md](./api/DASHBOARD_API_CONTRACT.md) | Frontend/backend contract |
| Landing API Contract | [docs/api/LANDING_API_CONTRACT.md](./api/LANDING_API_CONTRACT.md) | Landing discovery, manifest, ingest, binding |
| Readiness API Contract | [docs/api/READINESS_API_CONTRACT.md](./api/READINESS_API_CONTRACT.md) | Readiness, actions, snapshots, semantics |

## Landing

| Document | Path | Purpose |
|---|---|---|
| Landing Full Root Processing | [docs/landing/LANDING_FULL_ROOT_PROCESSING.md](./landing/LANDING_FULL_ROOT_PROCESSING.md) | Multi-project landing root workflow |
| Landing File Update Workflow | [docs/landing/LANDING_FILE_UPDATE_WORKFLOW.md](./landing/LANDING_FILE_UPDATE_WORKFLOW.md) | Safe local file lifecycle |
| Landing Manifest Spec | [docs/landing/LANDING_MANIFEST_SPEC.md](./landing/LANDING_MANIFEST_SPEC.md) | Manifest shape and classification rules |
| Landing Project Binding | [docs/landing/LANDING_PROJECT_BINDING.md](./landing/LANDING_PROJECT_BINDING.md) | Client/project binding workflow |

## Frontend

| Document | Path | Purpose |
|---|---|---|
| App Navigation Map | [docs/frontend/APP_NAVIGATION_MAP.md](./frontend/APP_NAVIGATION_MAP.md) | Route inventory |
| Processing / Sync Manual | [docs/frontend/PROCESSING_SYNC_MANUAL.md](./frontend/PROCESSING_SYNC_MANUAL.md) | Operator control room workflow |
| Project Setup Workflow | [docs/frontend/PROJECT_SETUP_WORKFLOW.md](./frontend/PROJECT_SETUP_WORKFLOW.md) | Project/client binding and landing setup |
| Documents / Evidence Workflow | [docs/frontend/DOCUMENTS_EVIDENCE_WORKFLOW.md](./frontend/DOCUMENTS_EVIDENCE_WORKFLOW.md) | Evidence candidate document flow |
| Requirements Workflow | [docs/frontend/REQUIREMENTS_WORKFLOW.md](./frontend/REQUIREMENTS_WORKFLOW.md) | Owner requirements semantics |
| Drawing Reel Workflow | [docs/frontend/DRAWING_REEL_WORKFLOW.md](./frontend/DRAWING_REEL_WORKFLOW.md) | Indexed drawing sheet view |
| Model Health Workflow | [docs/frontend/MODEL_HEALTH_WORKFLOW.md](./frontend/MODEL_HEALTH_WORKFLOW.md) | Model health and issue views |
| Executive Overview Workflow | [docs/frontend/EXECUTIVE_OVERVIEW_WORKFLOW.md](./frontend/EXECUTIVE_OVERVIEW_WORKFLOW.md) | Portfolio dashboard summary |
| Design System | [docs/frontend/DESIGN_SYSTEM.md](./frontend/DESIGN_SYSTEM.md) | Tokens, materials, and controls |
| Appearance Settings | [docs/frontend/APPEARANCE_SETTINGS.md](./frontend/APPEARANCE_SETTINGS.md) | Theme settings and safety |
| Appearance Memory | [docs/APPEARANCE_MEMORY.md](./APPEARANCE_MEMORY.md) | Canonical local-only appearance memory for style propagation |
| White Label Guide | [docs/frontend/WHITE_LABEL_GUIDE.md](./frontend/WHITE_LABEL_GUIDE.md) | Brand configuration and demo labeling |

## Readiness

| Document | Path | Purpose |
|---|---|---|
| Readiness Semantics | [docs/readiness/READINESS_SEMANTICS.md](./readiness/READINESS_SEMANTICS.md) | Deterministic scoring policy |
| Owner Requirements Readiness | [docs/readiness/OWNER_REQUIREMENTS_READINESS.md](./readiness/OWNER_REQUIREMENTS_READINESS.md) | Client binding and requirements states |
| Evidence Candidate Policy | [docs/readiness/EVIDENCE_CANDIDATE_POLICY.md](./readiness/EVIDENCE_CANDIDATE_POLICY.md) | What is and is not official evidence |

## Revit

| Document | Path | Purpose |
|---|---|---|
| Ribbon Layout | [docs/revit/RIBBON_LAYOUT.md](./revit/RIBBON_LAYOUT.md) | Ribbon panel layout |
| Ribbon to Landing Workflow | [docs/revit/RIBBON_TO_LANDING_WORKFLOW.md](./revit/RIBBON_TO_LANDING_WORKFLOW.md) | Export and landing handoff |
| Command Map | [docs/revit/COMMAND_MAP.md](./revit/COMMAND_MAP.md) | Command inventory |
| Add-in Installation | [docs/revit/ADDIN_INSTALLATION.md](./revit/ADDIN_INSTALLATION.md) | Installation and deployment |
| Revit Runtime Smoke Checklist | [docs/revit/REVIT_RUNTIME_SMOKE_CHECKLIST.md](./revit/REVIT_RUNTIME_SMOKE_CHECKLIST.md) | Host validation checklist |

## Demo

| Document | Path | Purpose |
|---|---|---|
| 5 Minute Demo Script | [docs/demo/EMA_AI_5_MINUTE_DEMO_SCRIPT.md](./demo/EMA_AI_5_MINUTE_DEMO_SCRIPT.md) | Executive demo flow |
| Demo Runbook | [docs/demo/DEMO_RUNBOOK.md](./demo/DEMO_RUNBOOK.md) | Local demo operating notes |
| Demo Smoke Checklist | [docs/demo/EMA_AI_DEMO_SMOKE_CHECKLIST.md](./demo/EMA_AI_DEMO_SMOKE_CHECKLIST.md) | Pre-demo validation checklist |

## Security

| Document | Path | Purpose |
|---|---|---|
| Security Notes | [docs/security/SECURITY_NOTES.md](./security/SECURITY_NOTES.md) | Data and boundary guidance |
| Security Notes | [docs/security/SECURITY_NOTES.md](./security/SECURITY_NOTES.md) | Data and boundary guidance (canonical security document) |

## Reports

| Document | Path | Purpose |
|---|---|---|
| External Project Report | [docs/reports/EMA_AI_EXTERNAL_PROJECT_REPORT.md](./reports/EMA_AI_EXTERNAL_PROJECT_REPORT.md) | Stakeholder-ready summary |
| Azure Deployment Recommendation | [docs/reports/EMA_AI_AZURE_DEPLOYMENT_RECOMMENDATION.md](./reports/EMA_AI_AZURE_DEPLOYMENT_RECOMMENDATION.md) | Azure pilot recommendation |

## Papers

| Paper | Path | Audience | Build Command | Status |
|---|---|---|---|---|
| EMA-AI Technical Whitepaper | [docs/papers/ema_ai_technical_whitepaper](./papers/ema_ai_technical_whitepaper) | technical/product architecture | `latexmk -pdf -interaction=nonstopmode -halt-on-error main.tex` | pending build validation |
| EMA-AI Product Overview | [docs/papers/ema_ai_product_overview](./papers/ema_ai_product_overview) | executive/product stakeholders | `latexmk -pdf -interaction=nonstopmode -halt-on-error main.tex` | pending build validation |
| EMA-AI Architecture Report | [docs/papers/ema_ai_architecture_report](./papers/ema_ai_architecture_report) | IT/cloud/engineering reviewers | `latexmk -pdf -interaction=nonstopmode -halt-on-error main.tex` | pending build validation |

## Governance

| Document | Path | Purpose |
|---|---|---|
| Canonical Reference Manifest | [.ai/PROJECT_REFERENCE_MANIFEST.yaml](../.ai/PROJECT_REFERENCE_MANIFEST.yaml) | Machine-readable source of truth for all volatile project facts |
| Reference Validation Script | [scripts/validate_project_references.py](../scripts/validate_project_references.py) | Detects stale references, broken links, and overclaims |
| Validation Allowlist | [docs/reference_validation_allowlist.yaml](./reference_validation_allowlist.yaml) | Intentional exceptions to the validator |
| Project Reference Baseline | [docs/audits/PROJECT_REFERENCE_BASELINE.md](./audits/PROJECT_REFERENCE_BASELINE.md) | Audit baseline before reconciliation (2026-06-14) |
| Project Reference Final Audit | [docs/audits/PROJECT_REFERENCE_FINAL_AUDIT.md](./audits/PROJECT_REFERENCE_FINAL_AUDIT.md) | Reconciliation results and remaining decisions |

## Known Caveats

- PDFs in landing directories are evidence candidates only.
- Revit runtime validation is pending unless explicitly tested in Revit.
- Azure deployment is a recommendation path, not a live production claim.
- Python tests (174 total) require a running Docker/PostgreSQL stack for full pass.
- CI is currently failing — Python tests need a database service configured in the GitHub Actions workflow.
