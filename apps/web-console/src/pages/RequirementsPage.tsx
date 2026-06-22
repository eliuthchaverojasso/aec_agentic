import { useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  ClipboardCheck,
  Clock,
  Link2,
  PlusCircle,
  RotateCcw,
  Search,
  XCircle,
} from "lucide-react";
import { api } from "../api/client";
import { DetailDrawer, DetailGrid, DetailItem } from "../components/DetailDrawer";
import { StatusBadge } from "../components/StatusBadge";
import type {
  Client,
  Issue,
  ProjectReadiness,
  ProjectRequirementRow,
  ProjectRequirementsResponse,
  ProjectSummary,
  RequirementEvidence,
} from "../types";
import { formatDate, formatDateTime, parseDateValue } from "../lib/format";

type RequirementsPageProps = {
  selectedProject?: ProjectSummary;
  clients: Client[];
  requirements: unknown[];
  projectRequirements?: ProjectRequirementsResponse | null;
  issues: Issue[];
  readiness?: ProjectReadiness | null;
  onToast: (message: string, tone?: "success" | "info" | "warning") => void;
  onRefreshProjectData?: () => Promise<void> | void;
};

type RequirementTab = "mapping" | "evidence";

type EvidenceFilter =
  | "all"
  | "no_evidence"
  | "candidate"
  | "needs_review"
  | "accepted"
  | "rejected";

type BulkMilestone = "DD 30%" | "DD 50%" | "DD 75%" | "DD 95%" | "CD 50%" | "CD 75%";

const PAGE_SIZE_OPTIONS = [6, 7, 10, 15, 25];
const DEFAULT_PAGE_SIZE = 7;

const REQUIREMENT_TABS: Array<{
  key: RequirementTab;
  label: string;
  description: string;
}> = [
  {
    key: "mapping",
    label: "Milestone Mapping",
    description: "Assign requirements to project milestones.",
  },
  {
    key: "evidence",
    label: "Evidence Review",
    description: "Review evidence status and reviewer decisions.",
  },
];

const EVIDENCE_FILTERS: Array<{
  key: EvidenceFilter;
  label: string;
}> = [
  { key: "all", label: "All Evidence" },
  { key: "no_evidence", label: "No Evidence" },
  { key: "candidate", label: "Candidate" },
  { key: "needs_review", label: "Needs Review" },
  { key: "accepted", label: "Accepted" },
  { key: "rejected", label: "Rejected" },
];

const BULK_MILESTONES: BulkMilestone[] = [
  "DD 30%",
  "DD 50%",
  "DD 75%",
  "DD 95%",
  "CD 50%",
  "CD 75%",
];

