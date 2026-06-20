# Revit Add-in

This folder contains the migrated EMAExtractor add-in and tests. It is the first local edge adapter for the control plane.

The add-in should remain responsible for:

- Local extraction
- Local deterministic rule evaluation
- Evidence artifact creation
- Durable sync queue
- Dashboard context links

It should not own contract interpretation, earned value authority, milestone approval, or permanent backend credentials.

