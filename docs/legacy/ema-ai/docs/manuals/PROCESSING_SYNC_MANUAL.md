# Processing / Sync Manual

## Operation Order
1. Health Check
2. Landing Status (verify folder structure and file counts)
3. Discover Landing Projects (optional)
4. Bootstrap From Folder (optional)
5. Scan Landing
6. Rebuild Manifest
7. Dry Run Ingest
8. Run Ingest
9. Create Readiness Snapshot

## Processing Page Sections (A-G)

The Processing page is organized into the following sections:

### A. Selected Project Context
Shows project name, ID, client, and selection source. The localStorage key `ema-ai-selected-project-id` persists the active project across sessions.

### B. Environment & Path Mapping
Displays backend `landing_dir`, path mode (container_path vs windows_path), container detection, and project folder. A warning banner appears when the backend uses a container path but is running outside Docker, or vice versa.

### C. Landing Status
Shows the landing path, manifest status, standard folder checklist, and file counts by type. Refreshed after Landing Status and Scan operations.

### D. Latest Export
Displays the most recent Revit export filename, status, modification timestamp, and element count.

### E. Operational Controls (Grouped)
Operations are grouped into three safety levels:
- **Safe / Read-only**: Health Check, Landing Status, Scan Landing — no data modified
- **Preparation**: Rebuild Manifest (writes), Dry Run Ingest (read-only) — manifest/dry-run
- **Write**: Run Ingest, Create Snapshot — require confirmation when write guard is active

### F. Operation Result
Every operation displays its name, duration (ms), and a collapsible JSON response viewer showing the full API response.

### G. Inline Operation History
Two tables: **Sync Step Details** from persistent backend logs (top 10 most recent), and **Session History** from the current browser session.

## Write Guard
A toggle at the top of the page enables/disables the write guard. When active, write operations (Rebuild Manifest, Run Ingest, Create Snapshot) require confirmation via modal dialog.

## Response Interpretation
Each response should include operation metadata (`operation`, `project_id`, `counts`, `warnings`, `errors`).

Read these fields first:
- `ok` and `status`
- `endpoint` and `operation`
- `request_id` / `run_id` (if provided)
- `warnings` / `errors`
- `next_actions`

## Failure Handling
- Route mismatch: verify project-scoped endpoints are used.
- Landing unreachable: validate backend landing root and container mapping.
- Ingest errors: use Debug / Logs with `request_id` and `run_id`.

## Debug Linkage
After failures, open **Debug / Logs** and filter by:
- Project ID
- Operation type
- Status `failed`
