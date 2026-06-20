# SEION Audit Spec

## Audit Shape

Each audit returns:

```json
{
  "block": "A_projector",
  "status": "PASS",
  "metrics": {},
  "thresholds": {},
  "warnings": [],
  "notes": []
}
```

Statuses used in v0.1 are `PASS`, `WARN`, and `FAIL`; unimplemented future audits are represented as `WARN` with an explicit `not implemented` note.

## Master Report

`run_basic_audit(product, P, samples, Delta=None)` returns:

```json
{
  "seion_version": "0.1.0",
  "object": "TernaryProduct(...)",
  "audits": [],
  "summary": {
    "pass": 0,
    "warn": 0,
    "fail": 0
  }
}
```

Reports can be persisted with `write_audit_report` and loaded with `load_audit_report`.
