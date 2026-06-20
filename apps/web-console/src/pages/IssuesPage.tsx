import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, ArrowRight, Search } from "lucide-react";
import { api } from "../api/client";
import { DetailDrawer, DetailGrid, DetailItem } from "../components/DetailDrawer";
import { StatusBadge } from "../components/StatusBadge";
import { formatDateTime } from "../lib/format";
import type { Issue, Requirement } from "../types";

type IssuesPageProps = {
  issues: Issue[];
  requirements: Requirement[];
  initialFilter?: { severity?: "high" | "critical" | "all"; search?: string } | null;
};

export function IssuesPage({ issues, requirements, initialFilter }: IssuesPageProps) {
  const [selectedIssue, setSelectedIssue] = useState<Issue | null>(null);
  const [search, setSearch] = useState(initialFilter?.search || "");
  const [severity, setSeverity] = useState(initialFilter?.severity || "all");
  const [status, setStatus] = useState("all");
  const [rule, setRule] = useState("all");
  const [page, setPage] = useState(1);
  useEffect(() => {
    setSearch(initialFilter?.search || "");
    setSeverity(initialFilter?.severity || "all");
  }, [initialFilter]);

  const visibleIssues = useMemo(
    () =>
      issues.filter((issue) => {
        const query = search.trim().toLowerCase();
        const matchesSearch =
          !query ||
          `${issue.id} ${issue.message || ""} ${issue.rule_code || ""} ${issue.issue_type || ""}`
            .toLowerCase()
            .includes(query);
        const matchesSeverity = severity === "all" || issue.severity === severity;
        const matchesStatus = status === "all" || issue.status === status;
        const matchesRule = rule === "all" || issue.rule_code === rule;
        return matchesSearch && matchesSeverity && matchesStatus && matchesRule;
      }),
    [issues, search, severity, status, rule],
  );
  const rules = useMemo(() => Array.from(new Set(issues.map((issue) => issue.rule_code).filter(Boolean))).sort(), [issues]);
  const pageSize = 25;
  const pageCount = Math.max(1, Math.ceil(visibleIssues.length / pageSize));
  const pagedIssues = visibleIssues.slice((page - 1) * pageSize, page * pageSize);
  const relatedRequirement = useMemo(() => {
    if (!selectedIssue) {
      return undefined;
    }

    const message = `${selectedIssue.message || ""} ${selectedIssue.issue_type || ""}`.toLowerCase();
    return requirements.find((requirement) => {
      const discipline = requirement.discipline.toLowerCase();
      return message.includes(discipline) || message.includes(discipline.slice(0, 5));
    });
  }, [requirements, selectedIssue]);

  return (
    <>
    <section className="ema-page ema-page-shell ema-card" data-no-glass>
        <div className="ema-card-header">
          <div>
            <h2 className="text-sm font-semibold text-ink">Issues and Gaps</h2>
            <p className="text-xs text-muted">Showing {pagedIssues.length} of {visibleIssues.length} filtered issues ({issues.length} loaded)</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <div className="ema-search-shell w-56">
              <Search size={15} className="text-muted" aria-hidden />
              <input
                placeholder="Search issues"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
            <select
              className="ema-select h-9 px-3"
              value={severity}
              onChange={(event) => setSeverity(event.target.value as "high" | "critical" | "all")}
            >
              <option value="all">All severities</option>
              <option value="critical">Critical</option>
              <option value="high">High</option>
            </select>
            <select className="ema-select h-9 px-3" value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="all">All statuses</option>
              {["open", "in_review", "reviewed", "closed", "reopened"].map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
            <select className="ema-select h-9 px-3" value={rule} onChange={(event) => setRule(event.target.value)}>
              <option value="all">All rules</option>
              {rules.map((item) => <option key={item || ""} value={item || ""}>{item}</option>)}
            </select>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-line text-sm">
            <thead className="bg-surface-2 text-left text-xs font-semibold uppercase tracking-wide text-muted">
              <tr>
                <th className="px-4 py-3">Issue</th>
                <th className="px-4 py-3">Severity</th>
                <th className="px-4 py-3">Rule</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Created</th>
                <th className="px-4 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {pagedIssues.map((issue) => (
                <tr
                  key={issue.id}
                  className="cursor-pointer hover:bg-surface-2"
                  onClick={() => setSelectedIssue(issue)}
                >
                  <td className="max-w-2xl px-4 py-3">
                    <div className="font-medium text-ink">#{issue.id}</div>
                    <div className="text-muted">{issue.message || issue.issue_type || "Issue"}</div>
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge value={issue.severity} />
                  </td>
                  <td className="px-4 py-3 text-muted">{issue.rule_code || "-"}</td>
                  <td className="px-4 py-3">
                    <StatusBadge value={issue.status} />
                  </td>
                  <td className="px-4 py-3 text-muted">{formatDateTime(issue.created_at)}</td>
                  <td className="px-4 py-3 text-right text-accent">
                    <ArrowRight className="ml-auto h-4 w-4" aria-hidden />
                  </td>
                </tr>
              ))}
              {visibleIssues.length === 0 && (
                <tr>
                  <td className="px-4 py-6 text-sm text-muted" colSpan={6}>
                    No issues match the current filters.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        <div className="flex items-center justify-between border-t border-line px-4 py-3 text-sm text-muted">
          <span>Page {page} of {pageCount}</span>
          <div className="flex gap-2">
            <button className="ema-btn-secondary px-3 py-1 text-xs disabled:opacity-50" type="button" disabled={page <= 1} onClick={() => setPage((value) => Math.max(1, value - 1))}>Previous</button>
            <button className="ema-btn-secondary px-3 py-1 text-xs disabled:opacity-50" type="button" disabled={page >= pageCount} onClick={() => setPage((value) => Math.min(pageCount, value + 1))}>Next</button>
          </div>
        </div>
      </section>

      <IssueDrawer
        issue={selectedIssue}
        relatedRequirement={relatedRequirement}
        onClose={() => setSelectedIssue(null)}
      />
    </>
  );
}

