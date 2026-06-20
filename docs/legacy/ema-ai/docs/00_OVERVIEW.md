# EMA AI Overview

**Last Updated:** 2026-05-28  
**Project Status:** Pilot MVP (local dev ready, Azure pending)

## What is EMA AI?

EMA AI is a web-based dashboard that helps identify whether a construction project is ready against Owner Requirements by connecting project data, evidence, gaps, and readiness metrics.

### The Problem

Construction projects have:
- **Owner Requirements** (specifications, standards)
- **Project Data** (Revit models, drawings, specifications)
- **QA/QC Issues** (validation findings)

But they lack a unified way to **see what evidence exists**, **track gaps**, and **score readiness**.

### The Solution

EMA AI connects these pieces:

```
Revit Export → Owner Requirements → Evidence Gathering → Readiness Score → Dashboard
```

1. Upload Owner Requirements (Excel spreadsheet)
2. Export Revit model data (JSON from Revit add-in)
3. System finds evidence (model elements, drawings, manual review)
4. Mark evidence as accepted/rejected
5. View readiness dashboard (% of requirements with accepted evidence)

### One-Sentence Value

*EMA AI helps teams identify whether a project is ready against Owner Requirements by aggregating Revit data, requirements, evidence, gaps, and readiness status into a single interactive dashboard.*

---

## Who Is It For?

| Role | Use Case |
|------|----------|
| **Project Manager** | Track readiness progress, identify blockers, manage handoff |
| **Owner Rep** | Verify requirements are met, approve evidence |
| **Design Team** | See what requirements exist and what's missing |
| **QA/QC Lead** | Track issues, link to requirements, monitor resolution |
| **Contractor** | Understand deliverable expectations, plan work |
| **Facility Manager** | Document as-built status, plan operations |

---

## What EMA AI Does

✅ **Implemented**
- Project setup and management (CRUD)
- Owner Requirements ingestion (from Excel)
- Revit export import (JSON streaming, element extraction)
- Evidence tracking (candidates, accepted, rejected states)
- Readiness scoring (deterministic formula: 50% requirements + 30% QA/QC + 20% freshness)
- Web dashboard (portfolio, project overview, requirements, evidence, readiness)
- Database persistence (PostgreSQL)
- Local Docker stack
- 126+ backend tests passing
- Frontend TypeScript strict mode passing

⏳ **Partial**
- Compliance rules (framework ready, NEC mapping pending)
- SEION knowledge graph (models defined, inference pending)
- Document ingestion (scanning works, OCR placeholder)
- Authentication (JWT login works, RBAC pending)

❌ **Not Implemented Yet**
- Azure deployment (architecture designed, not deployed)
- Model viewer (3D visualization)
- Web file uploader (manual landing folder only)
- PDF/DOCX parsing (manual indexing)
- GraphRAG (deferred, advisory only)

---

## What EMA AI Is NOT

### ❌ Not Production Compliance Software
This is a **pilot MVP**, not official certification. EMA AI helps identify readiness but does not legally certify compliance.

### ❌ Not AI Auto-Approval
Humans make all acceptance decisions. **AI is advisory only**—it suggests, explains, and searches. Evidence acceptance requires explicit reviewer action and audit trail.

### ❌ Not Automatic
Every step requires explicit action:
- **Upload** requirements manually
- **Export** from Revit manually
- **Link** evidence manually or semi-automatically
- **Accept** evidence manually
- **Approve** readiness manually

### ❌ Not Enterprise-Ready Yet
- No RBAC (role-based access control)
- No SSO (single sign-on)
- Local demo auth only (hardcoded users)
- Not audited for security
- Not load-tested

---

## Core Concepts

### Readiness Semantics

**Accepted Evidence** ✓
- Counts toward requirement coverage
- Reviewed and approved by human
- Audit trail recorded (timestamp, approver, reason)

**Candidate Evidence** ◐
- Indexed automatically (from Revit export, document scan)
- NOT yet accepted
- Does NOT count as covered
- Awaiting reviewer decision

**Rejected Evidence** ✗
- Reviewed but not acceptable
- Does NOT count as covered
- Audit trail recorded

### The Readiness Formula

```
Readiness % = (50% × Requirement Coverage) + (30% × QA/QC Health) + (20% × Sync Freshness)

Where:
  Requirement Coverage = # accepted evidence / total requirements
  QA/QC Health = (100 - penalty %) where penalties come from issues
  Sync Freshness = decay over time (100% if ≤1 day, 50% if ≤14 days, 25% if older)
```

---

## Tech Stack

### Frontend
- **React 19** with TypeScript 5.7
- **Vite 6** (dev server @ localhost:5173)
- **Tailwind CSS 3.4** for styling
- **Recharts** for visualizations
- **Lucide React** for icons

### Backend
- **FastAPI 0.115** (ASGI web framework)
- **Python 3.12** with SQLAlchemy 2.0 ORM
- **PostgreSQL 16** (database @ localhost:5432)
- **pytest** (126+ tests passing)
- **Uvicorn** ASGI server (@ localhost:8010)

