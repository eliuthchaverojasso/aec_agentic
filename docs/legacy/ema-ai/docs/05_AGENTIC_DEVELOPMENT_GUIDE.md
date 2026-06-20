# Agentic Development Guide

**Last Updated:** 2026-05-28  
**For:** AI coding agents, AI-assisted developers

This guide contains the essential rules and patterns for AI agents contributing to EMA AI safely.

---

## 🚨 Critical Rules (Non-Negotiable)

### 1. Readiness Semantics

**RULE: Only accepted evidence counts toward readiness coverage.**

- ✓ **Accepted Evidence** = Counts as covered
- ◐ **Candidate Evidence** = Indexed but NOT approved yet = Does NOT count
- ✗ **Rejected Evidence** = Reviewed but not acceptable = Does NOT count

**Why:** Distinguishing between candidate and accepted evidence is the core product differentiation. Confusing them breaks the entire readiness scoring system.

**Where enforced:**
- `Pipeline/pipeline/app/readiness/scoring.py` — The authoritative readiness formula
- `Pipeline/pipeline/app/models.py` — `RequirementEvidence.status` enum
- Database queries: `WHERE status = 'accepted'` only counts

**What NOT to do:**
- ❌ Count candidate evidence in readiness calculation
- ❌ Call accepted evidence "covered" if it's still candidate
- ❌ Auto-approve evidence without explicit human action
- ❌ Skip the `accepted_at`, `accepted_by` audit fields

### 2. Revit Export Behavior

**RULE: Revit export creates model evidence CANDIDATES, not automatic approvals.**

- Revit export runs → Elements extracted → QA/QC rules evaluated → Issues logged
- Model evidence candidates created (indexed from Revit)
- **These remain CANDIDATES until a human reviews and explicitly accepts them**

**Why:** Revit export is data ingestion, not approval. Requirements must still be evaluated by humans.

**Where enforced:**
- `Pipeline/pipeline/app/ingestion/loader.py` — Sets `status='candidate'` on evidence creation
- `Pipeline/pipeline/app/services/model_evidence_resolver.py` — Generates candidates, not approvals

**What NOT to do:**
- ❌ Create evidence with `status='accepted'` from Revit export
- ❌ Bypass evidence review workflow
- ❌ Claim Revit export "covers" a requirement without human acceptance
- ❌ Set `accepted_at` timestamp without human reviewer action

### 3. Database is Source of Truth

**RULE: All state persists to PostgreSQL. No dummy data in production tables.**

- All projects, requirements, evidence, readiness state → PostgreSQL
- No in-memory caches except query optimization
- No temporary data in database (use `.tmp` tables if needed, then clean up)
- Demo data is separate from production

**Where enforced:**
- `Pipeline/pipeline/app/database.py` — SQLAlchemy ORM configuration
- `Pipeline/pipeline/app/models.py` — All persistent models
- `Pipeline/pipeline/db/init.sql` — Schema is source of truth for structure

**What NOT to do:**
- ❌ Leave test projects in the database
- ❌ Use hardcoded UUIDs (use `uuid.uuid4()`)
- ❌ Store temporary data without cleanup
- ❌ Bypass ORM for direct SQL without validation
- ❌ Create database tables without updating models.py

### 4. No AI Auto-Approval

**RULE: AI agents can suggest, explain, and search. They cannot approve official readiness.**

**Allowed:**
- ✓ Suggest evidence candidates
- ✓ Explain why a requirement might be covered
- ✓ Search for related documents
- ✓ Propose rule patterns

**NOT allowed:**
- ❌ Automatically set evidence `status='accepted'`
- ❌ Silently approve without audit trail
- ❌ Bypass human reviewer action
- ❌ Implement auto-approval endpoints

**Where enforced:**
- `/api/v1/projects/{id}/evidence/accept` — Requires human POST with reviewer info
- No background jobs approve evidence
- All acceptance logged with timestamp and reviewer_id

**What NOT to do:**
- ❌ Write endpoints that accept evidence without human request
- ❌ Create scheduled jobs that approve evidence
- ❌ Bypass `accepted_by` field
- ❌ Implement "automatic acceptance" logic

### 5. Protected Files (Do Not Modify)

These files define critical system behavior. Do NOT modify unless explicitly scoped:

| File | Reason |
|------|--------|
| `Pipeline/pipeline/app/database.py` | Database connection & session config |
| `Pipeline/pipeline/db/init.sql` | PostgreSQL schema (source of truth) |
| `Pipeline/pipeline/app/models.py` | ORM models (very careful with schema changes) |
| `Pipeline/pipeline/app/readiness/scoring.py` | Readiness calculation (deterministic) |
| `EMAExtractor/` | Entire Revit add-in folder |
| `docker-compose.ai.yml` | AI stack config (managed by devops) |
| `.env` files | Secrets (never commit) |

