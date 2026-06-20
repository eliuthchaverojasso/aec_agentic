# EMA AI Pilot Quick Start Guide

This folder contains the LaTeX source for the client-facing pilot quick start guide.

## Build

Run from `docs/quick_start/`:

```powershell
latexmk -pdf -interaction=nonstopmode -halt-on-error main.tex
```

If `latexmk` is unavailable:

```powershell
pdflatex -interaction=nonstopmode -halt-on-error main.tex
pdflatex -interaction=nonstopmode -halt-on-error main.tex
```

## Output

The intended PDF output is:

`docs/quick_start/EMA_AI_Pilot_Quick_Start_Guide.pdf`

## Notes

- The guide is written for the Revit-first controlled pilot.
- Docker, PostgreSQL, and local AI are described as optional or full-platform features, not required for the pilot path.
- Replace any remaining placeholders before client delivery if support/contact details change.
