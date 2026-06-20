import {
  AlertCircle,
  Building2,
  ClipboardCheck,
  Gauge,
  Layers3,
  RefreshCw,
  ShieldCheck,
} from "lucide-react";
import { KpiCard } from "../components/KpiCard";
import { MilestoneRail } from "../components/MilestoneRail";
import { ProgressBar } from "../components/ProgressBar";
import { StatusBadge } from "../components/StatusBadge";
import { projectDisplayName } from "../lib/derived";
import { formatNumber, formatPercent, timeAgo } from "../lib/format";
import type {
  ExportRecord,
  Issue,
  LandingDocument,
  ProjectReadiness,
  ProjectRequirementsResponse,
  ProjectSummary,
  ReadinessAction,
  ReadinessSnapshot,
  Requirement,
  SeionPrediction,
} from "../types";

type ProjectOverviewPageProps = {
  project?: ProjectSummary;
  readiness?: ProjectReadiness | null;
  projectRequirements?: ProjectRequirementsResponse | null;
  exports: ExportRecord[];
  issues: Issue[];
  requirements: Requirement[];
  documents: LandingDocument[];
  readinessActions: ReadinessAction[];
  readinessSnapshots: ReadinessSnapshot[];
  seionSuggestions: SeionPrediction[];
  onAcceptSeionSuggestion: (predictionId: number) => void;
  onRejectSeionSuggestion: (predictionId: number) => void;
  onToast: (message: string, tone?: "success" | "info" | "warning") => void;
  onOpenViewer?: () => void;
  onOpenProcessing?: () => void;
  onOpenDocuments?: () => void;
  onOpenDebugLogs?: () => void;
  forceViewerTab?: boolean;
};