**If you need to modify one:**
1. Read [AGENTS.md](../AGENTS.md) for governance rules
2. Use Plan mode first
3. Get explicit approval in your task description
4. Test thoroughly (especially database schema changes)
5. Update related documentation

---

## Before You Edit: Discovery Workflow

### 1. Read Project Rules

Start with these files to understand constraints:

```powershell
# Project development rules
cat AGENTS.md

# Current state and priorities
cat .ai/CURRENT_STATE.md

# Architecture overview
cat docs/03_ARCHITECTURE.md
```

### 2. Check Git History

Understand what was recently changed:

```powershell
# Last 20 commits
git log --oneline -20

# Who touched which files
git log --oneline Pipeline/pipeline/app/models.py | head -10

# See a specific commit
git show abc123def
```

### 3. Search for Related Code

Before writing new code, see if it exists:

```powershell
# Search for "evidence" in backend
grep -r "evidence" Pipeline/pipeline/app --include="*.py"

# Find where a function is defined
grep -r "def calculate_readiness" Pipeline/pipeline/app

# Search frontend
grep -r "evidence" Pipeline/pipeline/frontend/src --include="*.ts" --include="*.tsx"
```

### 4. Use Plan Mode

For non-trivial changes:

```
User: "Add an endpoint to bulk-accept evidence"

You: Use Plan mode first
  1. Explore existing evidence endpoint structure
  2. Check readiness/acceptance patterns
  3. Design the new endpoint
  4. Identify tests needed
  5. Present plan for approval
  6. Implement after approval
```

---

## Safe Development Patterns

### Adding an API Endpoint

**Pattern:** Follow existing structure in `Pipeline/pipeline/app/api/`.

**Template:**

```python
# File: Pipeline/pipeline/app/api/my_feature.py

from fastapi import APIRouter, HTTPException, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import Project, MyModel
from app.schemas import MyRequest, MyResponse

router = APIRouter(prefix="/api/v1/my-feature", tags=["my-feature"])

@router.get("/{id}")
def get_my_feature(id: str, db: Session = Depends(get_db)) -> MyResponse:
    """Get my feature by ID.
    
    Args:
        id: Feature ID
        
    Returns:
        Feature response
        
    Raises:
        404: Feature not found
    """
    feature = db.query(MyModel).filter(MyModel.id == id).first()
    if not feature:
        raise HTTPException(status_code=404, detail="Feature not found")
    return MyResponse.from_orm(feature)

@router.post("")
def create_my_feature(req: MyRequest, db: Session = Depends(get_db)) -> MyResponse:
    """Create a new feature."""
    feature = MyModel(**req.dict())
    db.add(feature)
    db.commit()
    db.refresh(feature)
    return MyResponse.from_orm(feature)
```

**Then register in `main.py`:**

```python
from app.api import my_feature
app.include_router(my_feature.router)
```

**Testing:**

```python
# File: Pipeline/pipeline/tests/test_my_feature.py

def test_get_feature(client, db):
    # Create fixture
    feature = MyModel(id="test-123", ...)
    db.add(feature)
    db.commit()
    
    # Test GET
    response = client.get("/api/v1/my-feature/test-123")
    assert response.status_code == 200
    assert response.json()["id"] == "test-123"

def test_get_feature_not_found(client):
    response = client.get("/api/v1/my-feature/nonexistent")
    assert response.status_code == 404
```

**Don't:**
- ❌ Skip error handling (always return 400/404/500 appropriately)
- ❌ Modify database without validation
- ❌ Forget docstrings
- ❌ Leave test data in database

### Adding a Frontend Page

**Pattern:** Follow existing page structure in `Pipeline/pipeline/frontend/src/pages/`.

**Template:**

```tsx
// File: Pipeline/pipeline/frontend/src/pages/MyFeaturePage.tsx

import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { client } from '../api/client';
import { LoadingState, ErrorState } from '../components/states';

export function MyFeaturePage() {
  const { projectId } = useParams<{ projectId: string }>();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadData = async () => {
      try {
        const result = await client.getMyFeature(projectId!);
        setData(result);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unknown error');
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, [projectId]);

  if (loading) return <LoadingState />;
  if (error) return <ErrorState message={error} />;
  if (!data) return <ErrorState message="No data found" />;

  return (
    <div className="p-6">
      <h1 className="text-3xl font-bold">{data.name}</h1>
      {/* Your content */}
    </div>
  );
}
```