export function RequirementsPage({
  selectedProject,
  clients,
  projectRequirements,
  issues,
  readiness,
  onToast,
  onRefreshProjectData,
}: RequirementsPageProps) {
  const [selectedRequirement, setSelectedRequirement] = useState<ProjectRequirementRow | null>(null);
  const [search, setSearch] = useState("");
  const [sourceFilter, setSourceFilter] = useState("all");
  const [disciplineFilter, setDisciplineFilter] = useState("all");
  const [milestoneFilter, setMilestoneFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [evidenceFilter, setEvidenceFilter] = useState<EvidenceFilter>("all");
  const [activeTab, setActiveTab] = useState<RequirementTab>("mapping");
  const [selectedRequirementIds, setSelectedRequirementIds] = useState<Set<number>>(new Set());
  const [bulkMilestone, setBulkMilestone] = useState<BulkMilestone>("DD 75%");
  const [currentPage, setCurrentPage] = useState(1);
  const [rowsPerPage, setRowsPerPage] = useState(DEFAULT_PAGE_SIZE);
  const [busyAction, setBusyAction] = useState(false);

  const clientId = selectedProject?.client_id ?? readiness?.client_id;
  const client = clients.find((item) => item.id === clientId);
  const projectId = selectedProject?.id ?? readiness?.project_id;
  const projectRows = projectRequirements?.items ?? [];

  const sources = useMemo(
    () =>
      Array.from(
        new Set(
          projectRows
            .map((requirement) => requirement.source || "Owner Requirements")
            .filter(Boolean),
        ),
      ).sort(),
    [projectRows],
  );

  const disciplines = useMemo(
    () =>
      Array.from(
        new Set(
          projectRows
            .map((requirement) => requirement.discipline)
            .filter(Boolean),
        ),
      ).sort(),
    [projectRows],
  );

  const milestones = useMemo(
    () =>
      Array.from(
        new Set(
          projectRows
            .map((requirement) => requirement.milestone || "Unassigned")
            .filter(Boolean),
        ),
      ).sort(),
    [projectRows],
  );

  const statuses = useMemo(
    () =>
      Array.from(
        new Set(
          projectRows
            .map((requirement) => formatOwnerStatusFromRow(requirement))
            .filter(Boolean),
        ),
      ).sort(),
    [projectRows],
  );

  const requirementCounts = useMemo(
    () => getRequirementCounts(projectRows),
    [projectRows],
  );

  const visibleRequirements = useMemo(
    () =>
      projectRows.filter((requirement) => {
        const query = search.trim().toLowerCase();

        const source = requirement.source || "Owner Requirements";
        const milestone = requirement.milestone || "Unassigned";
        const status = formatOwnerStatusFromRow(requirement);
        const evidence = getEvidenceFilterKey(requirement);

        const matchesSearch =
          !query ||
          `${requirement.requirement_text} ${requirement.discipline} ${requirement.category || ""} ${source} ${milestone} ${status}`
            .toLowerCase()
            .includes(query);

        const matchesSource = sourceFilter === "all" || source === sourceFilter;
        const matchesDiscipline =
          disciplineFilter === "all" || requirement.discipline === disciplineFilter;
        const matchesMilestone = milestoneFilter === "all" || milestone === milestoneFilter;
        const matchesStatus = statusFilter === "all" || status === statusFilter;
        const matchesEvidence = evidenceFilter === "all" || evidence === evidenceFilter;

        return (
          matchesSearch &&
          matchesSource &&
          matchesDiscipline &&
          matchesMilestone &&
          matchesStatus &&
          matchesEvidence
        );
      }),
    [
      disciplineFilter,
      evidenceFilter,
      milestoneFilter,
      projectRows,
      search,
      sourceFilter,
      statusFilter,
    ],
  );

  useEffect(() => {
    setCurrentPage(1);
  }, [
    activeTab,
    disciplineFilter,
    evidenceFilter,
    milestoneFilter,
    rowsPerPage,
    search,
    sourceFilter,
    statusFilter,
  ]);

  const totalPages = Math.max(
    1,
    Math.ceil(visibleRequirements.length / rowsPerPage),
  );

  const paginatedRequirements = useMemo(() => {
    const start = (currentPage - 1) * rowsPerPage;
    return visibleRequirements.slice(start, start + rowsPerPage);
  }, [currentPage, rowsPerPage, visibleRequirements]);

  const relatedIssue = useMemo(() => {
    if (!selectedRequirement) {
      return undefined;
    }

    return issues.find((issue) => {
      const message = `${issue.message || ""} ${issue.issue_type || ""}`.toLowerCase();
      const discipline = selectedRequirement.discipline.toLowerCase();

      return message.includes(discipline) || message.includes(discipline.slice(0, 5));
    });
  }, [issues, selectedRequirement]);

  const filterCount = [
    search.trim() ? "search" : null,
    sourceFilter !== "all" ? "source" : null,
    disciplineFilter !== "all" ? "discipline" : null,
    milestoneFilter !== "all" ? "milestone" : null,
    statusFilter !== "all" ? "status" : null,
    evidenceFilter !== "all" ? "evidence" : null,
  ].filter(Boolean).length;

  function resetFilters() {
    setSearch("");
    setSourceFilter("all");
    setDisciplineFilter("all");
    setMilestoneFilter("all");
    setStatusFilter("all");
    setEvidenceFilter("all");
    setCurrentPage(1);
  }

  function toggleRequirement(requirementId: number) {
    setSelectedRequirementIds((current) => {
      const next = new Set(current);

      if (next.has(requirementId)) {
        next.delete(requirementId);
      } else {
        next.add(requirementId);
      }

      return next;
    });
  }

  function toggleAllVisible() {
    setSelectedRequirementIds((current) => {
      const visibleIds = paginatedRequirements.map((row) => row.requirement_id);
      const allSelected =
        visibleIds.length > 0 && visibleIds.every((id) => current.has(id));

      if (allSelected) {
        const next = new Set(current);
        visibleIds.forEach((id) => next.delete(id));
        return next;
      }

      return new Set([...Array.from(current), ...visibleIds]);
    });
  }

  async function handleBulkAssignMilestone() {
    if (selectedRequirementIds.size === 0) {
      onToast("Select at least one requirement before assigning a milestone.", "warning");
      return;
    }

    if (!projectId) {
      onToast("Project context is unavailable.", "warning");
      return;
    }

    setBusyAction(true);
    try {
      await Promise.all(
        Array.from(selectedRequirementIds).map((requirementId) =>
          api.updateRequirementMapping(projectId, requirementId, {
            milestone: bulkMilestone,
          }),
        ),
      );
      onToast(`${selectedRequirementIds.size} requirement(s) assigned to ${bulkMilestone}.`, "success");
      setSelectedRequirementIds(new Set());
      await onRefreshProjectData?.();
    } catch {
      onToast("Milestone assignment failed.", "warning");
    } finally {
      setBusyAction(false);
    }
  }

  return (
    <>
      <section className="ema-page ema-page-shell space-y-6">
        <section className="ema-liquid-section p-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h2 className="text-2xl font-semibold text-ink">Owner Requirements</h2>
              <p className="mt-1 max-w-4xl text-sm text-muted">
                Review owner requirements, map them to milestones, and validate evidence used for readiness tracking.
              </p>
              <p className="mt-2 text-xs text-subtle">
                Accepted evidence updates readiness tracking. It does not represent official code or compliance approval.
              </p>
            </div>

            <div className="ema-liquid-capsule px-3 py-2 text-xs">
              <div className="font-semibold text-ink">
                {client?.display_name || projectRequirements?.client_name || "No client linked"}
              </div>
              <div className="text-muted">
                {selectedProject?.project_name || selectedProject?.project_title || "No project selected"}
              </div>
              {readiness && (
                <div className="mt-2">
                  <StatusBadge value={readiness.requirement_coverage.label} />
                </div>
              )}
            </div>
          </div>

          {projectRequirements && (
            <div className="mt-5 grid gap-3 md:grid-cols-4 xl:grid-cols-7">
              <Summary label="Total" value={projectRequirements.counts.total} />
              <Summary label="Actionable" value={projectRequirements.counts.actionable} />
              <Summary label="Covered" value={projectRequirements.counts.covered} />
              <Summary label="Missing" value={projectRequirements.counts.missing} />
              <Summary label="Needs Review" value={projectRequirements.counts.needs_review} />
              <Summary label="Evaluated" value={projectRequirements.counts.evaluated} />
              <Summary label="Non-actionable" value={projectRequirements.counts.non_actionable} />
            </div>
          )}
        </section>

        <section className="ema-card p-4">
          <div className="flex flex-wrap gap-2">
            {REQUIREMENT_TABS.map((tab) => {
              const active = activeTab === tab.key;

              return (
                <button
                  key={tab.key}
                  type="button"
                  onClick={() => setActiveTab(tab.key)}
                  className={`rounded-xl border px-4 py-3 text-left transition ${
                    active
                      ? "border-accent/40 bg-accent/[0.05] text-ink"
                      : "border-line bg-surface text-muted hover:bg-surface-2"
                  }`}
                >
                  <div className="text-sm font-semibold">{tab.label}</div>
                  <div className="mt-1 text-xs">{tab.description}</div>
                </button>
              );
            })}
          </div>
        </section>

        <section className="ema-card" data-no-glass>
          <div className="ema-card-header">
            <div>
              <h3 className="text-lg font-semibold text-ink">
                {activeTab === "mapping" ? "Milestone Mapping" : "Evidence Review"}
              </h3>
              <p className="text-sm text-muted">
                {activeTab === "mapping"
                  ? "Select requirements and assign them to project milestones."
                  : "Focus on evidence status, reviewer decisions, and missing coverage."}
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <div className="ema-search-shell w-72">
                <Search size={15} className="text-muted" aria-hidden />
                <input
                  placeholder="Search requirements"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </div>

              <button
                type="button"
                className="ema-btn-secondary inline-flex h-9 items-center gap-2"
                onClick={resetFilters}
                disabled={filterCount === 0}
                title="Reset all filters"
              >
                <RotateCcw size={14} aria-hidden />
                Reset
                {filterCount > 0 ? ` (${filterCount})` : ""}
              </button>
            </div>
          </div>

          <div className="grid gap-3 border-b border-line px-5 py-4 md:grid-cols-2 xl:grid-cols-5">
            <FilterSelect
              label="Source"
              value={sourceFilter}
              onChange={setSourceFilter}
              options={sources}
              allLabel="All sources"
            />

            <FilterSelect
              label="Discipline"
              value={disciplineFilter}
              onChange={setDisciplineFilter}
              options={disciplines}
              allLabel="All disciplines"
            />

            <FilterSelect
              label="Milestone"
              value={milestoneFilter}
              onChange={setMilestoneFilter}
              options={milestones}
              allLabel="All milestones"
            />

            <FilterSelect
              label="Status"
              value={statusFilter}
              onChange={setStatusFilter}
              options={statuses}
              allLabel="All statuses"
            />

            <label className="text-xs font-semibold uppercase tracking-wide text-muted">
              Evidence
              <select
                className="ema-select mt-1 h-9 w-full px-3 text-sm normal-case"
                value={evidenceFilter}
                onChange={(event) => setEvidenceFilter(event.target.value as EvidenceFilter)}
              >
                {EVIDENCE_FILTERS.map((filter) => (
                  <option key={filter.key} value={filter.key}>
                    {filter.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {activeTab === "mapping" && (
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-5 py-4">
              <div className="text-sm text-muted">
                <span className="font-semibold text-ink">{selectedRequirementIds.size}</span> selected requirement(s)
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <select
                  className="ema-select h-9 px-3"
                  value={bulkMilestone}
                  onChange={(event) => setBulkMilestone(event.target.value as BulkMilestone)}
                >
                  {BULK_MILESTONES.map((milestone) => (
                    <option key={milestone} value={milestone}>
                      {milestone}
                    </option>
                  ))}
                </select>

                <button
                  type="button"
                  className="ema-btn-secondary h-9"
                  disabled={busyAction}
                  onClick={handleBulkAssignMilestone}
                >
                  Assign Milestone
                </button>
              </div>
            </div>
          )}

          <div className="grid gap-3 border-b border-line px-5 py-4 md:grid-cols-5">
            <StatusMiniCard
              label="No Evidence"
              value={requirementCounts.noEvidence}
              icon={Clock}
              active={evidenceFilter === "no_evidence"}
              onClick={() => setEvidenceFilter(evidenceFilter === "no_evidence" ? "all" : "no_evidence")}
            />
            <StatusMiniCard
              label="Candidate"
              value={requirementCounts.candidate}
              icon={Link2}
              active={evidenceFilter === "candidate"}
              onClick={() => setEvidenceFilter(evidenceFilter === "candidate" ? "all" : "candidate")}
            />
            <StatusMiniCard
              label="Needs Review"
              value={requirementCounts.needsReview}
              icon={AlertTriangle}
              active={evidenceFilter === "needs_review"}
              onClick={() => setEvidenceFilter(evidenceFilter === "needs_review" ? "all" : "needs_review")}
            />
            <StatusMiniCard
              label="Accepted"
              value={requirementCounts.accepted}
              icon={CheckCircle2}
              active={evidenceFilter === "accepted"}
              onClick={() => setEvidenceFilter(evidenceFilter === "accepted" ? "all" : "accepted")}
            />
            <StatusMiniCard
              label="Rejected"
              value={requirementCounts.rejected}
              icon={XCircle}
              active={evidenceFilter === "rejected"}
              onClick={() => setEvidenceFilter(evidenceFilter === "rejected" ? "all" : "rejected")}
            />
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-5 py-3">
            <div className="text-sm text-muted">
              Showing{" "}
              <span className="font-semibold text-ink">{visibleRequirements.length}</span>{" "}
              filtered requirement(s)
              {filterCount > 0 && (
                <span className="ml-2 rounded-full bg-accent/[0.08] px-2 py-1 text-xs font-semibold text-accent">
                  {filterCount} active filter(s)
                </span>
              )}
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <label className="flex items-center gap-2 text-sm text-muted">
                Rows
                <select
                  className="ema-select h-9 px-3"
                  value={rowsPerPage}
                  onChange={(event) => setRowsPerPage(Number(event.target.value))}
                >
                  {PAGE_SIZE_OPTIONS.map((size) => (
                    <option key={size} value={size}>
                      {size} / page
                    </option>
                  ))}
                </select>
              </label>

              <button
                type="button"
                className="ema-btn-secondary h-9"
                disabled={currentPage <= 1}
                onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
              >
                Previous
              </button>

              <span className="rounded-md border border-line bg-surface px-3 py-2 text-sm font-semibold text-ink">
                Page {currentPage} of {totalPages}
              </span>

              <button
                type="button"
                className="ema-btn-secondary h-9"
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
              >
                Next
              </button>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-line text-sm">
              <thead className="bg-surface-2 text-left text-xs font-semibold uppercase tracking-wide text-muted">
                <tr>
                  {activeTab === "mapping" && (
                    <th className="px-4 py-3">
                      <input
                        type="checkbox"
                        checked={
                          paginatedRequirements.length > 0 &&
                          paginatedRequirements.every((row) =>
                            selectedRequirementIds.has(row.requirement_id),
                          )
                        }
                        onChange={toggleAllVisible}
                        aria-label="Select all visible requirements"
                      />
                    </th>
                  )}
                  <th className="px-4 py-3">Requirement ID</th>
                  <th className="px-4 py-3">Source</th>
                  <th className="px-4 py-3">Discipline</th>
                  <th className="px-4 py-3">Milestone</th>
                  <th className="px-4 py-3">Requirement</th>
                  <th className="px-4 py-3">Category</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Evidence</th>
                  <th className="px-4 py-3 text-right">Action</th>
                </tr>
              </thead>

              <tbody className="divide-y divide-line">
                {paginatedRequirements.map((requirement) => {
                  const selected = selectedRequirementIds.has(requirement.requirement_id);

                  return (
                    <tr
                      key={requirement.requirement_id}
                      className="cursor-pointer hover:bg-surface-2"
                      onClick={() => setSelectedRequirement(requirement)}
                    >
                      {activeTab === "mapping" && (
                        <td className="px-4 py-3" onClick={(event) => event.stopPropagation()}>
                          <input
                            type="checkbox"
                            checked={selected}
                            onChange={() => toggleRequirement(requirement.requirement_id)}
                            aria-label={`Select REQ-${requirement.requirement_id}`}
                          />
                        </td>
                      )}

                      <td className="px-4 py-3 font-semibold text-accent">
                        REQ-{requirement.requirement_id}
                      </td>

                      <td className="px-4 py-3 text-muted">
                        {requirement.source || "Owner Requirements"}
                      </td>

                      <td className="px-4 py-3 text-muted">{requirement.discipline}</td>

                      <td className="px-4 py-3">
                        <span className="ema-chip ema-chip-accent text-xs">
                          {requirement.milestone || "Unassigned"}
                        </span>
                      </td>

                      <td className="max-w-2xl px-4 py-3 text-ink">
                        {requirement.requirement_text}
                      </td>

                      <td className="px-4 py-3 text-muted">{requirement.category || "-"}</td>

                      <td className="px-4 py-3">
                        <StatusBadge value={formatOwnerStatusFromRow(requirement)} />
                      </td>

                      <td className="px-4 py-3">
                        <StatusBadge value={formatRequirementEvidenceBadge(requirement)} />
                      </td>

                      <td className="px-4 py-3 text-right text-accent">
                        <ArrowRight className="ml-auto h-4 w-4" aria-hidden />
                      </td>
                    </tr>
                  );
                })}

                {visibleRequirements.length === 0 && (
                  <tr>
                    <td className="px-4 py-6 text-sm text-muted" colSpan={activeTab === "mapping" ? 10 : 9}>
                      {emptyRequirementMessage(projectRequirements?.state)}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-line px-5 py-4">
            <div className="text-sm text-muted">
              Showing{" "}
              <span className="font-semibold text-ink">
                {visibleRequirements.length === 0
                  ? 0
                  : (currentPage - 1) * rowsPerPage + 1}
              </span>
              {" "}to{" "}
              <span className="font-semibold text-ink">
                {Math.min(currentPage * rowsPerPage, visibleRequirements.length)}
              </span>
              {" "}of{" "}
              <span className="font-semibold text-ink">
                {visibleRequirements.length}
              </span>
              {" "}requirements
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                className="ema-btn-secondary h-9"
                disabled={currentPage <= 1}
                onClick={() => setCurrentPage(1)}
              >
                First
              </button>

              <button
                type="button"
                className="ema-btn-secondary h-9"
                disabled={currentPage <= 1}
                onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
              >
                Previous
              </button>

              <span className="rounded-md border border-line bg-surface px-3 py-2 text-sm font-semibold text-ink">
                Page {currentPage} of {totalPages}
              </span>

              <button
                type="button"
                className="ema-btn-secondary h-9"
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
              >
                Next
              </button>

              <button
                type="button"
                className="ema-btn-secondary h-9"
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage(totalPages)}
              >
                Last
              </button>
            </div>
          </div>
        </section>
      </section>

      <RequirementDrawer
        requirement={selectedRequirement}
        relatedIssue={relatedIssue}
        projectId={projectId}
        onClose={() => setSelectedRequirement(null)}
        onAction={onToast}
        onRefreshProjectData={onRefreshProjectData}
      />
    </>
  );
}

function Summary({ label, value }: { label: string; value: number }) {
  return (
    <div className="ema-card p-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">
        {label}
      </div>
      <div className="mt-1 text-xl font-semibold text-ink">{value}</div>
    </div>
  );
}

function FilterSelect({
  label,
  value,
  onChange,
  options,
  allLabel,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: string[];
  allLabel: string;
}) {
  return (
    <label className="text-xs font-semibold uppercase tracking-wide text-muted">
      {label}
      <select
        className="ema-select mt-1 h-9 w-full px-3 text-sm normal-case"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="all">{allLabel}</option>
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </label>
  );
}

function StatusMiniCard({
  label,
  value,
  icon: Icon,
  active,
  onClick,
}: {
  label: string;
  value: number;
  icon: typeof Clock;
  active?: boolean;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`ema-card flex items-center justify-between gap-3 p-3 text-left transition hover:-translate-y-0.5 hover:shadow-md ${
        active ? "border-accent/40 bg-accent/[0.05]" : ""
      }`}
    >
      <div>
        <div className="text-xs font-semibold uppercase tracking-wide text-muted">
          {label}
        </div>
        <div className="mt-1 text-lg font-semibold text-ink">{value}</div>
      </div>
      <Icon size={18} className={active ? "text-accent" : "text-muted"} aria-hidden />
    </button>
  );
}

function emptyRequirementMessage(state?: string) {
  if (state === "no_client_linked") {
    return "No client linked. Owner requirements cannot be evaluated. Bind the project to a client in Project Setup, then run Preview Import from Data Sync.";
  }

  if (state === "client_linked_no_requirements") {
    return "Client is linked, but no owner requirements have been loaded. Add XLSX files in Data Sync, build the file list, and import data.";
  }

  if (state === "filtered_empty") {
    return "Requirements loaded, but no rows match the current filters. Try adjusting search or filters.";
  }

  if (state === "requirements_loaded") {
    return "Requirements loaded, but no rows match the current filters.";
  }

  return "No requirements found for this project. Load owner requirements from Data Sync.";
}

export function RequirementDrawer({
  requirement,
  relatedIssue,
  projectId,
  onClose,
  onAction,
  onRefreshProjectData,
}: {
  requirement: ProjectRequirementRow | null;
  relatedIssue?: Issue;
  projectId?: number;
  onClose: () => void;
  onAction?: (message: string, tone?: "success" | "info" | "warning") => void;
  onRefreshProjectData?: () => Promise<void> | void;
}) {
  const [evidenceRows, setEvidenceRows] = useState<RequirementEvidence[]>([]);
  const [mappingMilestone, setMappingMilestone] = useState("");
  const [mappingDiscipline, setMappingDiscipline] = useState("");
  const [isActionable, setIsActionable] = useState(true);
  const [complianceStatus, setComplianceStatus] = useState<
    "compliant" | "non_compliant" | "not_evaluated" | "not_applicable" | "needs_review"
  >("not_evaluated");
  const [reviewNote, setReviewNote] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setReviewNote("");
    setMappingMilestone(requirement?.milestone || "Unassigned");
    setMappingDiscipline(requirement?.discipline || "");
    setIsActionable(requirement?.is_actionable ?? true);
    setComplianceStatus(
      requirement?.readiness_status === "compliant"
        ? "compliant"
        : requirement?.readiness_status === "non_compliant"
          ? "non_compliant"
          : requirement?.readiness_status === "needs_review"
            ? "needs_review"
            : requirement?.readiness_status === "not_applicable"
              ? "not_applicable"
              : "not_evaluated",
    );

    if (!requirement || !projectId) {
      setEvidenceRows([]);
      setBusy(false);
      return;
    }

    setBusy(true);

    api
      .listRequirementEvidence(projectId, requirement.requirement_id)
      .then((rows) => setEvidenceRows(rows))
      .catch(() => setEvidenceRows([]))
      .finally(() => setBusy(false));
  }, [projectId, requirement?.requirement_id]);

  const expectedEvidence = requirement ? evidenceFor(requirement.discipline) : "-";
  const relatedSheet = requirement ? sheetFor(requirement.discipline) : "-";

  const latestEvidence = useMemo(
    () =>
      [...evidenceRows].sort((a, b) => {
        const aTime =
          parseDateValue(a.updated_at || a.reviewed_at || a.created_at)?.getTime() ?? 0;
        const bTime =
          parseDateValue(b.updated_at || b.reviewed_at || b.created_at)?.getTime() ?? 0;

        return bTime - aTime;
      })[0] ?? null,
    [evidenceRows],
  );

  const hasEvidence = evidenceRows.length > 0;
  const currentReviewStatus = latestEvidence?.review_status ?? "none";
  const evidenceSummary = formatEvidenceReviewLabel(currentReviewStatus);
  const recommendedAction = getRecommendedAction({
    hasEvidence,
    currentReviewStatus,
    relatedIssue,
  });

  const saveMapping = async () => {
    if (!requirement || !projectId) {
      onAction?.("Project context is unavailable for milestone mapping.", "warning");
      return;
    }

    setBusy(true);
    try {
      await api.updateRequirementMapping(projectId, requirement.requirement_id, {
        milestone: mappingMilestone,
        discipline: mappingDiscipline || requirement.discipline,
        is_actionable: isActionable,
        notes: reviewNote.trim() || undefined,
      });
      onAction?.("Requirement mapping saved.", "success");
      await onRefreshProjectData?.();
    } catch {
      onAction?.("Milestone mapping update failed.", "warning");
    } finally {
      setBusy(false);
    }
  };

  const saveCompliance = async (
    status: "compliant" | "non_compliant" | "needs_review" | "not_applicable",
  ) => {
    if (!requirement || !projectId) {
      onAction?.("Project context is unavailable for compliance review.", "warning");
      return;
    }

    setBusy(true);
    try {
      await api.updateRequirementCompliance(projectId, requirement.requirement_id, {
        status,
        evidence: {
          milestone: mappingMilestone,
          review_source: "owner_requirements_drawer",
        },
        notes: reviewNote.trim() || undefined,
        evaluated_by: "local_demo",
      });
      setComplianceStatus(status);
      onAction?.(
        status === "compliant"
          ? "Marked Approved / Covered."
          : status === "non_compliant"
            ? "Marked Not Approved / Non-compliant."
            : status === "needs_review"
              ? "Marked Needs Review."
              : "Marked Not Applicable.",
        "success",
      );
      await onRefreshProjectData?.();
    } catch {
      onAction?.("Compliance update failed.", "warning");
    } finally {
      setBusy(false);
    }
  };

  const submitReview = async (
    review_status: "candidate" | "accepted" | "rejected" | "needs_review",
  ) => {
    if (!requirement || !projectId) {
      onAction?.("Project context is unavailable for evidence review.", "warning");
      return;
    }

    if (review_status === "accepted" && !hasEvidence) {
      onAction?.("Add or mark evidence as candidate before accepting this requirement.", "warning");
      return;
    }

    setBusy(true);

    try {
      const payload = {
        review_status,
        evidence_type: "manual" as const,
        source_ref: latestEvidence?.source_ref || `manual:req:${requirement.requirement_id}`,
        source_label: latestEvidence?.source_label || `REQ-${requirement.requirement_id} Manual Review`,
        review_note: reviewNote.trim() || undefined,
      };

      const saved = latestEvidence
        ? await api.updateRequirementEvidence(projectId, latestEvidence.id, payload)
        : await api.createRequirementEvidence(projectId, requirement.requirement_id, payload);

      setEvidenceRows((rows) => [saved, ...rows.filter((row) => row.id !== saved.id)]);

      onAction?.(
        `Evidence marked ${formatEvidenceReviewLabel(saved.review_status)}.`,
        "success",
      );
      await onRefreshProjectData?.();
    } catch {
      onAction?.("Evidence review update failed.", "warning");
    } finally {
      setBusy(false);
    }
  };

  return (
    <DetailDrawer
      title="Requirement Detail"
      subtitle={requirement ? `REQ-${requirement.requirement_id}` : undefined}
      isOpen={Boolean(requirement)}
      onClose={onClose}
    >
      {requirement && (
        <div className="space-y-5">
          <div className="ema-card p-4">
            <div className="mb-3 flex items-center justify-between gap-3">
              <div className="flex items-center gap-2 text-sm font-semibold text-ink">
                <ClipboardCheck className="h-4 w-4 text-accent" aria-hidden />
                Owner Requirement
              </div>

              <StatusBadge value={formatComplianceStatusLabel(complianceStatus)} />
            </div>

            <p className="text-sm leading-6 text-muted">
              {requirement.requirement_text}
            </p>
          </div>

          <DetailGrid>
            <DetailItem label="Source" value={requirement.source || "Owner Requirements"} />
            <DetailItem label="Discipline" value={requirement.discipline} />
            <DetailItem label="Milestone" value={mappingMilestone} />
            <DetailItem label="Requirement Status" value={formatOwnerStatusFromRow(requirement)} />
            <DetailItem label="Expected Evidence" value={expectedEvidence} />
            <DetailItem label="Related Sheet" value={relatedSheet} />
            <DetailItem label="Source Type" value={requirement.source_type} />
            <DetailItem label="Readiness Status" value={requirement.readiness_status} />
            <DetailItem label="Current Compliance" value={complianceStatus} />
            <DetailItem label="Evidence Status" value={evidenceSummary} />
          </DetailGrid>

          <div className="ema-card p-4">
            <h3 className="text-sm font-semibold text-ink">Milestone Mapping</h3>
            <p className="mt-1 text-xs text-muted">
              Save milestone, discipline, and actionability back to PostgreSQL before refreshing the tracker.
            </p>

            <div className="mt-4 grid gap-3 md:grid-cols-2">
              <label className="text-xs font-semibold uppercase tracking-wide text-muted">
                Milestone
                <select
                  className="ema-select mt-1 h-10 w-full px-3 text-sm normal-case"
                  value={mappingMilestone}
                  onChange={(event) => setMappingMilestone(event.target.value)}
                >
                  {["Unassigned", "DD 50%", "DD 75%", "DD 95%", "CD 50%", "CD 75%"].map((milestone) => (
                    <option key={milestone} value={milestone}>
                      {milestone}
                    </option>
                  ))}
                </select>
              </label>

              <label className="text-xs font-semibold uppercase tracking-wide text-muted">
                Discipline
                <input
                  className="ema-input mt-1 h-10 w-full px-3 text-sm"
                  value={mappingDiscipline}
                  onChange={(event) => setMappingDiscipline(event.target.value)}
                />
              </label>
            </div>

            <label className="mt-3 inline-flex items-center gap-2 text-sm text-muted">
              <input
                type="checkbox"
                checked={isActionable}
                onChange={(event) => setIsActionable(event.target.checked)}
              />
              Actionable requirement
            </label>

            <div className="mt-3 flex flex-wrap gap-2">
              <button
                className="ema-btn-secondary h-9 disabled:opacity-60"
                type="button"
                disabled={busy}
                onClick={saveMapping}
              >
                Save Mapping
              </button>
            </div>
          </div>

          <div className="ema-card p-4">
            <h3 className="text-sm font-semibold text-ink">Compliance Review</h3>
            <p className="mt-1 text-xs text-muted">
              Compliance is deterministic and saved in the backend. It drives readiness calculations.
            </p>

            <div className="mt-3 flex flex-wrap gap-2">
              <button
                className="ema-btn-secondary border-accent text-sm font-semibold text-accent disabled:opacity-60"
                type="button"
                disabled={busy}
                onClick={() => saveCompliance("compliant")}
              >
                Mark Approved
              </button>
              <button
                className="ema-btn-secondary border-danger text-sm font-semibold text-danger disabled:opacity-60"
                type="button"
                disabled={busy}
                onClick={() => saveCompliance("non_compliant")}
              >
                Mark Not Approved
              </button>
              <button
                className="ema-btn-secondary border-warning text-sm font-semibold text-warning disabled:opacity-60"
                type="button"
                disabled={busy}
                onClick={() => saveCompliance("needs_review")}
              >
                Mark Needs Review
              </button>
              <button
                className="ema-btn-secondary text-sm font-semibold disabled:opacity-60"
                type="button"
                disabled={busy}
                onClick={() => saveCompliance("not_applicable")}
              >
                Mark Not Applicable
              </button>
            </div>
          </div>

          <div className="ema-card p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h3 className="text-sm font-semibold text-ink">Evidence Review</h3>
                <p className="mt-1 text-xs text-muted">
                  Accepted evidence means this requirement is covered for readiness tracking.
                  It does not represent official code or compliance approval.
                  Candidate evidence must be reviewed before it contributes to readiness.
                </p>
              </div>

              {hasEvidence ? (
                <StatusBadge value={`${evidenceRows.length} evidence link(s)`} />
              ) : (
                <StatusBadge value="No evidence" />
              )}
            </div>

            {!hasEvidence && (
              <div className="ema-notice-warning mt-4 p-3 text-xs">
                No evidence has been linked yet. Attach model, sheet, specification, or manual review evidence before accepting this requirement.
              </div>
            )}

            <label className="mt-4 block text-xs font-semibold uppercase tracking-wide text-muted">
              Review note
            </label>

            <textarea
              className="ema-input mt-2 min-h-20 w-full px-3 py-2 text-sm"
              placeholder="Optional reviewer note"
              value={reviewNote}
              onChange={(event) => setReviewNote(event.target.value)}
            />

            <div className="mt-3 flex flex-wrap gap-2">
              <button
                className="ema-btn-secondary inline-flex items-center gap-2 text-sm font-semibold disabled:opacity-60"
                type="button"
                disabled={busy}
                onClick={() => submitReview("candidate")}
              >
                <PlusCircle size={15} />
                {hasEvidence ? "Evidence Candidate" : "Add Manual Evidence"}
              </button>

              {hasEvidence && (
                <button
                  className="ema-btn-secondary border-accent text-sm font-semibold text-accent disabled:opacity-60"
                  type="button"
                  disabled={busy}
                  onClick={() => submitReview("accepted")}
                >
                  Accept Evidence
                </button>
              )}

              <button
                className="ema-btn-secondary border-warning text-sm font-semibold text-warning disabled:opacity-60"
                type="button"
                disabled={busy}
                onClick={() => submitReview("needs_review")}
              >
                Needs Review
              </button>

              {hasEvidence && (
                <button
                  className="ema-btn-secondary border-danger text-sm font-semibold text-danger disabled:opacity-60"
                  type="button"
                  disabled={busy}
                  onClick={() => submitReview("rejected")}
                >
                  Reject
                </button>
              )}
            </div>

            <div className="mt-4 space-y-2">
              {busy ? (
                <p className="text-sm text-muted">Updating evidence review...</p>
              ) : evidenceRows.length === 0 ? (
                <p className="text-sm text-muted">No evidence links saved yet.</p>
              ) : (
                evidenceRows.map((row) => (
                  <div key={row.id} className="ema-card p-3 text-sm">
                    <div className="flex flex-wrap items-center gap-2">
                      <StatusBadge value={formatEvidenceReviewLabel(row.review_status)} />
                      <span className="font-semibold text-ink">
                        {row.source_label || row.source_ref || `Evidence ${row.id}`}
                      </span>
                    </div>

                    <div className="mt-2 grid gap-2 text-xs text-muted md:grid-cols-2">
                      <div>Type: {row.evidence_type}</div>
                      <div>Evidence status: {row.evidence_status}</div>
                      {row.confidence != null && (
                        <div>Confidence: {Math.round(Number(row.confidence) * 100)}%</div>
                      )}
                      <div>Reviewed by: {row.reviewed_by || "-"}</div>
                      <div>
                        Reviewed at:{" "}
                        {row.reviewed_at ? formatDateTime(row.reviewed_at) : "-"}
                      </div>
                    </div>

                    {row.evidence_type === "model" && row.metadata_json && (
                      <ModelEvidenceDetail meta={row.metadata_json} />
                    )}

                    {row.review_note ? (
                      <p className="mt-2 text-xs text-muted">{row.review_note}</p>
                    ) : null}
                  </div>
                ))
              )}
            </div>
          </div>

          <div className="ema-card p-4">
            <h3 className="text-sm font-semibold text-ink">Related Gap / Issue</h3>

            {relatedIssue ? (
              <div className="mt-3 space-y-2 text-sm text-muted">
                <div className="flex items-center gap-2">
                  <StatusBadge value={relatedIssue.severity} />
                  <span>{relatedIssue.rule_code || `Issue #${relatedIssue.id}`}</span>
                </div>

                <p>{relatedIssue.message || relatedIssue.issue_type}</p>

                <p className="text-xs text-muted">
                  Related issues are possible gaps. They are not evidence by themselves.
                </p>
              </div>
            ) : (
              <p className="mt-2 text-sm text-muted">
                No direct issue mapping exists yet. Use discipline and evidence review for triage.
              </p>
            )}
          </div>

          <div className="ema-notice-info p-4">
            <h3 className="text-sm font-semibold text-ink">Recommended Action</h3>
            <p className="mt-2 text-sm text-muted">{recommendedAction}</p>
          </div>
        </div>
      )}
    </DetailDrawer>
  );
}

function ModelEvidenceDetail({ meta }: { meta: Record<string, unknown> }) {
  const str = (key: string) => {
    const v = meta[key];
    return typeof v === "string" && v ? v : undefined;
  };

  const matchedParams = meta.matched_params;
  const paramEntries =
    matchedParams && typeof matchedParams === "object" && !Array.isArray(matchedParams)
      ? Object.entries(matchedParams as Record<string, unknown>)
          .filter(([, v]) => v != null && v !== "")
          .map(([k, v]) => `${k}: ${String(v)}`)
      : [];

  const fields = [
    ["Category", str("category")],
    ["Name", str("name")],
    ["Family", str("family")],
    ["Type", str("type")],
    ["Level", str("level")],
    ["Intent", str("matched_intent")],
  ].filter(([, v]) => v != null) as [string, string][];

  if (fields.length === 0 && paramEntries.length === 0) return null;

  return (
    <div className="mt-3 rounded-xl border border-line bg-surface-2 p-3 text-xs">
      <div className="mb-2 font-semibold uppercase tracking-wide text-muted">Model Element</div>
      <div className="grid gap-1 md:grid-cols-2">
        {fields.map(([label, value]) => (
          <div key={label}>
            <span className="text-muted">{label}:</span> {value}
          </div>
        ))}
      </div>
      {paramEntries.length > 0 && (
        <div className="mt-2">
          <span className="font-semibold text-muted">Matched params: </span>
          {paramEntries.join(" · ")}
        </div>
      )}
    </div>
  );
}

function getRequirementCounts(rows: ProjectRequirementRow[]) {
  return rows.reduce(
    (counts, row) => {
      const key = getEvidenceFilterKey(row);

      if (key === "no_evidence") counts.noEvidence += 1;
      if (key === "candidate") counts.candidate += 1;
      if (key === "needs_review") counts.needsReview += 1;
      if (key === "accepted") counts.accepted += 1;
      if (key === "rejected") counts.rejected += 1;

      return counts;
    },
    {
      noEvidence: 0,
      candidate: 0,
      needsReview: 0,
      accepted: 0,
      rejected: 0,
    },
  );
}

function getEvidenceFilterKey(row: ProjectRequirementRow): EvidenceFilter {
  // Prefer normalized_status from backend (source of truth) when available
  const normalized = (row as { normalized_status?: string }).normalized_status;
  if (normalized === "not_applicable") return "no_evidence";
  if (normalized === "accepted") return "accepted";
  if (normalized === "candidate") return "candidate";
  if (normalized === "needs_review") return "needs_review";
  if (normalized === "rejected") return "rejected";
  if (normalized === "no_evidence") return "no_evidence";

  // Fallback: derive from evidence_review_status
  const review = row.evidence_review_status || "none";
  if (review === "accepted") return "accepted";
  if (review === "candidate") return "candidate";
  if (review === "needs_review") return "needs_review";
  if (review === "rejected") return "rejected";

  return "no_evidence";
}

function formatOwnerStatusFromRow(requirement: ProjectRequirementRow) {
  return requirement.owner_status?.replace(/_/g, " ") || "Not started";
}

function formatComplianceStatusLabel(status: string) {
  return status.replace(/_/g, " ").replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function evidenceFor(_discipline: string) {
  return "Model, sheet, specification, or manual review";
}

function sheetFor(_discipline: string) {
  return "Sheet evidence pending";
}

function formatRequirementEvidenceBadge(requirement: ProjectRequirementRow) {
  const review = requirement.evidence_review_status || "none";

  if (review === "accepted") return "Accepted Evidence";
  if (review === "candidate") return "Evidence Candidate";
  if (review === "needs_review") return "Needs Review";
  if (review === "rejected") return "Rejected";

  if (requirement.evidence_status === "compliant") return "Covered";
  if (requirement.evidence_status === "not_applicable") return "Not Applicable";
  if (requirement.evidence_status === "missing") return "No Evidence";

  return requirement.owner_status?.replace(/_/g, " ") || "No Evidence";
}

function formatEvidenceReviewLabel(status: string) {
  if (status === "candidate") return "Evidence Candidate";
  if (status === "accepted") return "Accepted Evidence";
  if (status === "rejected") return "Rejected";
  if (status === "needs_review") return "Needs Review";
  return "No Evidence";
}

function getRecommendedAction({
  hasEvidence,
  currentReviewStatus,
  relatedIssue,
}: {
  hasEvidence: boolean;
  currentReviewStatus: string;
  relatedIssue?: Issue;
}) {
  if (!hasEvidence) {
    return "Add manual evidence or link model, sheet, or specification evidence before marking this requirement covered.";
  }

  if (currentReviewStatus === "candidate") {
    return "Review the candidate evidence and accept it, reject it, or mark it as needs review.";
  }

  if (currentReviewStatus === "needs_review") {
    return "Add a reviewer note and follow up with the responsible discipline lead.";
  }

  if (currentReviewStatus === "accepted") {
    return "Requirement is covered for readiness tracking. No further action is required unless evidence changes.";
  }

  if (currentReviewStatus === "rejected") {
    return "Rejected evidence does not count toward readiness. Add or find another evidence source.";
  }

  if (relatedIssue) {
    return "Review the related gap or issue and decide whether it blocks requirement coverage.";
  }

  return "Review evidence and update the requirement status.";
}
