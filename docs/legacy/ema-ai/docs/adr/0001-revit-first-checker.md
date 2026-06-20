# ADR 0001 - Revit-First Owner Requirement Checker

## Status

Accepted

## Context

EMA AI needs a designer-first workflow that starts in Revit instead of requiring dashboard/project setup first.

## Decision

Make the primary workflow:

`Revit -> Owner Requirements workbook -> discipline selection -> local HTML report`

## Consequences

- The local checker must work without the dashboard.
- The designer workflow stays deterministic and local.
- Backend sync can be added later as an optional adapter.
