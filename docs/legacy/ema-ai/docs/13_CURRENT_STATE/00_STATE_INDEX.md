# Current State Index

**Last Updated:** 2026-05-28  
**Project Status:** Pilot MVP (local dev ready)

## Quick Status

| Component | Status | Notes |
|-----------|--------|-------|
| **Local Development** | ✓ Ready | Docker Compose, npm dev server |
| **Backend API** | ✓ Ready | 14 routers, 50+ endpoints, 126+ tests passing |
| **Frontend Dashboard** | ✓ Ready | 21 pages, TypeScript strict, build passing |
| **PostgreSQL Database** | ✓ Ready | 20+ tables, seed data loaded |
| **Revit Add-in** | ✓ Ready | Ribbon, export, project binding |
| **Testing** | ✓ Ready | pytest 126+, frontend typecheck, Docker smoke |
| **Demo Happy Path** | ✓ Ready | Login → Portfolio → Upload → Evidence → Readiness |
| **Azure Deployment** | ◐ Planned | Architecture documented, not deployed |
| **Production Auth** | ◐ Partial | JWT login works, RBAC/SSO not implemented |
| **Model Viewer** | ✗ Missing | 3D viewer not implemented (images only) |
| **Web Uploader** | ✗ Missing | File upload via landing folder only |
| **Compliance Rules** | ◐ Partial | NEC framework ready, mappings pending |
| **SEION Knowledge Graph** | ◐ Partial | Models defined, inference pending |

---

## Detailed Status by Component

### ✓ Fully Implemented

**Backend API (14 routers, 50+ endpoints)**
- Project CRUD + binding + models
- Export upload + sync logs
- Requirements ingestion + listing
- Evidence creation + state tracking (candidate/accepted/rejected)
- Readiness scoring + snapshots + actions
- Document metadata + retrieval
- QA/QC issue tracking
- Landing zone operations
- Auth (JWT login)
- Debug operation logging

**Frontend Dashboard (21 pages)**
- Login page
- Portfolio (projects overview)
- Project Overview (readiness summary)
- Processing page (sync status, export logs)
- Requirements page (listing + evidence states)
- Trade Readiness (discipline-level breakdown)
- Issues page (QA/QC findings)
- Evidence page (review + accept/reject workflow)
- Readiness page (overall score + gaps + actions)
- Documents page (drawing reels, specifications)
- Appearance page (Liquid Glass theming)
- Settings, roles/permissions, debug logs (placeholders or partial)

**Database (PostgreSQL)**
- 20+ tables: projects, requirements, evidence, readiness, issues, operations, exports, etc.
- Seed data: 3 organizations (Denton ISD, Northwest ISD, Rockwall ISD)
- ORM models in SQLAlchemy (models.py)
- Schema source of truth (init.sql)

