# SEION Core Spec

## Package

`Pipeline/pipeline/app/seion_core/`

## Implemented Modules

- `products.py`: `TernaryProduct`, batch product, projection, associator, cyclic defect.
- `projectors.py`: QR orthonormalization, projector construction, random projector, projector metrics.
- `audits.py`: structured JSON-compatible audit blocks.
- `reports.py`: audit report read/write helpers.
- `tensors.py`: dense kernel validation helpers.

## Audits

Implemented:

- `A_projector`
- `B_commutator`
- `G_nary_closure`
- `H_nary_associator`
- `N_cyclic_law`

Future audits return `WARN` with `not implemented in SEION v0.1` notes when included in the master report.

## Safety

Audit output is numerical diagnostic data only. It is not official EMA readiness evidence.
