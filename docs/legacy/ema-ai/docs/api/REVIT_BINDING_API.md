# Revit Binding API

## Purpose
Define the JSON payload used by Project Setup for manual Revit add-in configuration.

## Current Payload
```json
{
  "environment": "Local",
  "api_base_url": "http://localhost:8010",
  "dashboard_url": "http://localhost:5173",
  "landing_root": "<path>",
  "project_folder_name": "<folder>",
  "project_display_name": "<name>",
  "project_code": "<code>",
  "client_code": "<code>",
  "client_name": "<name>",
  "project_id": 0,
  "client_id": 0,
  "model_id": 0,
  "current_milestone": "DD75",
  "use_landing_structure": true
}
```

## Status
Implemented as generated dashboard output and copy/download action.
