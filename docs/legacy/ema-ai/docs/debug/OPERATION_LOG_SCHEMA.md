# Operation Log Schema

`pipeline_operation_log` stores local operational events.

Key fields:
- `run_id`, `request_id`
- `project_id`, `operation_type`, `endpoint`, `method`
- `status`, `severity`, `started_at`, `finished_at`, `duration_ms`
- `counts_json`, `request_summary_json`, `response_summary_json`
- `warnings_json`, `errors_json`, `environment_json`, `metadata_json`

Safety:
- secrets are redacted
- raw document contents are excluded
