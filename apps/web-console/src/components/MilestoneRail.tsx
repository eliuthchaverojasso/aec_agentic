import {
  AlertTriangle,
  CheckCircle2,
  Circle,
  Flag,
  Loader2,
  type LucideIcon,
} from "lucide-react";
import { ProgressBar } from "./ProgressBar";
import { StatusBadge } from "./StatusBadge";
import type {
  ProjectReadiness,
  ProjectRequirementsResponse,
  ProjectSummary,
} from "../types";

type MilestoneRailProps = {
  project: ProjectSummary;
  readiness: ProjectReadiness;
  projectRequirements?: ProjectRequirementsResponse | null;
};

type MilestoneStatus = "Completed" | "In Progress" | "Blocked" | "Upcoming" | "Pending";

type MilestoneCard = {
  label: string;
  status: MilestoneStatus;
  approved: number;
  applicable: number;
  missing: number;
  needsReview: number;
  score: number;
  note: string;
};

type StatusConfig = {
  icon: LucideIcon;
  badge: string;
  iconWrapClass: string;
  iconClass: string;
  cardClass: string;
  progressTone: "teal" | "amber" | "rose" | "blue" | "violet";
};

const milestoneOrder = ["DD 50%", "DD 75%", "DD 95%", "CD 50%", "CD 75%"];

const statusConfig: Record<MilestoneStatus, StatusConfig> = {
  Completed: {
    icon: CheckCircle2,
    badge: "Completed",
    iconWrapClass: "border-success bg-success text-inverse",
    iconClass: "text-accent",
    cardClass: "border-success/30 bg-success/5",
    progressTone: "teal",
  },
  "In Progress": {
    icon: Loader2,
    badge: "In Progress",
    iconWrapClass:
      "border-accent bg-accent text-inverse shadow-sm ring-4 ring-accent/10",
    iconClass: "animate-spin text-accent",
    cardClass:
      "border-accent/40 bg-accent/[0.04] shadow-sm ring-2 ring-accent/10",
    progressTone: "amber",
  },
  Blocked: {
    icon: AlertTriangle,
    badge: "Blocked",
    iconWrapClass:
      "border-danger bg-danger text-inverse shadow-sm ring-4 ring-danger/10",
    iconClass: "animate-pulse text-danger",
    cardClass: "border-danger/40 bg-danger/5 shadow-sm ring-2 ring-danger/10",
    progressTone: "rose",
  },
  Upcoming: {
    icon: Flag,
    badge: "Upcoming",
    iconWrapClass: "border-line bg-surface text-muted",
    iconClass: "text-muted",
    cardClass: "border-line bg-surface",
    progressTone: "blue",
  },
  Pending: {
    icon: Circle,
    badge: "Pending",
    iconWrapClass: "border-line bg-surface text-muted",
    iconClass: "text-muted",
    cardClass: "border-line bg-surface",
    progressTone: "blue",
  },
};

export function MilestoneRail({
  project,
  readiness,
  projectRequirements,
}: MilestoneRailProps) {
  const rows = projectRequirements?.items ?? [];
  const currentMilestone = normalizeMilestone(project.phase) ?? "DD 50%";
  const currentIndex = Math.max(
    0,
    milestoneOrder.findIndex((milestone) => milestone === currentMilestone),
  );

  const cards = milestoneOrder.map((label, index) =>
    buildMilestoneCard(label, index, currentIndex, rows),
  );

  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-5">
      {cards.map((card) => {
        const config = statusConfig[card.status];
        const Icon = config.icon;

        return (
          <article
            key={card.label}
            className={`group flex h-full min-h-[245px] flex-col rounded-2xl border p-4 transition duration-300 hover:-translate-y-1 hover:shadow-lg ${config.cardClass}`}
          >
            <div className="flex items-start justify-between gap-3 p-2">
              <div
                className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border-4 transition duration-300 group-hover:scale-105 ${config.iconWrapClass}`}
              >
                <Icon size={20} className={config.iconClass} aria-hidden />
              </div>

              <div className="shrink-0">
                <StatusBadge value={config.badge} />
              </div>
            </div>

            <div className="mt-4">
              <h4 className="text-lg font-semibold leading-tight text-ink">{card.label}</h4>
              <p className={`mt-1 text-xs font-semibold ${statusToneClass(card.status)}`}>
                {card.note}
              </p>
            </div>

            <div className="mt-auto rounded-xl border border-line bg-surface p-3 text-sm">
              <MetricRow label="Approved / Applicable" value={`${card.approved}/${card.applicable}`} />
              <MetricRow label="Missing" value={String(card.missing)} />
              <MetricRow label="Needs Review" value={String(card.needsReview)} />
              <MetricRow label="Readiness" value={`${Math.round(card.score)}%`} />

              <div className="mt-3">
                <ProgressBar value={card.score} tone={config.progressTone} />
              </div>
            </div>
          </article>
        );
      })}
    </div>
  );
}

function buildMilestoneCard(
  label: string,
  index: number,
  currentIndex: number,
  rows: ProjectRequirementsResponse["items"],
): MilestoneCard {
  const normalizedLabel = normalizeMilestone(label);
  const milestoneRows = rows.filter((row) => normalizeMilestone(row.milestone) === normalizedLabel);
  const actionableRows = milestoneRows.filter((row) => row.is_actionable);
  const applicableRows = actionableRows.filter((row) => row.readiness_status !== "not_applicable");
  const approved = applicableRows.filter((row) => row.readiness_status === "compliant").length;
  const notApproved = applicableRows.filter((row) => row.readiness_status === "non_compliant").length;
  const needsReview = applicableRows.filter((row) => row.readiness_status === "needs_review").length;
  const missing = applicableRows.filter(
    (row) => row.readiness_status === "missing" || row.readiness_status === "not_evaluated",
  ).length;
  const applicable = applicableRows.length;
  const score = applicable > 0 ? (approved / applicable) * 100 : 0;

  let status: MilestoneStatus = "Pending";
  if (index === currentIndex) {
    status = score >= 100 && applicable > 0 ? "Completed" : score < 35 ? "Blocked" : "In Progress";
  } else if (index < currentIndex) {
    status = score >= 100 && applicable > 0 ? "Completed" : "Blocked";
  } else if (index === currentIndex + 1) {
    status = "Upcoming";
  }

  const note =
    status === "Completed"
      ? "Approved for package closeout"
      : status === "Blocked"
        ? `${missing + needsReview + notApproved} items need attention`
        : status === "In Progress"
          ? `${approved} approved / ${applicable} applicable`
          : status === "Upcoming"
            ? "Next package"
            : "Not started";

  return {
    label,
    status,
    approved,
    applicable,
    missing,
    needsReview,
    score,
    note,
  };
}

function normalizeMilestone(value?: string | null) {
  if (!value) {
    return null;
  }
  const normalized = value.toUpperCase().replace(/[^A-Z0-9]/g, "");
  for (const milestone of milestoneOrder) {
    if (milestone.toUpperCase().replace(/[^A-Z0-9]/g, "") === normalized) {
      return milestone;
    }
  }
  return value.trim();
}

function statusToneClass(status: MilestoneStatus) {
  if (status === "Blocked") {
    return "text-danger";
  }
  if (status === "In Progress" || status === "Completed") {
    return "text-accent";
  }
  return "text-muted";
}

function MetricRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="mt-1.5 flex items-center justify-between gap-3">
      <span className="text-muted">{label}</span>
      <strong className="font-semibold text-ink">{value}</strong>
    </div>
  );
}
