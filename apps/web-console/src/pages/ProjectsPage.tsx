import {
  AlertTriangle,
  Clock3,
  Download,
  FileWarning,
  FolderOpen,
  Layers3,
  LineChart,
  Search,
} from "lucide-react";
import { useMemo, useState } from "react";
import { KpiCard } from "../components/KpiCard";
import { ProgressBar } from "../components/ProgressBar";
import { StatusBadge } from "../components/StatusBadge";
import {
  atRiskCount,
  averageReadiness,
  disciplineScore,
  ownerRequirementGaps,
  projectDisplayName,
} from "../lib/derived";
import { demoFallback, demoMilestone } from "../lib/demoFallback";
import { downloadCsv } from "../lib/exportData";
import { formatPercent, timeAgo } from "../lib/format";
import type { ProjectReadiness, ProjectSummary, Requirement } from "../types";

type ProjectsPageProps = {
  projects: ProjectSummary[];
  readiness?: ProjectReadiness | null;
  requirements: Requirement[];
  onSelectProject: (projectId: number) => void;
};

export function ProjectsPage({ projects, readiness, requirements, onSelectProject }: ProjectsPageProps) {
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const avgReadiness = averageReadiness(projects, readiness);
  const atRiskProjects = atRiskCount(projects, readiness);
  const reqGaps = ownerRequirementGaps(requirements, readiness);
  const lastSync = readiness?.latest_sync_at || projects.find((project) => project.last_sync_at)?.last_sync_at;
  const visibleProjects = useMemo(
    () =>
      projects.filter((project) => {
        const selected = readiness?.project_id === project.id;
        const projectReadiness = selected ? readiness.overall_readiness : project.model_health_score ?? 100;
        const label = selected ? readiness.label : projectReadiness >= 75 ? "On Track" : "At Risk";
        const matchesSearch = `${project.project_name || ""} ${project.project_title} ${project.client_name || ""}`
          .toLowerCase()
          .includes(search.trim().toLowerCase());
        const matchesStatus = statusFilter === "all" || label.toLowerCase() === statusFilter;
        return matchesSearch && matchesStatus;
      }),
    [projects, readiness, search, statusFilter],
  );

  const exportVisibleProjects = () => {
    downloadCsv(
      "ema-ai-project-portfolio.csv",
      visibleProjects.map((project) => {
        const selected = readiness?.project_id === project.id;
        const projectReadiness = selected ? readiness.overall_readiness : project.model_health_score ?? 100;
        const label = selected ? readiness.label : projectReadiness >= 75 ? "On Track" : "At Risk";
        return {
          project: projectDisplayName(project),
          client: readiness?.client_name || project.client_name || "Unassigned",
          current_stage: demoMilestone.currentStage,
          next_deliverable: demoMilestone.dueDate,
          readiness: Math.round(projectReadiness),
          open_issues: project.open_issues,
          high_issues: project.high_issues,
          drawing_package: demoMilestone.drawingPackage,
          status: label,
          last_sync: project.last_sync_at || "",
        };
      }),
    );
  };

  return (
    <div className="ema-page ema-page-shell space-y-6">
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-7">
        <KpiCard
          label="Active Projects"
          value={String(projects.length)}
          detail="Across pilot scope"
          icon={FolderOpen}
          tone="teal"
        />
        <KpiCard label="Projects Due Soon" value={String(demoFallback.readiness.projectsDueSoon)} detail="Within 2 weeks" icon={Clock3} tone="amber" />
        <KpiCard
          label="At-Risk Projects"
          value={String(atRiskProjects)}
          detail="Below threshold"
          icon={AlertTriangle}
          tone={atRiskProjects > 0 ? "rose" : "slate"}
        />
        <KpiCard
          label="Owner Req. Gaps"
          value={String(reqGaps)}
          detail="Missing or not evaluated"
          icon={FileWarning}
          tone="violet"
        />
        <KpiCard label="Demo Drawing Pkg. Gaps" value={String(demoFallback.readiness.drawingPackageGaps)} detail="Demo placeholder" icon={Layers3} tone="amber" />
        <KpiCard
          label="Avg Readiness"
          value={formatPercent(avgReadiness)}
          detail="Selected readiness + model health fallback"
          icon={LineChart}
          tone="teal"
        />
        <KpiCard
          label="Last Sync"
          value={timeAgo(lastSync)}
          detail="Latest backend sync"
          icon={Clock3}
          tone="blue"
        />
      </div>

      <section className="ema-card" data-no-glass>
        <div className="ema-card-header">
          <div>
            <h2 className="text-lg font-semibold text-ink">Project Deliverable Status</h2>
            <p className="text-sm text-muted">Backend readiness where available, with demo placeholders clearly marked</p>
          </div>
          <div className="relative flex gap-2">
            <button
              className="ema-btn-secondary inline-flex h-10 items-center gap-2 px-4"
              type="button"
              onClick={() => setFiltersOpen((open) => !open)}
            >
              <FileWarning size={16} aria-hidden />
              Filter
            </button>
            <button
              className="ema-btn-secondary inline-flex h-10 items-center gap-2 px-4"
              type="button"
              onClick={exportVisibleProjects}
            >
              <Download size={16} aria-hidden />
              Export
            </button>
            {filtersOpen && (
              <div className="ema-glass-panel ema-card absolute right-0 top-12 z-20 w-80 p-4">
                <label className="text-xs font-semibold uppercase tracking-wide text-muted">Search</label>
                <div className="ema-search-shell mt-2 h-10">
                  <Search size={15} className="text-muted" aria-hidden />
                  <input
                    value={search}
                    placeholder="Project or client"
                    onChange={(event) => setSearch(event.target.value)}
                  />
                </div>
                <label className="mt-4 block text-xs font-semibold uppercase tracking-wide text-muted">Status</label>
                <select
                  className="ema-select mt-2 h-10 w-full px-3"
                  value={statusFilter}
                  onChange={(event) => setStatusFilter(event.target.value)}
                >
                  <option value="all">All statuses</option>
                  <option value="behind">Behind</option>
                  <option value="at risk">At Risk</option>
                  <option value="on track">On Track</option>
                </select>
                <button
                  className="ema-btn-secondary mt-4 h-9 w-full"
                  type="button"
                  onClick={() => {
                    setSearch("");
                    setStatusFilter("all");
                  }}
                >
                  Reset filters
                </button>
              </div>
            )}
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="min-w-[1500px] divide-y divide-line text-sm">
            <thead className="bg-surface-2 text-left text-xs font-semibold text-muted">
              <tr>
                <th className="px-5 py-4">Project Name</th>
                <th className="px-5 py-4">Client / Owner</th>
                <th className="px-5 py-4">Demo Stage</th>
                <th className="px-5 py-4">Next Deliverable</th>
                <th className="px-5 py-4">Overall</th>
                <th className="px-5 py-4">Mechanical</th>
                <th className="px-5 py-4">Electrical</th>
                <th className="px-5 py-4">Plumbing</th>
                <th className="px-5 py-4">Technology</th>
                <th className="px-5 py-4">Owner Req. Evaluated</th>
                <th className="px-5 py-4">Demo Drawing Pkg.</th>
                <th className="px-5 py-4">Critical Gaps</th>
                <th className="px-5 py-4">Last Sync</th>
                <th className="px-5 py-4">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {visibleProjects.map((project) => {
                const selected = readiness?.project_id === project.id;
                const projectReadiness = selected ? readiness.overall_readiness : project.model_health_score ?? 100;
                const label = selected ? readiness.label : projectReadiness >= 75 ? "On Track" : "At Risk";
                const totalReq = selected
                  ? readiness.trade_readiness.reduce((sum, row) => sum + row.requirements_total, 0)
                  : requirements.length;
                const evaluatedReq = selected
                  ? readiness.trade_readiness.reduce((sum, row) => sum + row.requirements_evaluated, 0)
                  : 0;

                return (
                  <tr
                    key={project.id}
                    className="cursor-pointer hover:bg-surface-2"
                    onClick={() => onSelectProject(project.id)}
                  >
                    <td className="px-5 py-6">
                      <div className="flex items-center gap-3">
                        <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg border border-line bg-surface-2 text-muted">
                          <Layers3 size={18} aria-hidden />
                        </span>
                        <div>
                          <div className="font-semibold text-ink">{projectDisplayName(project)}</div>
                          <div className="text-xs text-muted">Pilot validation</div>
                        </div>
                      </div>
                    </td>
                    <td className="px-5 py-6 text-muted">{readiness?.client_name || project.client_name || "Unassigned"}</td>
                    <td className="px-5 py-6">
                      <span className="ema-chip ema-chip-accent text-xs font-semibold">
                        {demoMilestone.currentStage}
                      </span>
                    </td>
                    <td className="px-5 py-6">
                      <div className="font-medium text-ink">{demoMilestone.dueDate}</div>
                      <div className="text-xs text-muted">{demoMilestone.daysToDeliverable} days</div>
                    </td>
                    <td className="px-5 py-6">
                      <ProgressBar value={projectReadiness} label={formatPercent(projectReadiness)} tone={projectReadiness < 60 ? "rose" : projectReadiness < 75 ? "amber" : "teal"} />
                    </td>
                    <td className="px-5 py-6">
                      <ProgressBar value={disciplineScore(readiness, "MECHANICAL", 72)} label={formatPercent(disciplineScore(readiness, "MECHANICAL", 72))} tone="teal" />
                    </td>
                    <td className="px-5 py-6">
                      <ProgressBar value={disciplineScore(readiness, "ELECTRICAL", 64)} label={formatPercent(disciplineScore(readiness, "ELECTRICAL", 64))} tone="rose" />
                    </td>
                    <td className="px-5 py-6">
                      <ProgressBar value={disciplineScore(readiness, "PLUMBING", 76)} label={formatPercent(disciplineScore(readiness, "PLUMBING", 76))} tone="amber" />
                    </td>
                    <td className="px-5 py-6">
                      <ProgressBar value={disciplineScore(readiness, "TECHNOLOGY", 58)} label={formatPercent(disciplineScore(readiness, "TECHNOLOGY", 58))} tone="rose" />
                    </td>
                    <td className="px-5 py-6">
                      <div className="font-medium text-ink">
                        {evaluatedReq}/{totalReq || requirements.length}
                      </div>
                      <div className="text-xs text-muted">evaluated</div>
                      <div className="text-xs text-danger">{Math.max(0, (totalReq || requirements.length) - evaluatedReq)} missing</div>
                    </td>
                    <td className="px-5 py-6">
                      <div className="font-medium text-ink">{demoMilestone.drawingPackage}%</div>
                      <div className="text-xs text-muted">Demo placeholder</div>
                    </td>
                    <td className="px-5 py-6">
                      <span className="inline-flex h-8 min-w-8 items-center justify-center rounded-full bg-surface-2 px-2 text-sm font-semibold text-danger">
                        {project.critical_issues + project.high_issues}
                      </span>
                    </td>
                    <td className="px-5 py-6 text-muted">{timeAgo(project.last_sync_at)}</td>
                    <td className="px-5 py-6">
                      <StatusBadge value={label} />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <div className="flex items-center justify-between border-t border-line px-5 py-4 text-sm text-muted">
          <span>Showing {visibleProjects.length} of {projects.length} projects</span>
          <span className="ema-btn-ghost px-3 py-1">1</span>
        </div>
      </section>
    </div>
  );
}