export function ProjectOverviewPage({
  project,
  readiness,
  projectRequirements,
  exports,
}: ProjectOverviewPageProps) {
  if (!project || !readiness) {
    return <EmptyState />;
  }

  const latestExport = exports.find(
    (record) => record.project_id === project.id && record.status === "completed",
  );
  const currentMilestone = project.phase || "Not set";
  const currentMilestoneRows = (projectRequirements?.items ?? []).filter(
    (row) => normalizeMilestone(row.milestone) === normalizeMilestone(currentMilestone),
  );
  const currentMilestoneApplicableRows = currentMilestoneRows.filter(
    (row) => row.is_actionable && row.readiness_status !== "not_applicable",
  );
  const approvedRequirements = currentMilestoneApplicableRows.filter(
    (row) => row.readiness_status === "compliant",
  ).length;
  const missingRequirements = currentMilestoneApplicableRows.filter(
    (row) => row.readiness_status === "missing" || row.readiness_status === "not_evaluated",
  ).length;
  const needsReviewRequirements = currentMilestoneApplicableRows.filter(
    (row) => row.readiness_status === "needs_review",
  ).length;
  const applicableRequirements = currentMilestoneApplicableRows.length;
  const milestoneReadiness =
    applicableRequirements > 0
      ? (approvedRequirements / applicableRequirements) * 100
      : readiness.requirement_coverage.score;
  const milestoneLabel = readinessLabel(milestoneReadiness);
  const nextMilestone = "—";
  const dueDate = "TBD";

  return (
    <div className="ema-page ema-page-shell space-y-6">
      <section className="ema-liquid-section p-5">
        <div className="grid gap-5 xl:grid-cols-[1.2fr_360px_310px]">
          <div className="flex gap-4">
            <span className="ema-liquid-capsule inline-flex h-14 w-14 shrink-0 items-center justify-center text-accent">
              <Building2 size={26} aria-hidden />
            </span>

            <div className="min-w-0">
              <h2 className="truncate text-2xl font-semibold text-ink">
                {projectDisplayName(project)}
              </h2>

              <p className="mt-1 max-w-3xl text-sm text-muted">
                Deliverable tracker for owner requirements, model evidence, and project readiness.
              </p>

              <div className="mt-4 flex flex-wrap gap-3">
                <HeaderPill label="Current" value={currentMilestone} tone="blue" />
                <HeaderPill label="Next" value={nextMilestone} tone="amber" />
                <HeaderPill label="Due" value={dueDate} tone="rose" />
              </div>
            </div>
          </div>

          <div className="ema-semantic-warning-surface p-4">
            <div className="flex items-center justify-between gap-3">
              <span className="text-xs font-semibold uppercase tracking-wide text-warning">
                Current Milestone Readiness
              </span>
              <StatusBadge value={milestoneLabel} />
            </div>

            <div className="mt-3 flex items-end justify-between gap-3">
              <div>
                <div className="text-sm font-semibold text-muted">{currentMilestone}</div>
                <div className="mt-1 text-4xl font-semibold text-warning">
                  {formatPercent(milestoneReadiness)}
                </div>
              </div>

              <div className="text-right text-xs text-muted">
                <div>Next</div>
                <div className="font-semibold text-ink">{nextMilestone}</div>
              </div>
            </div>

            <div className="mt-3">
              <ProgressBar value={milestoneReadiness} tone="amber" />
            </div>

            <p className="mt-3 text-xs text-muted">
              {milestoneReadiness < 60
                ? "Behind due to incomplete requirements, open issues, or missing evidence."
                : "Milestone is progressing based on available requirement and model evidence."}
            </p>
          </div>

          <div className="ema-liquid-panel p-4">
            <div className="text-sm font-semibold text-muted">Sync / Data Status</div>

            <dl className="mt-3 space-y-3 text-sm">
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Model Sync</dt>
                <dd className="font-semibold text-ink">
                  {timeAgo(readiness.latest_sync_at)}
                </dd>
              </div>

              <div className="flex justify-between gap-3">
                <dt className="text-muted">Drawing Sync</dt>
                <dd className="flex items-center gap-2 font-semibold text-ink">
                  Candidate
                  <StatusBadge value="simulated" variant="outline" />
                </dd>
              </div>

              <div className="flex justify-between gap-3">
                <dt className="text-muted">Latest Export</dt>
                <dd className="font-semibold text-ink">
                  {formatNumber(latestExport?.element_count)} elements
                </dd>
              </div>
            </dl>

            <div className="mt-4">
              <StatusBadge value="Synced" />
            </div>
          </div>
        </div>
      </section>

      <section className="ema-card p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 className="text-base font-semibold text-ink">Milestone Progress</h3>
              <p className="mt-1 text-sm text-muted">
              Milestones represent drawing/design packages. Progress is calculated from required owner items,
              model evidence, indexed documents, and open issues.
            </p>
          </div>

          <div className="ema-liquid-capsule px-3 py-2 text-sm font-semibold text-accent">
            Active: {currentMilestone} · {formatPercent(milestoneReadiness)}
          </div>
        </div>

        <div className="mt-4">
          <MilestoneRail
            project={project}
            readiness={readiness}
            projectRequirements={projectRequirements}
          />
        </div>
      </section>

      {project.client_id == null && (
        <section className="ema-solid-warning-surface rounded-lg p-4 text-sm text-warning">
          Project has no client linked. Owner requirements cannot be evaluated until client binding is completed in Project Setup.
        </section>
      )}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-8">
        <KpiCard
          label="Project Health Score"
          value={formatPercent(project.model_health_score)}
          detail="Model QA/QC"
          icon={ShieldCheck}
          tone="rose"
        />

        <KpiCard
          label="Total Models"
          value={String(project.active_models)}
          detail="Active model rows"
          icon={Layers3}
          tone="slate"
        />

        <KpiCard
          label="Last Sync"
          value={timeAgo(readiness.latest_sync_at)}
          detail="Successful"
          icon={RefreshCw}
          tone="teal"
        />

        <KpiCard
          label="Open Issues"
          value={String(project.open_issues)}
          detail="Generated issues"
          icon={AlertCircle}
          tone="amber"
        />

        <KpiCard
          label="Critical Issues"
          value={String(readiness.open_issues.critical)}
          detail="Immediate attention"
          icon={AlertCircle}
          tone="rose"
        />

        <KpiCard
          label="Missing Requirements"
          value={String(missingRequirements)}
          detail="Applicable owner requirements"
          icon={ClipboardCheck}
          tone="amber"
        />

        <KpiCard
          label="Elements"
          value={formatNumber(latestExport?.element_count)}
          detail="Latest export"
          icon={Layers3}
          tone="blue"
        />

          <KpiCard
          label="Requirement Coverage"
          value={formatPercent(milestoneReadiness)}
          detail={`${approvedRequirements}/${applicableRequirements || 0} approved/applicable · ${missingRequirements} missing · ${needsReviewRequirements} needs review`}
          icon={Gauge}
          tone="teal"
        />
      </div>

      <section className="ema-card p-5">
        <h3 className="text-xl font-semibold text-ink">Discipline Readiness</h3>
        <p className="mt-1 text-sm text-muted">
          Discipline-level readiness based on missing requirements and critical/high issue exposure.
        </p>

        <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {readiness.trade_readiness.slice(0, 4).map((row) => (
            <div key={row.discipline} className="ema-card p-5">
              <div className="flex items-center justify-between">
                <span className="font-semibold text-ink">{row.discipline}</span>
                <span className="text-3xl font-semibold text-info">
                  {formatPercent(row.readiness)}
                </span>
              </div>

              <dl className="mt-4 space-y-2 text-sm">
                <div className="flex justify-between">
                  <dt className="text-muted">Missing Requirements</dt>
                  <dd className="font-semibold">{row.missing_requirements}</dd>
                </div>

                <div className="flex justify-between">
                  <dt className="text-muted">Critical Gaps</dt>
                  <dd className="font-semibold text-danger">
                    {row.critical_issues + row.high_issues}
                  </dd>
                </div>

                <div className="flex justify-between">
                  <dt className="text-muted">Last Sync</dt>
                  <dd className="font-semibold text-accent">
                    {timeAgo(readiness.latest_sync_at)}
                  </dd>
                </div>
              </dl>

              <div className="mt-4">
                <ProgressBar
                  value={row.readiness}
                  tone={row.readiness < 60 ? "rose" : row.readiness < 75 ? "amber" : "blue"}
                />
              </div>
            </div>
          ))}
        </div>
      </section>

      <section className="ema-card" data-no-glass>
        <div className="border-b border-line px-5 py-4">
          <h3 className="text-lg font-semibold text-ink">Recent Activity</h3>
          <p className="mt-1 text-sm text-muted">
            Recent project events, sync activity, and requirement availability.
          </p>
        </div>

        <div className="divide-y divide-line">
          {[
            [
              "Critical issue detected",
              `${project.high_issues} high-priority QA/QC items require attention`,
              "15 min ago",
            ],
            [
              "Model sync completed successfully",
              `${formatNumber(latestExport?.element_count)} elements synchronized`,
              "2h ago",
            ],
            [
              "Owner requirements loaded",
              `${currentMilestoneApplicableRows.length} current milestone requirement(s) available in database`,
              "3h ago",
            ],
          ].map(([title, detail, when]) => (
            <div key={title} className="flex items-center justify-between gap-4 px-5 py-4">
              <div>
                <div className="font-semibold text-ink">{title}</div>
                <div className="text-sm text-muted">{detail}</div>
              </div>

              <span className="text-sm text-muted">{when}</span>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

function HeaderPill({
  label,
  value,
  tone = "blue",
}: {
  label: string;
  value: string;
  tone?: "blue" | "amber" | "rose";
}) {
  const chip =
    tone === "rose"
      ? "ema-chip ema-chip-danger"
      : tone === "amber"
        ? "ema-chip ema-chip-warning"
        : "ema-chip ema-chip-accent";

  return (
    <span className={`${chip} px-3 py-2 text-sm`}>
      {label}: <span className="text-ink">{value}</span>
    </span>
  );
}

function EmptyState() {
  return (
    <section className="ema-card p-8 text-center">
      <p className="text-sm text-muted">Select a project to view readiness.</p>
    </section>
  );
}

function normalizeMilestone(value?: string | null) {
  if (!value) {
    return "";
  }

  return value.toUpperCase().replace(/[^A-Z0-9]/g, "");
}

function readinessLabel(score: number) {
  if (score >= 90) return "Ready";
  if (score >= 75) return "On Track";
  if (score >= 60) return "At Risk";
  if (score >= 40) return "Behind";
  return "Critical";
}
