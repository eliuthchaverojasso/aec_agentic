import { StatusBadge } from "../components/StatusBadge";
import type {
  Issue,
  LandingDocument,
  ProjectReadiness,
  ProjectRequirementsResponse,
  ProjectSummary,
  ProjectRequirementRow,
} from "../types";

type TradeReadinessPageProps = {
  project?: ProjectSummary | null;
  readiness?: ProjectReadiness | null;
  projectRequirements?: ProjectRequirementsResponse | null;
  issues: Issue[];
  documents: LandingDocument[];
};

export function TradeReadinessPage({
  project,
  readiness,
  projectRequirements,
  issues,
  documents,
}: TradeReadinessPageProps) {
  const rows = buildTradeRows(projectRequirements?.items ?? [], project?.phase, issues);
  const specs = documents.filter((document) => document.document_category === "specification");
  const currentMilestone = project?.phase || "Not set";

  return (
    <section className="ema-page ema-page-shell ema-card" data-no-glass>
      <div className="ema-card-header">
        <div>
          <h2 className="text-sm font-semibold text-ink">Trade Readiness</h2>
          <p className="mt-0.5 text-xs text-muted">
            Deterministic discipline readiness for the selected milestone. Calculated from project requirements only.
          </p>
        </div>

        <div className="ema-liquid-capsule px-3 py-2 text-sm font-semibold text-accent">
          Current milestone: {currentMilestone}
        </div>
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-line text-sm">
          <thead className="bg-surface-2 text-left text-xs font-semibold uppercase tracking-wide text-muted">
            <tr>
              <th className="px-4 py-3">Trade</th>
              <th className="px-4 py-3">Readiness</th>
              <th className="px-4 py-3">Approved / Applicable</th>
              <th className="px-4 py-3">Missing</th>
              <th className="px-4 py-3">Needs Review</th>
              <th className="px-4 py-3 text-danger">Critical</th>
              <th className="px-4 py-3 text-warning">High</th>
              <th className="px-4 py-3">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {rows.map((row) => (
              <tr key={row.discipline} className="hover:bg-surface-2">
                <td className="px-4 py-3 font-medium text-ink">{row.discipline}</td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    <div className="ema-progress-track w-28">
                      <div
                        className="ema-progress-fill"
                        style={{ width: `${Math.min(100, row.readiness)}%` }}
                      />
                    </div>
                    <span className="font-semibold text-ink">{Math.round(row.readiness)}%</span>
                  </div>
                </td>
                <td className="px-4 py-3 text-muted">
                  {row.approved}/{row.applicable}
                </td>
                <td className="px-4 py-3 text-muted">{row.missing}</td>
                <td className="px-4 py-3 text-muted">{row.needs_review}</td>
                <td className="px-4 py-3 font-medium text-danger">{row.critical_issues}</td>
                <td className="px-4 py-3 font-medium text-warning">{row.high_issues}</td>
                <td className="px-4 py-3">
                  <StatusBadge value={row.label} />
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td className="px-4 py-6 text-sm text-muted" colSpan={8}>
                  No milestone-specific trade readiness rows are available yet. Select a project with owner requirements.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="border-t border-line p-4 text-sm text-muted">
        {readiness ? (
          <>
            Overall readiness: {Math.round(readiness.overall_readiness)}%. Open issues: {issues.length}.{" "}
            Official specification documents indexed: {specs.length}.
          </>
        ) : (
          "Open issues and indexed documents are shown for context only."
        )}
      </div>
    </section>
  );
}

type TradeRow = {
  discipline: string;
  readiness: number;
  label: string;
  approved: number;
  applicable: number;
  missing: number;
  needs_review: number;
  critical_issues: number;
  high_issues: number;
};

function buildTradeRows(
  rows: ProjectRequirementRow[],
  milestone?: string | null,
  issues: Issue[] = [],
): TradeRow[] {
  const normalizedMilestone = normalizeMilestone(milestone);
  const filteredRows = normalizedMilestone
    ? rows.filter((row) => normalizeMilestone(row.milestone) === normalizedMilestone)
    : rows;

  const statsByDiscipline: Record<string, TradeRow> = {};
  for (const row of filteredRows) {
    if (!row.is_actionable) {
      continue;
    }

    const discipline = row.discipline || "Unmapped";
    const current = statsByDiscipline[discipline] ?? {
      discipline,
      readiness: 0,
      label: "Critical",
      approved: 0,
      applicable: 0,
      missing: 0,
      needs_review: 0,
      critical_issues: 0,
      high_issues: 0,
    };

    current.applicable += row.readiness_status === "not_applicable" ? 0 : 1;
    if (row.readiness_status === "compliant") {
      current.approved += 1;
    } else if (row.readiness_status === "missing" || row.readiness_status === "not_evaluated") {
      current.missing += 1;
    } else if (row.readiness_status === "needs_review") {
      current.needs_review += 1;
    }

    statsByDiscipline[discipline] = current;
  }

  return Object.values(statsByDiscipline)
    .sort((a, b) => a.discipline.localeCompare(b.discipline))
    .map((row) => {
      const readiness = row.applicable > 0 ? (row.approved / row.applicable) * 100 : 0;
      const issueCounts = issueCountsForDiscipline(issues, row.discipline);
      return {
        ...row,
        readiness,
        label: readiness >= 90 ? "Ready" : readiness >= 75 ? "On Track" : readiness >= 60 ? "At Risk" : readiness >= 40 ? "Behind" : "Critical",
        critical_issues: issueCounts.critical,
        high_issues: issueCounts.high,
      };
    });
}

function normalizeMilestone(value?: string | null) {
  if (!value) {
    return null;
  }
  return value.toUpperCase().replace(/[^A-Z0-9]/g, "");
}

function issueCountsForDiscipline(issues: Issue[], discipline: string) {
  let critical = 0;
  let high = 0;

  for (const issue of issues) {
    if (issue.status !== "open") {
      continue;
    }
    const text = `${issue.rule_code || ""} ${issue.issue_type || ""} ${issue.message || ""}`.toLowerCase();
    if (!text.includes(discipline.toLowerCase())) {
      continue;
    }
    if (issue.severity === "critical") {
      critical += 1;
    }
    if (issue.severity === "high") {
      high += 1;
    }
  }

  return { critical, high };
}