**Add to router:**

```tsx
// File: Pipeline/pipeline/frontend/src/App.tsx

import { MyFeaturePage } from './pages/MyFeaturePage';

// Inside Routes:
<Route path="/projects/:projectId/my-feature" element={<MyFeaturePage />} />
```

**Testing:**

```tsx
import { render, screen } from '@testing-library/react';
import { MyFeaturePage } from './MyFeaturePage';

test('renders feature data', () => {
  render(<MyFeaturePage />);
  expect(screen.getByText(/my feature/i)).toBeInTheDocument();
});
```

**Don't:**
- ❌ Hardcode API URLs (use `client.*` methods)
- ❌ Skip loading/error states
- ❌ Make API calls without useEffect
- ❌ Leave console.log() in code
- ❌ Create TypeScript type errors

### Modifying the Data Model

**Pattern:** Update ORM model, then database schema.

**Steps:**

1. **Update ORM model** (`Pipeline/pipeline/app/models.py`):

```python
class MyModel(Base):
    __tablename__ = "my_model"
    
    id = Column(String, primary_key=True)
    name = Column(String, nullable=False)
    new_field = Column(String, nullable=True, default=None)  # NEW
    created_at = Column(DateTime, default=datetime.utcnow)
```

2. **Update schema** (`Pipeline/pipeline/app/schemas.py`):

```python
class MyModelOut(BaseModel):
    id: str
    name: str
    new_field: Optional[str] = None  # NEW
    created_at: datetime
    
    class Config:
        from_attributes = True
```

3. **Test locally before deploying:**

```powershell
# Stop and wipe database
docker compose down -v

# Restart (re-runs init.sql)
docker compose up -d --build

# Check schema
docker compose exec postgres psql -U ema -d ema_ai -c "SELECT * FROM information_schema.columns WHERE table_name='my_model'"
```

4. **Update migration** (if production schema exists):

See deployment runbook for migration procedures.

**Don't:**
- ❌ Add non-nullable fields without default values
- ❌ Delete fields without understanding what depends on them
- ❌ Change field types without migration
- ❌ Forget to update schemas.py

### Linking Evidence to Requirements

**Pattern:** Use `evidence_service.py` and `model_evidence_resolver.py`.

```python
from app.services.evidence_service import EvidenceService
from app.models import RequirementEvidence

service = EvidenceService(db)

# Create evidence link
evidence = service.create_evidence(
    project_id=project_id,
    requirement_id=requirement_id,
    evidence_type="model",  # or "sheet", "spec", "manual", "hybrid"
    description="Revit export contains this element"
)

# Later, accept evidence (manually, via API)
service.accept_evidence(
    evidence_id=evidence.id,
    reviewer_id=user_id,
    reason="Verified in Revit export"
)

# Calculate readiness (automatically includes accepted evidence only)
from app.readiness.service import ReadinessService
readiness = ReadinessService(db).calculate_project_readiness(project_id)
```

**Don't:**
- ❌ Directly set `status='accepted'` without service
- ❌ Skip the acceptance workflow
- ❌ Hardcode reviewer IDs
- ❌ Leave candidate evidence unreviewed

### Modifying Readiness Scoring

**Pattern:** Modify `Pipeline/pipeline/app/readiness/scoring.py` and `readiness/rules.py`.

**Current formula:**
```
Readiness % = (50% × Requirement Coverage) + (30% × QA/QC Health) + (20% × Sync Freshness)
```

**To change weights:**

```python
# File: Pipeline/pipeline/app/readiness/scoring.py

REQUIREMENT_WEIGHT = 0.50  # Change this
QAQC_WEIGHT = 0.30        # And this
FRESHNESS_WEIGHT = 0.20   # And this

def calculate_overall_readiness(coverage, qaqc, freshness):
    return (
        coverage * REQUIREMENT_WEIGHT +
        qaqc * QAQC_WEIGHT +
        freshness * FRESHNESS_WEIGHT
    )
```

**To add a new component:**

1. Define calculation
2. Add to formula
3. Update readiness action rules
4. Test with sample data
5. Update documentation

**Don't:**
- ❌ Hardcode weights
- ❌ Skip backward-compatibility checks
- ❌ Make readiness non-deterministic
- ❌ Change formula without updating all snapshots

---

## Validation Before Pushing

### Checklist

