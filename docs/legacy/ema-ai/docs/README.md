# EMA AI — Documentation Index

**Product:** Revit-first Owner Requirements Readiness platform  
**Branch:** `docs/project-reference-reconciliation`  
**Validated product branch:** `feat/revit-first-owner-requirement-checker` (audited commit `ae6ded2e9e5efb96ccb9ab18298e08e37b6a7a1e`)  
**Last updated:** 2026-06-15

---

## Product Overview

EMA AI is an Engineering Intelligence / Deliverable Readiness platform currently focused on **Owner Requirements Readiness** through a **Revit-first deterministic workflow**.

**Official workflow:**

1. Open Revit
2. Open EMA AI workflow panel
3. Load Owner Requirements (XLSX)
4. Sync Model Data
5. Run Compliance Check
6. Generate Master HTML/PDF Report
7. Review Discipline Sections
8. Inspect Evidence Found, Validation Type, Rule Applied
9. Trace Revit Element IDs
10. Ask EMA AI

The **deterministic engine** is the source of truth for status assignment. The report explains deterministic results. Ask EMA AI explains report context — it does not assign official statuses.

See [PROJECT_MEMORY.md](PROJECT_MEMORY.md) for full product narrative.

---

## Documentation Map

### Core Product Docs
| Doc | Description |
|-----|-------------|
| [PROJECT_MEMORY.md](PROJECT_MEMORY.md) | Single source of truth — product identity, state, decisions, risks |
| [AI_AGENT_SKILL.md](AI_AGENT_SKILL.md) | Handoff for AI coding agents working on this repo |

### Architecture
| Doc | Description |
|-----|-------------|
| [architecture/EMA_AI_ARCHITECTURE.md](architecture/EMA_AI_ARCHITECTURE.md) | System architecture and data flow |
| [docs/architecture/ARCHITECTURE.md](architecture/ARCHITECTURE.md) | Primary system overview |
| [docs/architecture/ARCHITECTURE_OVERVIEW.md](architecture/ARCHITECTURE_OVERVIEW.md) | Brief component boundaries |
| [docs/architecture/DATA_FLOW.md](architecture/DATA_FLOW.md) | Canonical data flow |
| [docs/architecture/REVIT_ADDIN_ARCHITECTURE.md](architecture/REVIT_ADDIN_ARCHITECTURE.md) | Revit add-in components |
| [docs/architecture/READINESS_ARCHITECTURE.md](architecture/READINESS_ARCHITECTURE.md) | Readiness engine architecture |
| [docs/architecture/LANDING_ZONE_ARCHITECTURE.md](architecture/LANDING_ZONE_ARCHITECTURE.md) | Landing zone structure |

### Methodology
| Doc | Description |
|-----|-------------|
| [methodology/OWNER_REQUIREMENTS_ENGINE.md](methodology/OWNER_REQUIREMENTS_ENGINE.md) | Deterministic requirement engine methodology |
| [docs/demo/EMA_AI_REQUIREMENT_ENGINE_METHODOLOGY.md](demo/EMA_AI_REQUIREMENT_ENGINE_METHODOLOGY.md) | Pipeline methodology with formulas |

### Reporting
| Doc | Description |
|-----|-------------|
| [reporting/OWNER_REQUIREMENTS_REPORT_SPEC.md](reporting/OWNER_REQUIREMENTS_REPORT_SPEC.md) | Formal report specification |
| [docs/reports/EMA_AI_EXTERNAL_PROJECT_REPORT.md](reports/EMA_AI_EXTERNAL_PROJECT_REPORT.md) | Stakeholder-facing summary |

### AI & Assistant
| Doc | Description |
|-----|-------------|
| [ai/ASK_EMA_AI_SPEC.md](ai/ASK_EMA_AI_SPEC.md) | Ask EMA AI behavior, provider strategy, guardrails |
| [docs/ai/AI_QUERY_BOUNDARIES.md](ai/AI_QUERY_BOUNDARIES.md) | AI query boundaries and rules |
| [docs/demo/EMA_AI_ASSISTANT.md](demo/EMA_AI_ASSISTANT.md) | AI Assistant specification |