**Revit Add-in (C# / .NET 4.8)**
- Ribbon with 40+ commands
- Export to JSON workflow
- Project binding (local config)
- Local settings management
- Icon library (60+ PNGs)

**Testing**
- pytest: 126+ tests passing
- Frontend: TypeScript strict mode passing
- Docker: Health checks all green
- Browser: Smoke tests for core routes passing

### ◐ Partially Implemented

**Authentication**
- ✓ JWT login endpoint works
- ✓ Token validation in API
- ✗ RBAC (role-based access control) not implemented
- ✗ SSO/OAuth2 not implemented
- Note: Local demo auth (hardcoded users) sufficient for MVP

**Document Ingestion**
- ✓ Landing zone scanning (PDF, drawings, specs)
- ✓ File type classification
- ✓ Document metadata extraction
- ◐ OCR (placeholder, vision API not integrated)
- ✗ PDF/DOCX full-text parsing (manual only)

**SEION Knowledge Graph**
- ✓ Data models defined (products, equipment, interconnections, outcomes)
- ✓ Training framework ready
- ✓ Prediction schema defined
- ✗ Live inference not running
- Note: Advisory only, never auto-approval

**Compliance Rules**
- ✓ NEC corpus import framework
- ✓ Rule schema and storage
- ✗ Rule-to-requirement mapping not done
- ✗ Compliance checking workflow not integrated

**CI/CD**
- ✓ GitHub Actions workflow (pytest on push/PR)
- ✗ Not enforced on main (manual QA still required)

### ✗ Not Implemented Yet

**Azure Deployment**
- Architecture documented (Container Apps, PostgreSQL Flexible, Static Web Apps)
- Not deployed (post-MVP goal)

**Model Viewer**
- 3D visualization not implemented
- IFC/BCF support deferred
- Current: Images and descriptions only

**Web File Uploader**
- No browser file upload UI
- Workaround: Manual landing folder structure

**Advanced Evidence Types**
- Manual evidence review (note + proof)
- Visual evidence (photos, markups)
- Video evidence

**GraphRAG**
- Deferred to P3 (advisory only, not auto-approval)
- Graph database not deployed

**Real-time Features**
- WebSocket connections not implemented
- Manual refresh required

---

## What Works in Demo

### Happy Path

1. ✓ Login (demo@ema.local / demo)
2. ✓ View Portfolio (list of projects)
3. ✓ Open Project Overview (readiness summary)
4. ✓ Upload Owner Requirements (Excel → database)
5. ✓ Upload Revit Export (JSON → element extraction → readiness recalc)
6. ✓ View Requirements page (see evidence states)
7. ✓ Review evidence candidates (from Revit export)
8. ✓ Accept/reject evidence (audit trail recorded)
9. ✓ See readiness updated (percentage reflects accepted evidence)
10. ✓ View Executive Overview (summary for stakeholders)

### Deep Dives

- Requirements → filtering by discipline
- Evidence → state transitions (candidate → accepted/rejected)
- Readiness → score breakdown (requirement coverage + QA/QC + freshness)
- Issues → linked to elements (from Revit)
- Operations → audit trail of all ingestions

### Limitations in Demo

- ⚠️ **Evidence Detail page:** Placeholder, don't click
- ⚠️ **Model viewer:** No 3D, images only
- ⚠️ **PDF reports:** Download not functional
- ⚠️ **Advanced filters:** Basic filtering works, complex queries don't
- ⚠️ **SEION predictions:** Model trained but inference not live
- ⚠️ **Compliance checking:** UI present but no live rules
- ⚠️ **Manual evidence:** Creation works but UI sparse

---

## Known Issues & Blockers

### Critical (P0)

None currently. MVP is ready for demo.

### Important (P1)

- Web uploader would improve UX (currently manual landing folder)
- Evidence detail page needs real content
- Compliance rule mappings incomplete

### Planned (P2)

- RBAC for multi-user deployments
- SSO/OAuth2 for production
- Model viewer integration

---

## Metrics

| Metric | Value |
|--------|-------|
| Backend Tests Passing | 126+ |
| Test Coverage (backend) | ~70% |
| Frontend Pages | 21 |
| API Endpoints | 50+ |
| Database Tables | 20+ |
| Revit Ribbon Commands | 40+ |
| Code Lines (backend) | ~20,000 |
| Code Lines (frontend) | ~15,000 |
| Documentation Files | 50+ (new structure) |

---

## Priorities by P-Level

### P0 (Completed — MVP Closure, 2026-05-26)
- ✓ Backend and frontend stable
- ✓ Happy path validated
- ✓ Demo script ready
- ✓ Clean seed data
- ✓ Test environment isolated

### P1 (Active)
- [ ] Deploy to Azure
- [ ] Web file uploader
- [ ] Evidence detail page with full content
- [ ] PDF/spec parsing improvements

### P2: Production Hardening (Month 1)
- [ ] RBAC implementation
- [ ] SSO/OAuth2
- [ ] Load testing (PostgreSQL, API, frontend)
- [ ] Backup/restore procedures
- [ ] Security audit

### P3+: Enterprise Features (Month 2+)
- [ ] GraphRAG integration (advisory only)
- [ ] Model viewer (3D/IFC)
- [ ] Advanced compliance workflows
- [ ] Real-time collaboration

---

## Test Status

| Test Suite | Status | Count | Command |
|-----------|--------|-------|---------|
| Backend pytest | ✓ Passing | 126+ | `pytest Pipeline/pipeline/tests -v` |
| Frontend TypeScript | ✓ Passing | — | `npm run build` |
| Docker Compose | ✓ Healthy | 2 services | `docker compose ps` |
| Browser Smoke | ✓ Passing | 5 routes | `./scripts/qa/smoke-*.ps1` |

---

## File Locations

Key documentation by topic:

| Topic | File(s) |
|-------|---------|
| Project Overview | `00_OVERVIEW.md` |
| Local Setup | `01_QUICKSTART_LOCAL.md` |
| Docker Setup | `02_QUICKSTART_DOCKER.md` |
| Architecture | `03_ARCHITECTURE.md` |
| Development Rules | `05_AGENTIC_DEVELOPMENT_GUIDE.md` |
| API Reference | `06_API_REFERENCE/` |
| Frontend Guide | `07_FRONTEND_GUIDE/` |
| Backend Guide | `08_BACKEND_GUIDE/` |
| Revit Add-in | `09_REVIT_ADDIN/` |
| Deployment | `10_DEPLOYMENT/` |
| Testing | `11_TESTING/` |
| Operations | `12_OPERATIONS/` |
| Demo Runbook | `12_OPERATIONS/03_DEMO_RUNBOOK.md` |

---

## Questions?

- **How do I run it locally?** → [01_QUICKSTART_LOCAL.md](../01_QUICKSTART_LOCAL.md)
- **What's the architecture?** → [03_ARCHITECTURE.md](../03_ARCHITECTURE.md)
- **How do I run the demo?** → [Demo Runbook](../demo/DEMO_RUNBOOK.md)
- **What are the rules for AI agents?** → [05_AGENTIC_DEVELOPMENT_GUIDE.md](../05_AGENTIC_DEVELOPMENT_GUIDE.md)
- **What's missing?** → See "Not Implemented Yet" above

---

**Last Updated:** 2026-05-28
