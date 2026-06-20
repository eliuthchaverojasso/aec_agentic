# Processing / Sync Animations

## Scope
Processing / Sync uses persistent local UI state and read-only heartbeat signals to keep operator context visible between refreshes.

## Persistence
- Local key: `ema-ai-processing-pipeline-state`
- Stores:
  - project id
  - last pipeline step statuses
  - session history
  - heartbeat timestamp
  - summary counters

## Heartbeat
- Interval: 30 seconds
- Read-only status checks only:
  - landing status
  - debug pipeline state
  - backend health
- Heartbeat never triggers ingest, rebuild, or snapshot writes.

## Safety Language
- UI explicitly marks workflow as **Operator Controlled**.
- Read-only heartbeat does not imply automatic ingestion.

## Visual Treatment
- Heartbeat indicator uses `ema-anim-heartbeat` and remains read-only.
- Copy now states “Operator controlled” and “No automatic ingest.”
- Reduced motion disables heartbeat pulse and data-flow animation.