- [ ] **Backend tests pass:** `pytest Pipeline/pipeline/tests -v` → 126+ tests passing
- [ ] **Frontend TypeScript:** `npm run build` in `Pipeline/pipeline/frontend` → No errors
- [ ] **No secrets in code:** `grep -r "password\|secret\|api_key" --include="*.py" --include="*.tsx" Pipeline/`
- [ ] **No dummy data left:** Database contains only seed data (Denton ISD, Northwest ISD, Rockwall ISD)
- [ ] **Docker healthy:** `docker compose ps` → All services "Up (healthy)"
- [ ] **No broken imports:** Code doesn't reference deleted files
- [ ] **Commit message clear:** Describes WHAT and WHY, not just code changes

### Test Commands

```powershell
# Backend tests
cd "C:\Documents\Hyperghaps EMA\EMA-AI"
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
py -3.12 -m pytest .\Pipeline\pipeline\tests -v

# Frontend build
cd Pipeline\pipeline\frontend
npm run build

# Docker health
cd Pipeline\pipeline
docker compose down
docker compose up -d --build
docker compose ps  # All should be healthy
```

---

## Files as Source of Truth

When uncertain, consult these:

| Question | Source File |
|----------|-------------|
| What are the database tables? | `Pipeline/pipeline/app/models.py` + `Pipeline/pipeline/db/init.sql` |
| What are the API endpoints? | `Pipeline/pipeline/app/main.py` (router includes) |
| How is readiness calculated? | `Pipeline/pipeline/app/readiness/scoring.py` |
| What evidence states exist? | `Pipeline/pipeline/app/models.py` → `RequirementEvidence.status` |
| What are frontend routes? | `Pipeline/pipeline/frontend/src/App.tsx` |
| How do I call the API? | `Pipeline/pipeline/frontend/src/api/client.ts` |
| What environment variables exist? | `Pipeline/pipeline/.env.example` |
| What are the current priorities? | `.ai/CURRENT_STATE.md` + `.ai/NEXT_STEPS.md` |

---

## Forbidden Behaviors

**Never:**

- ❌ Commit secrets (API keys, passwords, tokens)
- ❌ Force-push to main
- ❌ Delete production data without backup
- ❌ Modify `init.sql` without schema migration plan
- ❌ Auto-approve evidence (humans only)
- ❌ Skip error handling
- ❌ Leave `console.log()`, `print()`, `debugger` in code
- ❌ Commit unrelated changes
- ❌ Claim candidate evidence is "covered"
- ❌ Hardcode UUIDs or project IDs
- ❌ Touch EMAExtractor without explicit scope
- ❌ Disable TypeScript strict mode

---

## Quick Reference

### Essential Commands

```powershell
# Clone & setup
git clone https://github.com/shokworks/ema-ai.git
cd "C:\Documents\Hyperghaps EMA\EMA-AI"

# View code
code .  # Open in VS Code

# Check project rules
cat AGENTS.md

# Read current state
cat .ai/CURRENT_STATE.md

# Start dev stack
cd Pipeline\pipeline
docker compose up -d --build

# Run backend tests
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
py -3.12 -m pytest .\Pipeline\pipeline\tests -v

# Run frontend build
cd Pipeline\pipeline\frontend
npm install
npm run build

# View API docs
Start-Process http://localhost:8010/docs
```

### Key Files

```
EMA-AI/
├── AGENTS.md                           # Development rules
├── .ai/CURRENT_STATE.md                # Current priorities
├── Pipeline/pipeline/
│   ├── app/main.py                     # API entry point
│   ├── app/models.py                   # ORM models (source of truth)
│   ├── app/schemas.py                  # Request/response shapes
│   ├── app/readiness/scoring.py        # Readiness formula
│   ├── db/init.sql                     # Database schema
│   ├── docker-compose.yml              # Dev stack
│   ├── frontend/src/api/client.ts      # API client
│   └── tests/                          # pytest suite
└── docs/                               # Documentation
```

---

## Need Help?

- **Confused about readiness?** → Read [docs/readiness/READINESS_SEMANTICS.md](readiness/READINESS_SEMANTICS.md)
- **Don't understand architecture?** → Read [03_ARCHITECTURE.md](03_ARCHITECTURE.md)
- **Want to add a feature?** → Use Plan mode first
- **Found a bug?** → Check [Known Blockers](../.ai/KNOWN_BLOCKERS.md)
- **Stuck on deployment?** → See [Azure Deployment Runbook](runbooks/AZURE_DEPLOYMENT_RUNBOOK.md)

---

**Remember:** This is a pilot MVP. Speed matters, but correctness matters more. When in doubt, ask.

---

**Last Updated:** 2026-05-28