### Demo & Pilot
| Doc | Description |
|-----|-------------|
| [demo/EMA_AI_DEMO_SCRIPT.md](demo/EMA_AI_DEMO_SCRIPT.md) | Demo script for Paul/Broadleaf/EMA |
| [demo/EMA_AI_DEMO_SMOKE_CHECKLIST.md](demo/EMA_AI_DEMO_SMOKE_CHECKLIST.md) | Manual Revit/browser/PDF checklist |
| [demo/EMA_AI_WEEK_BY_WEEK_PLAN.md](demo/EMA_AI_WEEK_BY_WEEK_PLAN.md) | Remaining plan |
| [demo/EMA_AI_CLIENT_NARRATIVE.md](demo/EMA_AI_CLIENT_NARRATIVE.md) | Executive client-facing story |
| [docs/demo/EMA_AI_MONDAY_DEMO_FLOW.md](demo/EMA_AI_MONDAY_DEMO_FLOW.md) | Primary Revit demo flow |
| [docs/demo/THURSDAY_DEMO_PLAN.md](demo/THURSDAY_DEMO_PLAN.md) | 20-25 min client demo plan |
| [docs/demo/EMA_AI_5_MINUTE_DEMO_SCRIPT.md](demo/EMA_AI_5_MINUTE_DEMO_SCRIPT.md) | 5-minute executive demo |

### Development
| Doc | Description |
|-----|-------------|
| [dev/LOCAL_DEVELOPMENT.md](dev/LOCAL_DEVELOPMENT.md) | Local development guide |
| [dev/BUILD_RELEASE.md](dev/BUILD_RELEASE.md) | Build/test/installer guide |
| [dev/TESTING_STRATEGY.md](dev/TESTING_STRATEGY.md) | Testing strategy |
| [dev/GIT_HYGIENE.md](dev/GIT_HYGIENE.md) | Git hygiene and commit rules |
| [docs/developer/DEVELOPER_MANUAL.md](developer/DEVELOPER_MANUAL.md) | Full developer manual |
| [docs/developer/TESTING_GUIDE.md](developer/TESTING_GUIDE.md) | Testing guide |

### Reference
| Doc | Description |
|-----|-------------|
| [reference/DATA_SCHEMA.md](reference/DATA_SCHEMA.md) | Data model and hidden JSON schema |
| [reference/ENVIRONMENT_VARIABLES.md](reference/ENVIRONMENT_VARIABLES.md) | Environment variables and providers |
| [docs/deployment/ENVIRONMENT_VARIABLES.md](deployment/ENVIRONMENT_VARIABLES.md) | Deployment environment configuration |

### Planning
| Doc | Description |
|-----|-------------|
| [ROADMAP.md](ROADMAP.md) | Phase-by-phase roadmap |
| [DECISIONS.md](DECISIONS.md) | Architecture Decision Records |
| [RISKS_AND_LIMITATIONS.md](RISKS_AND_LIMITATIONS.md) | Current risks and mitigations |
| [docs/adr/](adr/) | Individual ADR files |
| [docs/roadmap/ROADMAP.md](roadmap/ROADMAP.md) | Detailed roadmap |

### Skills & Prompts
| Doc | Description |
|-----|-------------|
| [SKILLS.md](SKILLS.md) | Capability/skill map |
| [prompts/AGENT_PROMPTS.md](prompts/AGENT_PROMPTS.md) | Reusable project prompts |

### LaTeX Papers
| Doc | Description |
|-----|-------------|
| [docs/papers/ema_ai_technical_whitepaper/main.tex](papers/ema_ai_technical_whitepaper/main.tex) | Technical whitepaper |
| [docs/papers/ema_ai_product_overview/main.tex](papers/ema_ai_product_overview/main.tex) | Product overview |
| [docs/papers/ema_ai_architecture_report/main.tex](papers/ema_ai_architecture_report/main.tex) | Architecture report |

