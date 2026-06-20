import { FolderOpen, Layers3, RefreshCw, ShieldCheck, FileCog } from "lucide-react";
import type { ReactNode } from "react";
import { StatusBadge } from "./StatusBadge";
import type {
  LandingProjectDiscoveryResponse,
  LandingProjectSummary,
  LandingProjectStatus,
} from "../types";

type Props = {
  discovery: LandingProjectDiscoveryResponse | null;
  loading?: boolean;
  error?: string | null;
  onRefresh?: () => void;
  onRebuildDryRun?: () => void;
  onRebuildAll?: () => void;
  onIngestDryRun?: () => void;
  onIngestAll?: () => void;
  onSelectProject?: (project: LandingProjectSummary) => void;
  onBindProject?: (project: LandingProjectSummary) => void;
  onOpenProjectSetup?: (project: LandingProjectSummary) => void;
  onOpenDocuments?: (project: LandingProjectSummary) => void;
  onOpenRequirements?: (project: LandingProjectSummary) => void;
  onOpenDebugLogs?: (project: LandingProjectSummary) => void;
  footerNote?: ReactNode;
};

const statusLabels: Record<LandingProjectStatus, string> = {
  ready: "Ready",
  needs_manifest: "Needs manifest",
  needs_client_binding: "Needs client binding",
  partial: "Partial",
  has_errors: "Has errors",
  empty: "Empty",
};

