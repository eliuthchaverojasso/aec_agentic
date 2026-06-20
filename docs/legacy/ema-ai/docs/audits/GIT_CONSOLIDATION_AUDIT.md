# EMA AI — Git Consolidation Audit

**Date:** 2026-06-15 (UTC) · **Branch at audit:** `docs/project-reference-reconciliation` · **HEAD:** `b0cb42b`

This audit maps the real Git state and recommends a consolidation. **No remote
state, branch, tag, release, or PR is modified by this document.** The exact
commands to act on it (after authorization) are in
[../runbooks/GIT_CONSOLIDATION_RUNBOOK.md](../runbooks/GIT_CONSOLIDATION_RUNBOOK.md).

---

## 1. Remotes

| Remote | Fetch URL | Push URL | Verdict |
|---|---|---|---|
| `origin` | `echavero-shock/EMA-AI` | `echavero-shock/EMA-AI` | **CANONICAL** |
| `shock` | `echavero-shock/EMA-AI` | `echavero-shock/EMA-AI` | redundant alias of origin |
| `both` | `echavero-shock/EMA-AI` | **`shokworks/ema-ai`** | **UNSAFE** — split fetch/push |
| `shokworks` | `shokworks/ema-ai` | `shokworks/ema-ai` | legacy mirror |

**GIT-05 (HIGH):** `git push both <branch>` writes to the **legacy** `shokworks/ema-ai`
repository while fetches come from the canonical one. This is an easy mistake to
make and corrupts the legacy mirror or leaks work to the wrong repo. Recommend
removing `both` and `shock`, keeping only `origin` (canonical) and optionally
`shokworks` as an explicit, clearly-named read-only legacy reference.

---

## 2. Branch & tracking state

- `origin/HEAD -> origin/main` (canonical default branch is `main`). ✔
- **Local `main` tracks `shokworks/main`, not `origin/main`** (GIT-02, HIGH). Local `main` @ `9f20d31`.
- Current branch `docs/project-reference-reconciliation` @ `b0cb42b` tracks `origin/docs/project-reference-reconciliation`. ✔
- Formally audited product baseline: `feat/revit-first-owner-requirement-checker` @ `ae6ded2` (2026-06-10).
- ~14 other local branches point at `1674ad8` and are reported "behind `shokworks/main` by 33".
- `codex/cloud-url-defaults` @ `52e4dbf` carries `fix(readiness): treat empty model QA health as passing baseline` — directly relevant to the failing `qaqc_health` test (BE-QA-HEALTH). Investigate before integrating: the "passing baseline" change conflicts with product-truth #8.

## 3. Divergence summary

| Branch | Commit | Relationship | Action |
|---|---|---|---|
| `main` (canonical) | origin/main | default branch | Confirm as canonical; fix local tracking |
| `feat/revit-first-owner-requirement-checker` | `ae6ded2` | audited product baseline | Candidate to fold into main via reviewed PR |
| `docs/project-reference-reconciliation` | `b0cb42b` | docs branch that **also** carries ~5k LOC runtime audit code | Split: docs -> docs PR; runtime -> feat PR (GIT-04) |
| `codex/cloud-url-defaults` | `52e4dbf` | out-of-tree readiness change | Investigate vs product-truth #8 before merge |
| `*` @ `1674ad8` (×14) | — | stale, "behind by 33" | Prune after confirming nothing unique remains |

## 4. History/artifact findings

- **Tracked binaries (GIT-07, MEDIUM):** `installer/package/payload/EMA AI/EMAExtractor.dll`, `EMAExtractor.pdb`, and ~9 dependency DLLs are committed. Policy (AGENTS.md) forbids committing DLLs/PDBs. `.gitignore` cannot untrack already-tracked files.
- **Tracked build-info (GIT-08):** `Pipeline/pipeline/frontend/tsconfig.app.tsbuildinfo` is tracked despite matching the `*.tsbuildinfo` ignore — committed before the ignore rule.
- **No tags exist:** there is no immutable release anchor. A pilot release must be tied to a tag + immutable artifacts.
- **Stashes:** 2 stashes preserved (`temp-pre-consolidation`, qdrant WIP). Review before any branch pruning.

## 5. Recommended canonical model

1. **Canonical branch:** `main` on `echavero-shock/EMA-AI`.
2. **Remotes:** keep `origin` only; drop `both` + `shock`; (optional) keep `shokworks` read-only.
3. **Integration order:** (a) split `b0cb42b` into a docs PR and a runtime feat PR; (b) reconcile `feat/revit-first-owner-requirement-checker` into `main` via reviewed PR; (c) investigate then integrate or drop `codex/cloud-url-defaults`; (d) prune stale `1674ad8` branches.
4. **Hygiene:** untrack DLL/PDB + tsbuildinfo; regenerate at build/package time.
5. **Release anchoring:** introduce annotated tags (e.g. `v0.x.x-pilot`) tied to immutable artifacts once pilot gates pass.
6. **Branch protection (proposed):** require the reference validator, C# tests, frontend build, and the backend suite (with DB) as status checks on `main`.

All actions above are **gated on user authorization** (remote writes, tracked-state
changes). See the runbook.
