# SEION-KGE Advisory Integration Spec

## Status

SEION-KGE v0.1 foundation implemented. A deterministic NumPy training/scoring runtime is available in `app/seion_core`; predictions remain advisory.

## Purpose

SEION-KGE may suggest likely relationships:

- evidence links
- requirement relationships
- similar issues
- missing requirement/evidence relationships
- ranked candidate relationships

## Prohibited

SEION-KGE must not approve readiness, decide official compliance, close issues, write to `requirement_compliance` automatically, replace readiness scoring, or mutate official PostgreSQL records without reviewer/deterministic workflow acceptance.

## Data Flow

PostgreSQL source of truth -> `entities.jsonl` and `triples.jsonl` export -> offline/service SEION scoring -> `seion_prediction` suggestion store -> reviewer accepts/rejects -> accepted suggestion may create official records through a future deterministic workflow -> readiness recalculates deterministically.

## Export Files

Default output: `Pipeline/pipeline/artifacts/seion/`

- `entities.jsonl`
- `triples.jsonl`

## Training Command

From the repository root:

```powershell
$env:PYTHONPATH = (Resolve-Path .\Pipeline\pipeline).Path
py -3.12 -m app.seion_core.kge_train --entities .\Pipeline\pipeline\artifacts\seion\entities.jsonl --triples .\Pipeline\pipeline\artifacts\seion\triples.jsonl --out .\Pipeline\pipeline\artifacts\seion\model --dim 64 --epochs 5 --neg-k 16
```

Artifacts:

- `model.npz`
- `entity_to_id.json`
- `relation_to_id.json`
- `config.json`
- `metrics.json`

## Prediction Store

`seion_prediction` stores advisory rows with `project_id`, `head_uid`, `relation`, `tail_uid`, `score`, `rank`, `model_version`, `status`, `source`, reviewer fields, metadata, and timestamps.

Statuses: `suggested`, `accepted`, `rejected`, `stale`, `superseded`.

## API

- `POST /api/v1/seion/export-graph`
- `POST /api/v1/seion/import-predictions`
- `GET /api/v1/projects/{project_id}/seion/suggestions`
- `POST /api/v1/seion/suggestions/{prediction_id}/accept`
- `POST /api/v1/seion/suggestions/{prediction_id}/reject`

All responses are advisory.
