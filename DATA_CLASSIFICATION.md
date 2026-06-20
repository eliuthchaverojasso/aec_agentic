# DATA_CLASSIFICATION.md

**Generated:** 2026-06-20 (Phase 0 deliverable) · **Method:** tracked-file scan + content sampling
**Scope:** what *customer-derived* or *sensitive* data is tracked in Git, and how it should be
handled. This governs whether parts of the tree can be published (open standard, public repo,
marketing) without leaking client information.

---

## 0. Headline

- ✅ **No binary client artifacts are tracked.** `git ls-files` finds no `.xlsx/.xls/.pdf/.rvt/
  .rfa/.ifc` — `.gitignore` is doing its job. No raw owner-requirement workbooks, no Revit models,
  no exported reports as binaries.
- ⚠️ **Customer-identifying *text/JSON* is tracked, concentrated in `docs/legacy/ema-ai/docs/demo/`.**
  This is preserved legacy history, but it names a real school district and a real person, and two
  large JSON files are derived from a real Revit model. These must be reviewed before any public
  publication.

## 1. Sensitivity tiers

| Tier | Meaning |
| --- | --- |
| **C3 — Customer-derived** | Traceable to a specific real client/project (names, model structure, real reports). Must not be published without consent + scrubbing. |
| **C2 — Internal** | Proprietary product IP (taxonomies, methodology, rules). Not for public release, but not client-identifying. |
| **C1 — Shareable** | Generic scaffolding, schemas, synthetic fixtures. Safe to publish. |

## 2. Findings by location

### C3 — Customer-derived (review before any external use)

| Path | What it is | Action |
| --- | --- | --- |
| `docs/legacy/ema-ai/docs/demo/EMA_AI_REVIT_CATEGORY_FAMILY_TYPE_INVENTORY.json` (1.27 MB) | Category/family/type inventory **extracted from a real Revit model** | Confirm it carries no client-identifying family names; treat as client-derived; keep out of public artifacts |
| `docs/legacy/ema-ai/docs/demo/EMA_AI_REVIT_PARAMETER_INVENTORY.json` (354 KB) | Parameter inventory from a real model | Same as above |
| `docs/legacy/ema-ai/docs/demo/NISD_LOCAL_DEMO_SCRIPT.md` | Demo script naming a real district (NISD) | Scrub district name or keep internal-only |
| `docs/legacy/ema-ai/docs/demo/EMA_AI_CLIENT_NARRATIVE.md` | Client narrative | Internal-only |
| `docs/legacy/ema-ai/docs/demo/ema_ai_owner_requirements_report_paul_ready.tex` | A report naming a real individual ("paul") | Internal-only; remove personal name before reuse |
| `docs/legacy/ema-ai/...` (various, ~30 files mention district/client names) | Legacy demo/audit docs | Covered by the blanket rule in §3 |

### C2 — Internal product IP (not client-identifying)

| Path | What it is |
| --- | --- |
| `docs/legacy/ema-ai/docs/demo/EMA_AI_UNIVERSAL_REQUIREMENT_TAXONOMY.json`, `EMA_AI_REQUIREMENT_TYPE_TAXONOMY.json`, `EMA_AI_REQUIREMENT_PATTERN_LIBRARY.json`, `EMA_AI_RULES_TABLES.*` | Requirement taxonomies, pattern library, rule tables — proprietary methodology |
| `data/taxonomies/ema-ai/requirement_type_matrix.json` | Taxonomy (product IP) |
| `EMA_AI_REQUIREMENT_ENGINE_METHODOLOGY.md`, planning/backlog docs | Internal methodology/roadmap |

### C1 — Shareable

| Path | What it is |
| --- | --- |
| `standard/schemas/*`, `standard/events/*`, `standard/policies/rules/*`, `standard/conformance/*` | Contracts, synthetic rule fixtures — designed to be the open standard |
| `apps/control-plane-api/tests/fixtures/sample_evaluation_bundle.json` | Test fixture — **verify** it uses synthetic, not client, names (it currently matches a district-name scan; confirm/scrub) |
| `packages/python/*` | First-party scaffolding |

## 3. Handling rules

1. **`docs/legacy/ema-ai/**` is internal historical context, not a publication source.** It is
   preserved deliberately (see [`CURRENT_STATE.md`](CURRENT_STATE.md) §2) but must never be copied
   into a public repo, the open standard, or marketing without per-file scrubbing.
2. **Before publishing the open standard** (`standard/`), confirm every fixture and example uses
   synthetic data. Today `sample_evaluation_bundle.json` and three test modules match a real
   district-name scan — verify these are synthetic or scrub them.
3. **The two large Revit inventory JSONs are client-derived** and should be excluded from any
   distributable bundle or replaced with a synthetic equivalent for demos.
4. **No new C3 data** may be committed. The golden-project fixtures the register calls for (§20.5)
   must be synthetic or contractually cleared, not raw client exports.

## 4. Open follow-ups

- [ ] Confirm consent/usage rights for the NISD-derived demo content, or scrub identifiers.
- [ ] Verify `tests/fixtures/sample_evaluation_bundle.json` + the 3 matching test modules use
      synthetic names; scrub if not.
- [ ] Add a pre-publication checklist that diffs any to-be-published path against this classification.
- [ ] Add secret/PII scanning to CI (register §19, §21) to keep C3 data from re-entering the tree.
