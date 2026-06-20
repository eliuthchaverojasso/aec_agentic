import { AlertTriangle, Clock3, Filter, FolderKanban, Settings, ShieldCheck, XCircle } from "lucide-react";
import { useMemo, useState } from "react";
import { projectDisplayName } from "../lib/derived";
import { formatDateTime } from "../lib/format";
import type {
  ExecutiveProject,
  ExecutiveProjectStatus,
  LandingDocument,
  LandingProjectDiscoveryResponse,
  ProjectReadiness,
  ProjectSummary,
} from "../types";

type Props = {
  projects: ProjectSummary[];
  readiness?: ProjectReadiness | null;
  documents: LandingDocument[];
  landingDiscovery?: LandingProjectDiscoveryResponse | null;
  onSelectProject: (projectId: number) => void;
  onOpenProcessing: () => void;
  onOpenDebug: () => void;
  onOpenViewer: () => void;
  onOpenProjectSetup?: () => void;
};

const FILTER_KEY = "ema-ai-executive-overview-filters";

const statuses: ExecutiveProjectStatus[] = [
  "historical",
  "in_execution",
  "on_track",
  "behind",
  "blocked",
  "demo",
];

const STATUS_COLORS: Record<ExecutiveProjectStatus, string> = {
  historical: "ema-pill",
  in_execution: "ema-pill ema-pill-success",
  on_track: "ema-pill ema-pill-success",
  behind: "ema-pill ema-pill-warning",
  blocked: "ema-pill ema-pill-danger",
  demo: "ema-pill",
};

