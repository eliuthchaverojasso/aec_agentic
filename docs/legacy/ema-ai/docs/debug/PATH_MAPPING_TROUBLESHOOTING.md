# Path Mapping Troubleshooting

## Symptom
Landing discover/bootstrap fails when dashboard submits a Windows path but backend runtime uses `/app/landing`.

## Why
Backend path resolution occurs inside runtime context (often container filesystem), not host shell context. The backend config sets `landing_dir: Path = Path("/app/landing")` which is correct for Docker. The Docker volume `./landing:/app/landing` maps the host `Pipeline/pipeline/landing` into the container.

## Current Mapping
| Layer | Path |
|-------|------|
| Backend config | `/app/landing` |
| Docker volume | `./landing:/app/landing` (relative to docker-compose.yml) |
| Host landing | `Pipeline/pipeline/landing/` |
| Frontend/operator | Windows path to `Pipeline/pipeline/landing/` |

## Fix
1. Confirm backend configured landing root via `GET /api/v1/debug/environment`.
2. Confirm Docker volume maps host landing root into `/app/landing`.
3. Use backend-reachable path conventions in setup/testing.
4. The Processing page Environment section shows `file_path_mode` (container_path vs windows_path) and `container_hint` to diagnose mismatches.

## Diagnostics
- `GET /api/v1/debug/environment` — shows landing_dir, path mode, container hint, and warnings
- `GET /api/v1/debug/pipeline-state` — shows latest scan/ingest operations
- Debug / Logs warnings panel — shows path mismatch warnings
- Processing page section B — shows live path mapping status with mismatch warnings

## Expected Warning
When the backend runs in Docker (`file_path_mode: container_path`) but the debug environment reports `container_hint: false`, the Processing page shows a yellow warning banner: "Backend uses container-style landing_dir — Windows host paths may be unreachable unless Docker volume is mapped correctly."