function IssueDrawer({
  issue,
  relatedRequirement,
  onClose,
}: {
  issue: Issue | null;
  relatedRequirement?: Requirement;
  onClose: () => void;
}) {
  const observedValues = issue?.traceability?.observed_values || {};
  const family = valueAsText(observedValues.Family);
  const category = valueAsText(observedValues.Category);
  const panel = valueAsText(observedValues.Panel);
  const level = valueAsText(observedValues.Level);
  const updateIssue = (status: string, notes?: string) => {
    if (!issue) return;
    api.updateIssue(issue.id, { status, resolution_notes: notes }).catch(() => undefined);
  };

  return (
    <DetailDrawer
      title="Issue Detail"
      subtitle={issue ? `${issue.rule_code || "Rule"} / Issue #${issue.id}` : undefined}
      isOpen={Boolean(issue)}
      onClose={onClose}
    >
      {issue && (
        <div className="space-y-5">
          <div className="ema-card p-4">
            <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-ink">
              <AlertTriangle className="h-4 w-4 text-warning" aria-hidden />
              QA/QC Finding
            </div>
            <p className="text-sm leading-6 text-muted">{issue.message || issue.issue_type}</p>
          </div>

          <DetailGrid>
            <DetailItem label="Severity" value={<StatusBadge value={issue.severity} />} />
            <DetailItem label="Status" value={<StatusBadge value={issue.status} />} />
            <DetailItem label="Rule" value={issue.rule_code || "-"} />
            <DetailItem label="Source" value={issue.source} />
            <DetailItem label="Model Element" value={issue.element_db_id ? `Element ${issue.element_db_id}` : "-"} />
            <DetailItem label="Category" value={category || issue.issue_type || "-"} />
            <DetailItem label="Family" value={family || "-"} />
            <DetailItem label="Panel / Level" value={panel || level || "-"} />
            <DetailItem label="Created" value={formatDateTime(issue.created_at)} />
            <DetailItem label="Export" value={`Export #${issue.export_id}`} />
          </DetailGrid>

          <div className="ema-card p-4">
            <h3 className="text-sm font-semibold text-ink">Related Requirement</h3>
            {relatedRequirement ? (
              <div className="mt-3 space-y-2 text-sm text-muted">
                <div className="flex items-center gap-2">
                  <StatusBadge value={relatedRequirement.discipline} />
                  <span>REQ-{relatedRequirement.id}</span>
                </div>
                <p>{relatedRequirement.requirement_text}</p>
              </div>
            ) : (
              <p className="mt-2 text-sm text-muted">
                No direct requirement link exists yet. Treat this as a QA/QC gap for review.
              </p>
            )}
          </div>

          <div className="ema-notice-warning p-4">
            <h3 className="text-sm font-semibold text-ink">Recommended Action</h3>
            <p className="mt-2 text-sm text-muted">{recommendedAction(issue)}</p>
            <div className="mt-4 flex flex-wrap gap-2">
              <button className="ema-btn-secondary text-sm font-semibold" type="button" onClick={() => updateIssue("reviewed", "Marked reviewed from dashboard.")}>Mark Reviewed</button>
              <button className="ema-btn-secondary text-sm font-semibold" type="button" onClick={() => updateIssue("closed", "Closed from dashboard review.")}>Close Issue</button>
              <button className="ema-btn-secondary text-sm font-semibold text-info border-info" type="button" onClick={() => updateIssue("reopened", "Reopened from dashboard review.")}>Reopen Issue</button>
            </div>
          </div>
        </div>
      )}
    </DetailDrawer>
  );
}

function valueAsText(value: unknown) {
  if (value === null || value === undefined || value === "") {
    return "";
  }
  return String(value);
}

function recommendedAction(issue: Issue) {
  if (issue.rule_code === "R002") {
    return "Assign to electrical or lighting reviewer and confirm panel connectivity.";
  }
  if (issue.rule_code === "R001") {
    return "Assign to model owner and fill the missing level or location parameter.";
  }
  return "Review the model element, confirm evidence, and update issue status.";
}
