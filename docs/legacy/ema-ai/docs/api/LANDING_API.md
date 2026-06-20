# Landing API

## Implemented

### `POST /api/v1/projects/{project_id}/landing/configure`
- Configure project landing root/folder and create standard folders.

### `GET /api/v1/projects/{project_id}/landing/status`
- Return landing path and folder counts/warnings.

### `POST /api/v1/landing/projects/discover`
- Discover candidate project folders under landing root.

### `POST /api/v1/landing/projects/bootstrap-from-folder`
- Create/update client/project bindings from existing folder.

## Safety
- Reject path traversal.
- Project folder names should be relative folder segments, not arbitrary absolute targets.
