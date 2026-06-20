# Debug Bundle

`POST /api/v1/debug/bundle` returns a redacted diagnostics package with:
- environment snapshot
- operation log summary
- latest operation logs

Bundle output excludes:
- secrets
- `.env` values
- raw project document contents
- raw Revit export bodies