### Revit Add-in
- **C# / .NET Framework 4.8** (Windows only)
- **Revit 2023–2026** compatible (x64)
- **System.Text.Json** for export serialization
- **WPF** for UI windows

### Infrastructure
- **Docker Compose** (development orchestration)
- **GitHub Actions** (CI on push/PR)
- **Azure** (target: Static Web Apps + Container Apps + PostgreSQL Flexible)

---

## Project Structure

```
EMA-AI/
├── Pipeline/pipeline/
│   ├── app/                  # FastAPI backend
│   ├── frontend/             # React 19 SPA
│   ├── tests/                # pytest suite
│   ├── db/init.sql           # PostgreSQL schema
│   ├── docker-compose.yml    # Dev stack
│   └── landing/              # Sample project data
├── EMAExtractor/             # Revit add-in (C#)
├── docs/                     # Documentation (you are here)
├── scripts/                  # PowerShell utilities
└── README.md                 # This project README
```

---

## MVP Demo Readiness

### What Works Now (as of 2026-05-26 MVP closure)
- Login to dashboard
- Select project
- View portfolio and project overview
- Upload Owner Requirements (Excel)
- Upload Revit export (JSON)
- Review evidence candidates
- Accept/reject evidence
- View updated readiness score
- See executive overview

### ⚠️ Partial (Don't Click)
- Evidence detail pages (placeholder UI)
- PDF report download (pending)
- Model viewer (images only, no 3D)
- Advanced filters (basic filtering works)
- SEION predictions (no live inference)

### ❌ Missing (Skip in Demo)
- Web file uploader (use landing folder instead)
- Model 3D viewer
- Mobile app

---

## Key Constraints

1. **Revit export does NOT auto-approve** — Creates candidate evidence only
2. **Candidate evidence is not "covered"** — Only accepted evidence counts
3. **Database is source of truth** — All state in PostgreSQL
4. **No dummy data in production** — Demo uses separate schema
5. **AI suggests, humans decide** — No auto-approval

---

## Getting Started

Choose your path:

### I'm a New Developer
→ [01_QUICKSTART_LOCAL.md](01_QUICKSTART_LOCAL.md) → [03_ARCHITECTURE.md](03_ARCHITECTURE.md) → [docs/README.md](README.md)

### I'm Running the Demo
→ [Demo Runbook](demo/DEMO_RUNBOOK.md) → [Local Dev Setup](01_QUICKSTART_LOCAL.md)

### I'm Deploying to Azure
→ [Azure Deployment Runbook](runbooks/AZURE_DEPLOYMENT_RUNBOOK.md) (planned; not yet deployed)

### I'm Using Revit
→ [Revit Add-in Installation](revit/ADDIN_INSTALLATION.md) → [Revit Command Map](revit/COMMAND_MAP.md)

### I'm an AI Coding Agent
→ [05_AGENTIC_DEVELOPMENT_GUIDE.md](05_AGENTIC_DEVELOPMENT_GUIDE.md) → AGENTS.md

---

## Current Status Summary

| Component | Status | Notes |
|-----------|--------|-------|
| Backend API | ✓ Ready | 14 routers, 50+ endpoints, health checks |
| PostgreSQL | ✓ Ready | Schema, seed data, 20+ tables |
| Frontend | ✓ Ready | 21 pages, TypeScript strict, build passing |
| Testing | ✓ Ready | 126+ tests passing |
| Docker | ✓ Ready | Dev stack working |
| Revit Add-in | ✓ Ready | Ribbon, export, project binding |
| Demo Readiness | ✓ Ready | Happy path validated |
| Azure Deployment | ◐ Planned | Architecture ready, not deployed |
| Production Auth | ◐ Partial | JWT works, RBAC pending |
| Compliance Rules | ◐ Partial | Framework ready, mappings pending |

---

## FAQ

**Q: Is this production-ready?**  
A: No. This is a pilot MVP for demo and evaluation. Production hardening (auth, RBAC, load testing, audit logging) comes in P1–P2.

**Q: Can EMA AI approve requirements on its own?**  
A: No. AI is advisory only. All evidence acceptance is manual and tracked in the audit log.

**Q: What if I upload a requirement twice?**  
A: Duplicates are deduplicated by file hash. The system is idempotent.

**Q: Can I use this without Revit?**  
A: Yes. You can upload requirements and manually link evidence. Revit export is optional.

**Q: How do I reset the database?**  
A: Stop Docker Compose, remove the database volume, and restart: `docker compose down -v && docker compose up -d --build`.

**Q: What's the difference between candidate and accepted evidence?**  
A: Candidate is indexed but awaiting review. Only accepted evidence counts toward readiness.

---

**Next:** [01_QUICKSTART_LOCAL.md](01_QUICKSTART_LOCAL.md) or [README.md](README.md) for full docs index.
