# EMA AI Papers

Source-only LaTeX papers for technical, executive, and architecture publication workflows.

Build (if LaTeX toolchain is installed):
```bash
pdflatex main.tex
bibtex main
pdflatex main.tex
pdflatex main.tex
```
or:
```bash
latexmk -pdf main.tex
```
