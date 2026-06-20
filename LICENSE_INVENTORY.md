# LICENSE_INVENTORY.md

**Generated:** 2026-06-20 (Phase 0 deliverable) · **Method:** direct read of dependency manifests
**Status:** first pass. License strings below are the commonly-published licenses for each
package at the pinned version; they are **not** a substitute for an automated scan. Before any
external distribution, run a license scanner (`pip-licenses`, `pnpm licenses list`, `dotnet
list package --include-transitive` + a license tool) and reconcile against this file — see the
register's "license scanning" CI item.

This repository's own code is **proprietary** (see [`LICENSE`](LICENSE) — all rights reserved).
Everything below is *third-party* and governs redistribution and deployment.

---

## 1. Python — `apps/control-plane-api` (`requirements.txt`)

| Package | Pinned | License (typical) | Notes |
| --- | --- | --- | --- |
| fastapi | 0.115.5 | MIT | |
| uvicorn[standard] | 0.32.1 | BSD-3-Clause | `[standard]` pulls httptools, websockets, uvloop (BSD/MIT/Apache mix) |
| pydantic | 2.10.3 | MIT | core in Rust (pydantic-core, MIT) |
| pydantic-settings | 2.6.1 | MIT | |
| sqlalchemy | 2.0.36 | MIT | |
| **psycopg[binary]** | 3.2.3 | **LGPL-3.0-only** | ⚠️ copyleft. Used as a library (dynamic). LGPL obligations apply to the psycopg component if distributed. The `[binary]` extra bundles libpq. |
| python-multipart | 0.0.17 | Apache-2.0 | |
| ijson | 3.3.0 | BSD-3-Clause | optional C backend (yajl) |
| python-dotenv | 1.0.1 | BSD-3-Clause | |
| openpyxl | 3.1.5 | MIT | |
| pytest | 8.3.4 | MIT | dev/test only |
| boto3 | 1.35.92 | Apache-2.0 | pulls botocore (Apache-2.0) |

**Flag:** `psycopg` (LGPL-3.0) is the only copyleft runtime dependency. For a proprietary
distributed product this is generally fine (LGPL permits dynamic use), but the obligation to
allow relinking/replacement of the LGPL component must be honored. Confirm with counsel before
shipping a closed binary that statically embeds it.

## 2. First-party Python packages — `packages/python/*`

`control-plane-core`, `policy-engine`, `evidence-engine`, `organism-runtime`, `agent-runtime`,
`connector-sdk`, `reporting` — **first-party**, proprietary, standard-library only (no external
runtime dependencies declared). No third-party license obligations introduced.

## 3. JavaScript / TypeScript — `apps/web-console` (`package.json`)

| Package | Range | License (typical) |
| --- | --- | --- |
| react / react-dom | ^19.0.0 | MIT |
| vite | ^6.0.1 | MIT |
| @vitejs/plugin-react | ^5.1.1 | MIT |
| lucide-react | ^0.468.0 | ISC |
| recharts | ^2.15.0 | MIT |
| typescript | ^5.7.2 | Apache-2.0 (dev) |
| tailwindcss | ^3.4.16 | MIT (dev) |
| postcss | ^8.4.49 | MIT (dev) |
| autoprefixer | ^10.4.20 | MIT (dev) |
| @types/react, @types/react-dom | ^19.x | MIT (dev) |

All permissive (MIT/ISC/Apache-2.0). No copyleft. Transitive tree not yet scanned.

## 4. .NET — `apps/revit-addin` (`*.csproj` PackageReferences)

| Package | Version | License | Notes |
| --- | --- | --- | --- |
| System.Text.Json | 10.0.5 | MIT | |
| Microsoft.Web.WebView2 | 1.0.3351.48 | **Microsoft proprietary** (distributable) | Microsoft Software License Terms; redistribution allowed under those terms, not OSS |
| Microsoft.NET.Test.Sdk | 17.11.0 | MIT (test only) | |
| xunit | 2.9.0 | Apache-2.0 (test only) | |
| xunit.runner.visualstudio | 2.9.0 | Apache-2.0 (test only) | |

**Not via NuGet — proprietary references:**
- **Autodesk Revit API** (`RevitAPI.dll`, `RevitAPIUI.dll`) — Autodesk proprietary. Referenced
  for compilation, **must not be redistributed**; the add-in loads against the user's installed
  Revit. Confirm these DLLs are *not* tracked in Git (Phase 0 confirmed no DLL payloads tracked).

---

## 5. Container / base images

| Image | Where | License |
| --- | --- | --- |
| postgres:16 | `docker-compose.yml`, CI | PostgreSQL License (permissive, BSD-like) |

---

## 6. Open follow-ups

- [ ] Run automated scanners and capture **transitive** licenses (the tables above are direct deps only).
- [ ] Get counsel sign-off on `psycopg` LGPL-3.0 for the intended distribution model.
- [ ] Confirm WebView2 redistribution terms for the installer flow.
- [ ] Add a license-scan stage to CI (register §21.1) so this file is regenerated, not hand-maintained.
