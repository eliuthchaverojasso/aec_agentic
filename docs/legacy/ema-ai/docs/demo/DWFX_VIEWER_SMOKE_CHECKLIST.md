# DWFx / Viewer Smoke Checklist (Local Demo)

## Preconditions
- DWFx package exists under landing for selected project
- Project has been scanned/indexed in Processing / Sync

## Checks
- [ ] Viewer package appears in Model / Viewer selector
- [ ] Source format displays `DWFX`
- [ ] Evidence status displays `candidate` or mapped value
- [ ] Viewer mode shows `Registered Package` or clear fallback mode
- [ ] UI states include `Not Official Evidence`
- [ ] APS state clearly shows `Not Configured` unless backend APS is actually active
- [ ] Open Processing / Sync action works
- [ ] Open Documents / Evidence action works
- [ ] Open Debug / Logs action works

## Truth Boundaries
- Browser-native DWFx rendering is not claimed by default.
- DWFx indexing does not equal official evidence or compliance.
