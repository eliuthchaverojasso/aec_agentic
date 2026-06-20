import { useState } from "react";
import { api } from "../api/client";
import type { DevSmokeTest, DevStatus, ProjectSummary } from "../types";

export function DevModePage({ project }: { project?: ProjectSummary }) {
  const [status, setStatus] = useState<DevStatus | null>(null);
  const [smoke, setSmoke] = useState<DevSmokeTest | null>(null);
  const [response, setResponse] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  const [projectFolder, setProjectFolder] = useState("ROCHELL ES");
  const [manifestPath, setManifestPath] = useState("ROCHELL ES/landing_manifest.json");
  const [includePdfMetadata, setIncludePdfMetadata] = useState(true);
  const [preserveExisting, setPreserveExisting] = useState(true);
  const [recalcReadiness, setRecalcReadiness] = useState(true);
  const [dryRun, setDryRun] = useState(true);

  const run = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      const data = await fn();
      setResponse({ label, at: new Date().toISOString(), data });
      return data;
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="ema-page ema-page-shell space-y-5">
      <div className="ema-card p-5">
        <h2 className="text-lg font-semibold text-ink">Dev Mode Operations Console</h2>
        <p className="mt-1 text-sm text-muted">
          Local backend operations. Real ingest writes to local PostgreSQL. Official readiness remains deterministic.
        </p>
        <div className="mt-4 flex flex-wrap gap-2">
          <button className="ema-btn-secondary" disabled={busy} onClick={() => run("dev-status", api.getDevStatus).then((x) => setStatus(x as DevStatus))}>Load Dev Status</button>
          <button className="ema-btn-secondary" disabled={busy} onClick={() => run("smoke-test", api.runDevSmokeTest).then((x) => setSmoke(x as DevSmokeTest))}>Run Smoke Test</button>
          <button className="ema-btn-secondary" disabled={busy} onClick={() => run("health", api.health)}>Health</button>
          <button className="ema-btn-secondary" disabled={busy || !project} onClick={() => project && run("readiness", () => api.getProjectReadiness(project.id))}>Load Readiness</button>
          <button className="ema-btn-secondary" disabled={busy || !project} onClick={() => project && run("documents", () => api.listProjectDocuments(project.id))}>Load Documents</button>
          <button className="ema-btn-secondary" disabled={busy || !project} onClick={() => project && run("issues", () => api.listIssues(new URLSearchParams({ project_id: String(project.id), page_size: "50" })))}>Load Issues</button>
        </div>
      </div>

      {/* Landing Batch Endpoints */}
      <div className="ema-card p-5">
        <h3 className="font-semibold text-ink">Landing Batch Endpoints</h3>
        <p className="mt-1 text-xs text-muted">Test bulk landing operations directly against the API.</p>
        <div className="mt-3 flex flex-wrap gap-2">
          <button className="ema-btn-secondary" disabled={busy} onClick={() => run("get-landing-projects", api.getLandingProjects)}>GET /landing/projects</button>
          <button className="ema-btn-secondary" disabled={busy} onClick={() => run("rebuild-all-manifests", () => api.rebuildAllLandingManifests({ dry_run: true }))}>POST /landing/rebuild-all-manifests (dry-run)</button>
          <button
            className="ema-btn-secondary border-warning text-warning"
            disabled={busy}
            onClick={() =>
              window.confirm("Run real rebuild-all-manifests? This is not dry-run.") &&
              run("rebuild-all-manifests-real", () => api.rebuildAllLandingManifests({ dry_run: false }))
            }
          >
            POST /landing/rebuild-all-manifests (real)
          </button>
          <button className="ema-btn-secondary" disabled={busy} onClick={() => run("ingest-all-dry-run", () => api.ingestAllLandingProjects({ dry_run: true }))}>POST /landing/ingest-all (dry-run)</button>
          <button
            className="ema-btn-secondary border-danger text-danger"
            disabled={busy}
            onClick={() =>
              window.confirm("Run real ingest-all? This writes to local PostgreSQL.") &&
              run("ingest-all-real", () => api.ingestAllLandingProjects({ dry_run: false }))
            }
          >
            POST /landing/ingest-all (real)
          </button>
          <button
            className="ema-btn-secondary"
            disabled={busy || !projectFolder}
            onClick={() => run("bind-landing", () => api.bindLandingProject(projectFolder, { create_project: true }))}
          >
            POST /landing/projects/{projectFolder}/bind
          </button>
        </div>
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <div className="ema-card p-5">
          <h3 className="font-semibold text-ink">Landing Operations (Single-Project)</h3>
          <div className="mt-3 grid gap-3">
            <input className="ema-input h-10 px-3" value={projectFolder} onChange={(e) => setProjectFolder(e.target.value)} placeholder="Project folder" />
            <input className="ema-input h-10 px-3" value={manifestPath} onChange={(e) => setManifestPath(e.target.value)} placeholder="Manifest path" />
            <label className="flex items-center gap-2 text-sm text-muted">
              <input type="checkbox" className="ema-checkbox h-4 w-4" checked={includePdfMetadata} onChange={(e) => setIncludePdfMetadata(e.target.checked)} />
              Include PDF metadata
            </label>
            <label className="flex items-center gap-2 text-sm text-muted">
              <input type="checkbox" className="ema-checkbox h-4 w-4" checked={preserveExisting} onChange={(e) => setPreserveExisting(e.target.checked)} />
              Preserve existing manifest entries
            </label>
            <label className="flex items-center gap-2 text-sm text-muted">
              <input type="checkbox" className="ema-checkbox h-4 w-4" checked={dryRun} onChange={(e) => setDryRun(e.target.checked)} />
              Dry run
            </label>
            <label className="flex items-center gap-2 text-sm text-muted">
              <input type="checkbox" className="ema-checkbox h-4 w-4" checked={recalcReadiness} onChange={(e) => setRecalcReadiness(e.target.checked)} />
              Recalculate readiness after ingest
            </label>
            <div className="flex flex-wrap gap-2">
              <button className="ema-btn-secondary" disabled={busy} onClick={() => run("scan-landing", () => api.landingScan({ project_folder: projectFolder, dry_run: dryRun, include_pdf_metadata: includePdfMetadata }))}>Scan Landing</button>
              <button className="ema-btn-secondary" disabled={busy} onClick={() => run("rebuild-manifest", () => api.landingRebuildManifest({ project_folder: projectFolder, dry_run: dryRun, preserve_existing: preserveExisting, include_pdf_metadata: includePdfMetadata }))}>Rebuild Manifest</button>
              <button className="ema-btn-secondary" disabled={busy} onClick={() => run("dry-run-ingest", () => api.landingIngest({ manifest_path: manifestPath, dry_run: true, recalculate_readiness: recalcReadiness }))}>Dry Run Ingest</button>
              <button
                className="ema-btn-secondary border-danger text-danger"
                disabled={busy}
                onClick={() => window.confirm("Run real ingest to local PostgreSQL?") && run("real-ingest", () => api.landingIngest({ manifest_path: manifestPath, dry_run: false, recalculate_readiness: recalcReadiness }))}
              >
                Run Ingest
              </button>
            </div>
          </div>
        </div>

        <div className="ema-card p-5">
          <h3 className="font-semibold text-ink">Status</h3>
          {status ? (
            <pre className="ema-solid-json-surface mt-3 overflow-auto rounded-md p-3 text-xs">{JSON.stringify(status, null, 2)}</pre>
          ) : (
            <p className="mt-3 text-sm text-muted">No status loaded yet.</p>
          )}
          {smoke ? (
            <pre className="ema-solid-json-surface mt-3 overflow-auto rounded-md p-3 text-xs">{JSON.stringify(smoke, null, 2)}</pre>
          ) : null}
        </div>
      </div>

      <div className="ema-card p-5">
        <h3 className="font-semibold text-ink">Response Console</h3>
        {response ? (
          <pre className="ema-solid-json-surface mt-3 overflow-auto rounded-md p-3 text-xs">{JSON.stringify(response, null, 2)}</pre>
        ) : (
          <p className="mt-3 text-sm text-muted">Run any action to inspect request/response payloads.</p>
        )}
        <p className="mt-3 text-xs text-muted">
          Safety: Dev Mode operates local backend endpoints only. No secrets are shown. Real ingest writes to local PostgreSQL. Official readiness remains deterministic.
        </p>
      </div>
    </section>
  );
}