### `.ai` Context Files
| File | Purpose |
|------|---------|
| [.ai/PROJECT_CONTEXT.md](../.ai/PROJECT_CONTEXT.md) | Concise project context for agents |
| [.ai/MEMORY.md](../.ai/MEMORY.md) | Current state memory |
| [.ai/AGENT_INSTRUCTIONS.md](../.ai/AGENT_INSTRUCTIONS.md) | Agent instructions |
| [.ai/SKILLS.md](../.ai/SKILLS.md) | Capability map summary |
| [.ai/PROMPTS.md](../.ai/PROMPTS.md) | Key prompts |
| [.ai/REPORTING_CONTEXT.md](../.ai/REPORTING_CONTEXT.md) | Report-specific rules |
| [.ai/ASK_EMA_AI_CONTEXT.md](../.ai/ASK_EMA_AI_CONTEXT.md) | AI assistant context |

See [DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md) for the full categorized index.

---

## Current Project State

### Known Demo Dataset
- **Project/Model:** MEP-NISD-MIDDLE SCHOOL 8
- **Requirements workbook:** NORTHWEST ISD 06.02.2025.xlsx
- **Total requirements:** 804
- **Model elements reviewed:** 21,868
- **Disciplines:** Electrical, Lighting, Mechanical, Plumbing, Technology

### Current Verified Demo Totals
| Metric | Value |
|--------|-------|
| Total Requirements | 804 |
| Met | 55 |
| Not Met | 250 |
| Needs Human Review | 499 |
| Insufficient Model Data | 0 |
| Not Applicable | 0 |
| Evidence Review Score | 20.1 |
| Last verified | 2026-06-10 |

For filtered tile values, score-scale caveats, and the current report narrative, see [../.ai/CURRENT_STATE.md](../.ai/CURRENT_STATE.md) and [../.ai/PROJECT_REFERENCE_MANIFEST.yaml](../.ai/PROJECT_REFERENCE_MANIFEST.yaml).

### Report Capabilities
- Master Owner Requirements Review
- View / Status / Urgency filters
- Executive Summary
- Discipline Allocation
- Status / Urgency Legend
- Key Issues & Recommended Actions
- Issues by Urgency
- Discipline Sections
- Requirement-by-Requirement Detail
- Evidence Found / Validation Type / Rule Applied / Reasoning / Next Best Action
- Revit Element ID traceability (copy Element IDs, collapsed by default)
- Hidden machine-readable JSON (`#ema-ai-report-context`)

---

## Core Principles

1. **Deterministic engine owns official status.** AI explains, does not decide.
2. **Report is human-readable and machine-readable.** Hidden JSON for AI context.
3. **Revit Element IDs required for traceability.** Every evidence match includes IDs.
4. **Methodology separate from formal report.** Explainability blocks explain status.
5. **No-overclaim policy.** "Met" = AI-assisted first-pass model evidence review, not final compliance.

---

## Quick Start

```powershell
# Revit add-in build (Revit 2023)
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2023 /p:Platform=x64

# Revit add-in build (Revit 2024)
msbuild EMAExtractor/EMAExtractor.csproj /p:RevitYear=2024 /p:Platform=x64

# Backend tests
cd Pipeline\pipeline
py -3.12 -m pytest tests -v

# Frontend
cd Pipeline\pipeline\frontend
npx tsc -b --noEmit
npm run build
```

---

## Validation Commands

```powershell
git status --short
git diff --name-only
cd Pipeline\pipeline && py -3.12 -m pytest tests -v
ollama list
```

---

## Known Limitations

- Revit runtime not yet validated in host Revit (build only).
- Ask EMA AI provider requires local Ollama (qwen3.6:35b).
- Large traceability output collapsed by default.
- Semantic false-positive risk for requirements with weak/mismatched evidence.
- No-overclaim language must always be explicit.
- Not official compliance software.
