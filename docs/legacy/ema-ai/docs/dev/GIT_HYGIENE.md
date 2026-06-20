# EMA AI — Git Hygiene

**Last updated:** 2026-06-08

---

## Golden Rule

**Never use `git add .`** — always stage explicit files.

## Exact Paths Only

```powershell
# Good
git add docs/README.md
git add docs/PROJECT_MEMORY.md
git add .ai/PROJECT_CONTEXT.md

# BAD — never do this
git add .
git add -A
git add --all
```

## What NOT to Commit

| Pattern | Reason |
|---------|--------|
| `artifacts/**` | Build output |
| `*.exe`, `*.zip`, `*.log` | Binaries |
| `bin/`, `obj/` | .NET build artifacts |
| `dist/` | Frontend bundle |
| `node_modules/` | Dependencies |
| `.env` | Secrets |
| `TestWorkflow.cs`, `TestWorkflowApp/` | Test scaffolding |
| `test_*.ps1` | Test scripts (unless intentional) |
| `installer_comand.txt` | Notes |
| `*.aux`, `*.fdb_latexmk`, `*.fls`, `*.out` | LaTeX build artifacts |
| Real client `.xlsx`, `.rvt` files | Client data |
| `.pytest_cache/`, `__pycache__/` | Python cache |
| `*.tsbuildinfo` | TypeScript build info |
| `opencode.json` | Local AI config (unless intentional) |

## Commit Message Conventions

```
type(scope): short description

Types: feat, fix, docs, refactor, test, chore, ci
Scope: revit-addin, report, engine, docs, ai, demo, dev
```

### Examples
```
docs(project): update project memory with current state
docs(report): add report spec with explainability requirements
docs(ai): add Ask EMA AI spec with provider chain
docs(architecture): update architecture with Mermaid diagrams
docs(demo): update demo script for Revit-first workflow
```

## Before Every Commit

```powershell
# 1. Check what changed
git status --short

# 2. Confirm only intended files
git diff --name-only

# 3. Stage exact files
git add docs/README.md docs/PROJECT_MEMORY.md

# 4. Review staged diff
git diff --cached --name-only

# 5. Commit
git commit -m "docs(scope): description"
```

## Push

```powershell
git push both feat/revit-first-owner-requirement-checker
```

(Only push when explicitly asked.)

## Branch Hygiene

- Work in small branches
- Use descriptive names: `feat/description`, `fix/description`, `docs/description`
- Keep branch focused on single concern
- Rebase before merging (when applicable)

## For Documentation-Only Commits

Allowed file patterns:
- `docs/**`
- `.ai/**`
- `README.md`
- `AGENTS.md`

Not allowed:
- `EMAExtractor/**`
- `EMAExtractor.Tests/**`
- `Pipeline/**`
- `scripts/**`
- `*.csproj`
- `installer/**`