export function ExecutiveOverviewPage({
  projects,
  readiness,
  documents,
  landingDiscovery,
  onSelectProject,
  onOpenProcessing,
  onOpenProjectSetup,
}: Props) {
  const persisted = loadFilters();
  const [statusFilter, setStatusFilter] = useState<ExecutiveProjectStatus | "all">(persisted.status);
  const [dateRange, setDateRange] = useState<"7d" | "30d" | "90d" | "ytd" | "all">(persisted.dateRange);

  const executiveProjects = useMemo(
    () => buildExecutiveProjects(projects, readiness, documents),
    [projects, readiness, documents],
  );

  const visibleProjects = useMemo(
    () =>
      executiveProjects.filter(
        (project) =>
          (statusFilter === "all" ? true : project.status === statusFilter) &&
          isInDateRange(project.lastSync, dateRange),
      ),
    [executiveProjects, statusFilter, dateRange],
  );

  const kpis = useMemo(() => {
    const total = visibleProjects.length;
    const behind = visibleProjects.filter((project) => project.status === "behind").length;
    const blocked = visibleProjects.filter((project) => project.status === "blocked").length;
    const onTrack = visibleProjects.filter((project) => project.status === "on_track").length;
    const avg = total
      ? Math.round(visibleProjects.reduce((acc, project) => acc + (project.readinessScore || 0), 0) / total)
      : 0;

    return { total, behind, blocked, onTrack, avg };
  }, [visibleProjects]);

  const landingProjects = landingDiscovery?.projects ?? [];
  const landingTotals = landingDiscovery?.totals;

  const boundCount = landingProjects.filter(
    (project) => project.project_id != null && project.client_id != null,
  ).length;

  const needsBindingCount = landingProjects.filter(
    (project) => project.status === "needs_client_binding",
  ).length;

  const needsManifestCount = landingProjects.filter(
    (project) => project.status === "needs_manifest",
  ).length;

  const hasRevitCount = landingProjects.filter(
    (project) => project.counts.revit_exports > 0,
  ).length;

  const hasDrawingsCount = landingProjects.filter(
    (project) => project.counts.drawings > 0,
  ).length;

  const hasRequirementsCount = landingProjects.filter(
    (project) => project.counts.owner_requirements > 0,
  ).length;

  const dynamicActions = useMemo(() => {
    const actions: string[] = [];

    for (const project of landingProjects) {
      if (project.status === "needs_client_binding") {
        const hint = project.client_suggestion
          ? `Bind ${project.project_folder} to ${project.client_suggestion.client_name} (${project.client_suggestion.client_code})`
          : `Bind ${project.project_folder} to a client`;

        actions.push(hint);
      }

      if (project.status === "needs_manifest") {
        actions.push(`Rebuild manifest for ${project.project_folder}`);
      }

      if (project.status === "ready" && project.project_id) {
        actions.push(`Run dry-run ingest for ${project.project_folder}`);
      }

      if (project.status === "empty") {
        actions.push(`Add files to ${project.project_folder}`);
      }

      if (project.counts.revit_exports === 0 && project.project_id) {
        actions.push(`Add Revit export for ${project.project_folder}`);
      }
    }

    if (landingProjects.length === 0 && landingDiscovery) {
      actions.push("No landing projects discovered. Verify landing root path.");
    }

    return actions.slice(0, 8);
  }, [landingProjects, landingDiscovery]);

  const save = (
    nextStatus: ExecutiveProjectStatus | "all",
    nextDate: "7d" | "30d" | "90d" | "ytd" | "all",
  ) => {
    try {
      window.localStorage.setItem(
        FILTER_KEY,
        JSON.stringify({
          status: nextStatus,
          dateRange: nextDate,
        }),
      );
    } catch {
      // Best-effort local demo persistence.
    }
  };

  return (
    <div className="ema-page ema-page-shell space-y-5">
      <section className="ema-liquid-section p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-2xl font-semibold text-ink">Projects</h2>
            <p className="mt-1 text-sm text-muted">
              Portfolio project list with readiness, risk, owner requirement coverage, and latest sync status.
            </p>
          </div>

          <div className="flex flex-wrap gap-2 text-xs">
            <span className="ema-pill">Local Demo</span>
            <span className="ema-pill">Operator Controlled</span>
            <span className="ema-pill">Not Production</span>
            <span className="ema-pill">Not Official Compliance</span>
          </div>
        </div>

        <div className="ema-liquid-filterbar mt-4 flex flex-wrap gap-2 p-2">
          <Filter size={14} className="mt-2 text-muted" />

          <select
            className="ema-input h-9 px-2 text-sm"
            value={statusFilter}
            onChange={(event) => {
              const next = event.target.value as ExecutiveProjectStatus | "all";
              setStatusFilter(next);
              save(next, dateRange);
            }}
          >
            <option value="all">All</option>
            {statuses.map((status) => (
              <option key={status} value={status}>
                {labelForStatus(status)}
              </option>
            ))}
          </select>

          <select
            className="ema-input h-9 px-2 text-sm"
            value={dateRange}
            onChange={(event) => {
              const next = event.target.value as "7d" | "30d" | "90d" | "ytd" | "all";
              setDateRange(next);
              save(statusFilter, next);
            }}
          >
            <option value="7d">Last 7 days</option>
            <option value="30d">Last 30 days</option>
            <option value="90d">Last 90 days</option>
            <option value="ytd">Year to date</option>
            <option value="all">All history</option>
          </select>

          <button
            type="button"
            className="ema-btn-secondary h-9"
            onClick={() => {
              setStatusFilter("all");
              setDateRange("all");
              save("all", "all");
            }}
          >
            Reset
          </button>
        </div>
      </section>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <Kpi title="Portfolio Readiness" value={`${kpis.avg}%`} icon={ShieldCheck} />
        <Kpi title="Projects" value={String(kpis.total)} icon={FolderKanban} />
        <Kpi title="Behind" value={String(kpis.behind)} icon={AlertTriangle} />
        <Kpi title="Blocked" value={String(kpis.blocked)} icon={XCircle} />
        <Kpi title="On Track" value={String(kpis.onTrack)} icon={ShieldCheck} />
        <Kpi title="Docs Indexed" value={String(documents.length)} icon={Clock3} />
      </section>

      {landingDiscovery && (
        <section className="ema-card p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h3 className="text-base font-semibold text-ink">Landing Root Summary</h3>

            <div className="flex gap-2">
              <button type="button" className="ema-btn-secondary h-8 px-3 text-xs" onClick={onOpenProcessing}>
                Processing / Sync
              </button>

              {onOpenProjectSetup && (
                <button type="button" className="ema-btn-secondary h-8 px-3 text-xs" onClick={onOpenProjectSetup}>
                  <Settings size={12} className="mr-1" />
                  Project Setup
                </button>
              )}
            </div>
          </div>

          <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-8">
            <Stat label="Project Folders" value={landingDiscovery.project_count} />
            <Stat label="Bound" value={boundCount} />
            <Stat label="Needs Binding" value={needsBindingCount} />
            <Stat label="Needs Manifest" value={needsManifestCount} />
            <Stat label="Has Revit" value={hasRevitCount} />
            <Stat label="Has Drawings" value={hasDrawingsCount} />
            <Stat label="Has Reqs" value={hasRequirementsCount} />
            <Stat label="Manifests" value={landingTotals?.manifests ?? 0} />
          </div>
        </section>
      )}

      <section className="grid gap-4 xl:grid-cols-[1.4fr_0.8fr]">
        <div className="ema-solid-table-surface rounded-lg p-4" data-no-glass>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h3 className="font-semibold text-ink">Projects</h3>
              <p className="mt-1 text-sm text-muted">
                Select a project to open the deliverable tracker and review requirements, readiness, and processing status.
              </p>
            </div>
            <span className="ema-pill">{visibleProjects.length} visible</span>
          </div>

          <div className="mt-3 overflow-x-auto">
            <table className="min-w-full divide-y divide-line text-sm">
              <thead className="bg-surface-2 text-left text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-3 py-2">Project</th>
                  <th className="px-3 py-2">Client</th>
                  <th className="px-3 py-2">Status</th>
                  <th className="px-3 py-2">Readiness</th>
                  <th className="px-3 py-2">Open Issues</th>
                  <th className="px-3 py-2">Docs</th>
                  <th className="px-3 py-2">Last Sync</th>
                  <th className="px-3 py-2">Action</th>
                </tr>
              </thead>

              <tbody className="divide-y divide-line">
                {visibleProjects.map((project) => (
                  <tr key={String(project.id)}>
                    <td className="px-3 py-2">
                      <div className="font-semibold text-ink">{project.name}</div>
                      <div className="text-xs text-muted">{project.projectCode || project.currentMilestone}</div>
                    </td>

                    <td className="px-3 py-2 text-muted">{project.clientName || "Demo Client"}</td>

                    <td className="px-3 py-2">
                      <span className={STATUS_COLORS[project.status]}>{labelForStatus(project.status)}</span>
                    </td>

                    <td className="px-3 py-2 text-muted">{project.readinessScore ?? 0}%</td>

                    <td className="px-3 py-2 text-muted">{project.openIssues ?? 0}</td>

                    <td className="px-3 py-2 text-muted">{project.documentsIndexed ?? 0}</td>

                    <td className="px-3 py-2 text-muted">{formatDateTime(project.lastSync)}</td>

                    <td className="px-3 py-2">
                      <button className="ema-btn-secondary" onClick={() => onSelectProject(Number(project.id))}>
                        Open
                      </button>
                    </td>
                  </tr>
                ))}

                {visibleProjects.length === 0 && (
                  <tr>
                    <td colSpan={8} className="px-3 py-8 text-center text-muted">
                      No projects match the current filters.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="ema-liquid-panel p-4">
          <h3 className="font-semibold text-ink">Action Queue</h3>

          {dynamicActions.length === 0 ? (
            <p className="mt-3 text-sm text-muted">No pending actions for landing projects.</p>
          ) : (
            <ul className="mt-3 space-y-2 text-sm text-muted">
              {dynamicActions.map((action, index) => (
                <li key={index} className="flex items-start gap-2">
                  <span className="mt-0.5 h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
                  {action}
                </li>
              ))}
            </ul>
          )}

          <div className="mt-4 border-t border-line pt-3 text-xs text-subtle">
            <p>Actions are derived from landing root state. Complete binding and manifests before ingest.</p>
          </div>
        </div>
      </section>
    </div>
  );
}

function Kpi({
  title,
  value,
  icon: Icon,
}: {
  title: string;
  value: string;
  icon: React.ComponentType<{ size?: number }>;
}) {
  return (
    <div className="ema-liquid-kpi ema-anim-hover-lift p-3">
      <div className="flex items-center justify-between">
        <span className="text-xs uppercase tracking-wide text-muted">{title}</span>
        <Icon size={14} />
      </div>
      <div className="mt-2 text-2xl font-semibold text-ink">{value}</div>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="ema-card p-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 text-lg font-semibold text-ink">{value}</div>
    </div>
  );
}

function labelForStatus(status: ExecutiveProjectStatus) {
  switch (status) {
    case "historical":
      return "Historical";
    case "in_execution":
      return "In Execution";
    case "on_track":
      return "On Track";
    case "behind":
      return "Behind";
    case "blocked":
      return "Blocked";
    case "demo":
      return "Demo";
  }
}

function buildExecutiveProjects(
  projects: ProjectSummary[],
  readiness: ProjectReadiness | null | undefined,
  documents: LandingDocument[],
): ExecutiveProject[] {
  return projects.map((project) => {
    const score = readiness?.project_id === project.id ? readiness.overall_readiness : 0;

    const status: ExecutiveProjectStatus =
      score < 50 ? "blocked" :
      score < 65 ? "behind" :
      score < 80 ? "in_execution" :
      "on_track";

    const docCount = documents.filter(
      (document) =>
        document.project_id === project.id ||
        document.project_folder === (project.project_name || project.project_title),
    ).length;

    return {
      id: project.id,
      name: projectDisplayName(project),
      clientName: project.client_name || undefined,
      projectCode: project.project_code || undefined,
      status,
      currentMilestone: project.phase || "—",
      readinessScore: Math.round(score),
      requirementCoverage: readiness?.requirement_coverage?.score || 0,
      evidenceCoverage: Math.min(100, Math.round((docCount / Math.max(1, documents.length)) * 100)),
      modelHealth: project.model_health_score || undefined,
      openIssues: project.open_issues,
      criticalIssues: project.critical_issues + project.high_issues,
      documentsIndexed: docCount,
      lastSync: project.last_sync_at || undefined,
      location: undefined,
      tags: [],
    };
  });
}

function loadFilters(): {
  status: ExecutiveProjectStatus | "all";
  dateRange: "7d" | "30d" | "90d" | "ytd" | "all";
} {
  try {
    const raw = window.localStorage.getItem(FILTER_KEY);

    if (!raw) {
      return { status: "all", dateRange: "all" };
    }

    const parsed = JSON.parse(raw) as {
      status?: ExecutiveProjectStatus | "all";
      dateRange?: "7d" | "30d" | "90d" | "ytd" | "all";
    };

    const status =
      parsed.status && (["all", ...statuses] as string[]).includes(parsed.status)
        ? parsed.status
        : "all";

    const dateRange =
      parsed.dateRange && (["7d", "30d", "90d", "ytd", "all"] as string[]).includes(parsed.dateRange)
        ? parsed.dateRange
        : "all";

    return { status, dateRange };
  } catch {
    return { status: "all", dateRange: "all" };
  }
}

function isInDateRange(
  value: string | undefined,
  dateRange: "7d" | "30d" | "90d" | "ytd" | "all",
) {
  if (dateRange === "all" || !value) {
    return true;
  }

  const timestamp = new Date(value).getTime();

  if (!Number.isFinite(timestamp)) {
    return true;
  }

  const now = Date.now();

  if (dateRange === "ytd") {
    const yearStart = new Date(new Date().getFullYear(), 0, 1).getTime();
    return timestamp >= yearStart;
  }

  const days = dateRange === "7d" ? 7 : dateRange === "30d" ? 30 : 90;

  return timestamp >= now - days * 24 * 60 * 60 * 1000;
}