import type { LandingDocument, ProjectReadiness, ProjectSummary } from "../types";

export function ReportsPage({
  projects,
  readiness,
  documents,
}: {
  projects: ProjectSummary[];
  readiness: ProjectReadiness | null;
  documents: LandingDocument[];
}) {
  const exportJson = (name: string, payload: unknown) => {
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  return (
    <section className="ema-page ema-page-shell space-y-5">
      <div className="ema-card p-5">
        <h2 className="text-lg font-semibold text-ink">Reports Export</h2>
        <p className="mt-1 text-sm text-muted">Deterministic export of current backend data loaded in this session.</p>
        <div className="mt-4 flex flex-wrap gap-2">
          <button
            className="ema-btn-secondary"
            onClick={() => exportJson("project-readiness-summary.json", readiness)}
          >
            Project Readiness Summary
          </button>
          <button
            className="ema-btn-secondary"
            onClick={() => exportJson("projects-portfolio.json", projects)}
          >
            Portfolio JSON
          </button>
          <button
            className="ema-btn-secondary"
            onClick={() => exportJson("documents-index-summary.json", documents)}
          >
            Documents Index Summary
          </button>
          <button className="ema-btn-secondary opacity-50" disabled title="CSV export coming soon">
            CSV export (coming soon)
          </button>
        </div>
      </div>
    </section>
  );
}
