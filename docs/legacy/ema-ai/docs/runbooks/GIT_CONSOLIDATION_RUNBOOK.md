# EMA AI — Git Consolidation Runbook

**Companion to** [../audits/GIT_CONSOLIDATION_AUDIT.md](../audits/GIT_CONSOLIDATION_AUDIT.md).

> ⚠️ Every command here changes remote state, tracked state, or branch topology.
> **Do not run any of these without explicit user authorization.** They are written
> for copy-paste execution once approved. Each step is reversible as noted.

Pre-flight (always):

```powershell
git fetch --all --prune
git status
git stash list      # 2 stashes exist — confirm before pruning branches
git branch -vv
```

---

## Step 1 — Remove unsafe/redundant remotes (GIT-05)

Reversible (re-add with `git remote add`).

```powershell
git remote remove both     # split fetch/push -> pushes to legacy shokworks/ema-ai
git remote remove shock    # redundant alias of origin
# Optional: keep shokworks as an explicit read-only legacy reference, or remove it:
# git remote remove shokworks
git remote -v              # expect: origin (echavero-shock/EMA-AI) [+ shokworks if kept]
```

## Step 2 — Fix local `main` tracking (GIT-02)

Reversible (`git branch --set-upstream-to=shokworks/main main`).

```powershell
git checkout main
git branch --set-upstream-to=origin/main main
git status                 # expect: tracking origin/main
# Reconcile content only after reviewing the diff:
git log --oneline --left-right origin/main...main
```

## Step 3 — Split the docs/runtime mixing on `b0cb42b` (GIT-04)

`b0cb42b` mixed docs + ~5k LOC runtime audit code on a `docs/*` branch. Carry the
runtime onto a product branch; keep docs on the docs branch.

```powershell
# Create the product feature branch from the audited baseline:
git checkout -b feat/requirement-audit-bundle feat/revit-first-owner-requirement-checker
# Cherry-pick / port ONLY the runtime files from b0cb42b (review each):
#   EMAExtractor/Requirements/Audit/*, EMAExtractor/Reporting/OwnerRequirementHtmlReportGenerator.cs,
#   EMAExtractor/Services/RequirementCheckWorkflowService.cs, EMAExtractor/Reporting/EvidenceEmbedLimits.cs,
#   Pipeline/pipeline/app/{api/requirement_audits.py,services/requirement_audit_ingest.py,models.py,schemas.py,main.py},
#   Pipeline/pipeline/db/migrations/20260615_001_requirement_audit_v1.sql,
#   Pipeline/pipeline/frontend/src/{pages/RequirementAuditsPage.tsx,types.ts,api/client.ts,components/Layout.tsx,App.tsx}
# Validate on the new branch before opening a PR:
dotnet test EMAExtractor.Tests\EMAExtractor.Tests.csproj
cd Pipeline\pipeline; docker compose up -d; python scripts\apply_migrations.py; python -m pytest tests -q
```

## Step 4 — Untrack committed binaries (GIT-07) and build-info (GIT-08)

Reversible until committed. Confirm the installer build regenerates the payload first.

```powershell
git rm --cached "installer/package/payload/EMA AI/EMAExtractor.dll" `
                "installer/package/payload/EMA AI/EMAExtractor.pdb"
git rm --cached "installer/package/payload/EMA AI/"*.dll
git rm --cached Pipeline/pipeline/frontend/tsconfig.app.tsbuildinfo
# Add ignore rules (DLL/PDB scoped to the installer payload to avoid surprising other dirs):
# echo "installer/package/payload/**/*.dll" >> .gitignore
# echo "installer/package/payload/**/*.pdb" >> .gitignore
git status
```

> History rewriting (e.g. `git filter-repo`) to purge the DLL/PDB from past commits
> is a separate, higher-risk decision — do **not** rewrite shared history without
> explicit sign-off and a coordinated force-push window.

## Step 5 — Investigate `codex/cloud-url-defaults` (BE-QA-HEALTH)

```powershell
git log --oneline -5 codex/cloud-url-defaults
git diff feat/revit-first-owner-requirement-checker...codex/cloud-url-defaults -- "*readiness*"
```

The commit makes `qaqc_health_score` treat an empty model as a "passing baseline"
(returns 100.0). This **conflicts with product-truth #8** (missing data ≠ perfect
score) and the failing regression test. Resolve the product question before
integrating — do not silently adopt the 100.0 behavior.

## Step 6 — Prune stale branches (after confirming nothing unique remains)

```powershell
# For each stale branch at 1674ad8, confirm it has no unique commits vs main:
git log --oneline main..<branch>     # empty output == safe to delete
git branch -d <branch>               # use -D only after the above is empty
```

## Step 7 — Branch protection (GitHub UI / API — proposed)

On `main`, require these status checks before merge:

- `validate_project_references` (drift gate — now green)
- C# tests (`dotnet test`)
- Frontend typecheck + build
- Backend suite with a Postgres service (after VAL-01)

Plus: require PR review, linear history, no force-push to `main`.

## Step 8 — Release anchoring

```powershell
# Only after pilot gates pass and artifacts are immutable:
git tag -a v0.x.x-pilot.1 -m "EMA AI pilot release" <reviewed-sha>
# Push tags only with authorization:
# git push origin v0.x.x-pilot.1
```

---

## Rollback quick reference

| Step | Undo |
|---|---|
| 1 | `git remote add both <url>` / `git remote add shock <url>` |
| 2 | `git branch --set-upstream-to=shokworks/main main` |
| 3 | delete the new branch: `git branch -D feat/requirement-audit-bundle` |
| 4 | `git restore --staged <paths>` (before commit) |
| 6 | recreate from reflog: `git branch <name> <sha-from-reflog>` |
| 8 | `git tag -d <tag>` (and delete remote tag only if pushed + authorized) |
