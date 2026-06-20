# Project Setup Manual

## Goal
Create or bootstrap a project and generate a Revit binding JSON for local workflow execution.

## Steps
1. Choose existing client or enter new client name/code.
2. Enter project details (name/code/type/milestone/disciplines).
3. Create model binding (name/type/discipline/source).
4. Configure landing root + project folder and create standard folders.
5. Optionally discover/bootstrap from existing landing folder.
6. Register project files as evidence candidates.
7. Save and copy generated `project_binding.json`.

## Output
- `project_id`
- `client_id`
- `model_id`
- landing binding status
- Revit binding JSON payload for add-in settings

## Notes
- Bootstrap does not auto-ingest by default.
- Continue in Processing / Sync for scan/ingest steps.