export function LandingRootOverview({
  discovery,
  loading = false,
  error,
  onRefresh,
  onRebuildDryRun,
  onRebuildAll,
  onIngestDryRun,
  onIngestAll,
  onSelectProject,
  onBindProject,
  onOpenProjectSetup,
  onOpenDocuments,
  onOpenRequirements,
  onOpenDebugLogs,
  footerNote,
}: Props) {
  const projects = discovery?.projects ?? [];
  const totals = discovery?.totals;
  const readyCount = projects.filter((project) => project.status === "ready").length;
  const bindingCount = projects.filter((project) => project.status === "needs_client_binding").length;
  const manifestCount = projects.filter((project) => project.status === "needs_manifest").length;

  return (
    <section className="ema-liquid-section space-y-4 p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-lg font-semibold text-ink">Landing Root Overview</h3>
          <p className="mt-1 text-sm text-muted">
            Scan the landing root, rebuild manifests, and run dry-run ingest before any write operation.
          </p>
          <p className="mt-2 text-xs text-subtle">
            Local Demo · Writes to local PostgreSQL · Evidence Candidate · Operator Controlled · Not Official Compliance
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {onRefresh && (
            <button type="button" className="ema-btn-secondary h-9" onClick={onRefresh}>
              <RefreshCw size={14} className="mr-2" />
              Refresh
            </button>
          )}
          {onRebuildDryRun && (
            <button type="button" className="ema-btn-secondary h-9" onClick={onRebuildDryRun}>
              <FolderOpen size={14} className="mr-2" />
              Rebuild All Dry Run
            </button>
          )}
          {onRebuildAll && (
            <button type="button" className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-sm font-semibold text-amber-800 hover:bg-amber-100 h-9" onClick={onRebuildAll}>
              <FileCog size={14} className="mr-2" />
              Rebuild All
            </button>
          )}
          {onIngestDryRun && (
            <button type="button" className="ema-btn-secondary h-9" onClick={onIngestDryRun}>
              <Layers3 size={14} className="mr-2" />
              Ingest All Dry Run
            </button>
          )}
          {onIngestAll && (
            <button type="button" className="ema-btn-primary h-9" onClick={onIngestAll}>
              <ShieldCheck size={14} className="mr-2" />
              Run Ingest All
            </button>
          )}
        </div>
      </div>

      {error ? (
        <div className="ema-solid-warning-surface rounded-lg border border-amber-300 p-3 text-sm text-amber-900">
          {error}
        </div>
      ) : null}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <Stat label="Landing Root" value={discovery?.landing_root || "Unavailable"} />
        <Stat label="Project Folders" value={discovery?.project_count ?? 0} />
        <Stat label="Ready" value={readyCount} />
        <Stat label="Needs Client" value={bindingCount} />
        <Stat label="Needs Manifest" value={manifestCount} />
        <Stat label="Manifests" value={totals?.manifests ?? 0} />
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Stat label="Revit Exports" value={totals?.revit_exports ?? 0} />
        <Stat label="Sidecars" value={totals?.revit_meta ?? 0} />
        <Stat label="Drawings" value={totals?.drawings ?? 0} />
        <Stat label="Owner Req." value={totals?.owner_requirements ?? 0} />
        <Stat label="Specifications" value={totals?.specifications ?? 0} />
      </div>

      <div className="overflow-x-auto rounded-lg border border-line bg-surface">
        {loading ? (
          <div className="p-4 text-sm text-muted">Loading landing root inventory...</div>
        ) : projects.length === 0 ? (
          <div className="p-4 text-sm text-muted">No landing project folders discovered.</div>
        ) : (
          <table className="min-w-full divide-y divide-line text-sm">
            <thead className="bg-surface-2 text-left text-xs uppercase tracking-wide text-muted">
              <tr>
                <th className="px-4 py-3">Project Folder</th>
                <th className="px-4 py-3">Project / Client</th>
                <th className="px-4 py-3">Counts</th>
                <th className="px-4 py-3">Manifest</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Next Action</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {projects.map((project) => (
                <tr key={project.project_folder}>
                  <td className="px-4 py-3 font-semibold text-ink">
                    <div>{project.project_folder}</div>
                    <div className="text-xs text-subtle">{project.project_name}</div>
                  </td>
                  <td className="px-4 py-3 text-muted">
                    <div>{project.project_id ? `ID: ${project.project_id}` : "No project linked"}</div>
                    <div>{project.client_name || project.client_suggestion?.client_name || "Client pending"}</div>
                    {project.client_code ? <div className="text-xs text-subtle">{project.client_code}</div> : null}
                    {!project.client_name && project.client_suggestion ? (
                      <div className="mt-1 text-[11px] text-amber-600">
                        Suggestion: {project.client_suggestion.client_name} ({project.client_suggestion.client_code})
                      </div>
                    ) : null}
                  </td>
                  <td className="px-4 py-3 text-muted">
                    <div>Revit: {project.counts.revit_exports}</div>
                    <div>Drawings: {project.counts.drawings}</div>
                    <div>Owner Req: {project.counts.owner_requirements}</div>
                    <div>Specs: {project.counts.specifications}</div>
                  </td>
                  <td className="px-4 py-3 text-muted">{project.manifest_exists ? project.manifest_path || "Yes" : "Missing"}</td>
                  <td className="px-4 py-3"><StatusBadge value={statusLabels[project.status]} /></td>
                  <td className="px-4 py-3 text-muted">{project.next_action || "Review landing state"}</td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap justify-end gap-2">
                      {project.project_id && onSelectProject ? (
                        <button type="button" className="ema-btn-secondary h-8 px-2 text-xs" onClick={() => onSelectProject(project)}>
                          Select
                        </button>
                      ) : null}
                      {onBindProject ? (
                        <button type="button" className="ema-btn-secondary h-8 px-2 text-xs" onClick={() => onBindProject(project)}>
                          Bind
                        </button>
                      ) : null}
                      {project.project_id && onOpenDocuments ? (
                        <button type="button" className="ema-btn-secondary h-8 px-2 text-xs" onClick={() => onOpenDocuments(project)}>
                          Documents
                        </button>
                      ) : null}
                      {project.project_id && onOpenRequirements ? (
                        <button type="button" className="ema-btn-secondary h-8 px-2 text-xs" onClick={() => onOpenRequirements(project)}>
                          Requirements
                        </button>
                      ) : null}
                      {project.project_id && onOpenDebugLogs ? (
                        <button type="button" className="ema-btn-secondary h-8 px-2 text-xs" onClick={() => onOpenDebugLogs(project)}>
                          Logs
                        </button>
                      ) : null}
                      {project.project_id && onOpenProjectSetup ? (
                        <button type="button" className="ema-btn-secondary h-8 px-2 text-xs" onClick={() => onOpenProjectSetup(project)}>
                          Setup
                        </button>
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {footerNote ? <div className="text-xs text-subtle">{footerNote}</div> : null}
    </section>
  );
}

function Stat({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="ema-liquid-metric p-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 text-sm font-semibold text-ink">{value}</div>
    </div>
  );
}
