import { FileStack, Search } from "lucide-react";
import { useMemo, useState } from "react";
import { StatusBadge } from "../components/StatusBadge";
import { formatDateTime } from "../lib/format";
import type { LandingDocument, ProjectReadiness, ProjectSummary } from "../types";

type DrawingReelPageProps = {
  project?: ProjectSummary;
  documents: LandingDocument[];
  readiness?: ProjectReadiness | null;
};

export function DrawingReelPage({ project, documents, readiness }: DrawingReelPageProps) {
  const drawings = useMemo(
    () => documents.filter((d) => d.document_category === "drawing" || d.file_type === "drawing_pdf"),
    [documents],
  );
  const specs = useMemo(
    () => documents.filter((d) => d.document_category === "specification" || d.file_type === "specification_pdf"),
    [documents],
  );

  const [search, setSearch] = useState("");
  const [milestoneFilter, setMilestoneFilter] = useState("all");
  const [tradeFilter, setTradeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [revisionFilter, setRevisionFilter] = useState("all");
  const [ownerReqFilter, setOwnerReqFilter] = useState("all");
  const [markupFilter, setMarkupFilter] = useState("all");

  const disciplines = useMemo(
    () => Array.from(new Set(drawings.map((d) => d.discipline || "Unknown"))).sort(),
    [drawings],
  );

  const disciplineCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const d of drawings) {
      const disc = d.discipline || "Unknown";
      counts[disc] = (counts[disc] || 0) + 1;
    }
    return counts;
  }, [drawings]);

  const filtered = useMemo(() => {
    let list = drawings;
    const query = search.trim().toLowerCase();
    if (query) {
      list = list.filter((d) =>
        `${d.file_name} ${d.sheet_title || ""} ${d.discipline || ""}`.toLowerCase().includes(query),
      );
    }
    if (tradeFilter !== "all") list = list.filter((d) => (d.discipline || "Unknown") === tradeFilter);
    return list;
  }, [drawings, search, tradeFilter]);

  const milestones = ["DD50", "DD75", "DD95", "CD50", "CD75", "CD95", "Permit Review", "Submit Package"];
  const evidenceLabel = (sheet: LandingDocument) => {
    if (sheet.evidence_status === "official") {
      return "Official evidence";
    }
    if (sheet.evidence_status === "candidate") {
      return "Evidence candidate";
    }
    return "Indexed / not reviewed";
  };

  return (
    <div className="ema-page ema-page-shell space-y-6">
      <section className="ema-card p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              <FileStack size={20} className="text-accent" />
              <h2 className="text-xl font-semibold text-ink">Drawing Reel and Sheet Tracker</h2>
            </div>
            <p className="mt-1 max-w-3xl text-sm text-muted">
              Review drawing sheets by milestone, trade, revision status, owner requirement coverage, and open markups.
            </p>
            <p className="mt-1 text-xs text-muted">
              Filename inference — not actual sheet number extraction
            </p>
          </div>
          <StatusBadge value={drawings.length ? "Indexed" : "Pending"} />
        </div>
        <div className="mt-5 grid gap-4 md:grid-cols-4">
          <Metric label="Project" value={project?.project_name || project?.project_title || "-"} />
          <Metric label="Current Milestone" value={project?.phase || "Binding pending"} />
          <Metric label="Last Drawing Sync" value={drawings[0]?.indexed_at ? formatDateTime(drawings[0].indexed_at) : "No drawing sync"} />
          <Metric label="Package Readiness" value={readiness ? `${Math.round(readiness.overall_readiness)}% ${readiness.label}` : "Pending"} />
        </div>
      </section>

      {/* Discipline Counts */}
      {drawings.length > 0 && (
        <section className="ema-card p-4">
          <h3 className="text-sm font-semibold text-ink mb-3">Discipline Counts</h3>
          <div className="flex flex-wrap gap-3">
            {disciplines.map((disc) => (
              <span key={disc} className="ema-chip ema-chip-accent">
                {disc}: <strong>{disciplineCounts[disc] || 0}</strong> sheet{(disciplineCounts[disc] || 0) !== 1 ? "s" : ""}
              </span>
            ))}
          </div>
        </section>
      )}

      <section className="ema-card p-5">
        <div className="grid gap-3 md:grid-cols-6">
          <Filter label="Milestone" value={milestoneFilter} onChange={setMilestoneFilter} options={milestones} />
          <Filter label="Trade" value={tradeFilter} onChange={setTradeFilter} options={disciplines.length > 0 ? disciplines : ["Mechanical", "Electrical", "Plumbing", "Technology", "Lighting"]} />
          <Filter label="Sheet Status" value={statusFilter} onChange={setStatusFilter} options={["Indexed", "Missing", "Needs Review"]} />
          <Filter label="Revision" value={revisionFilter} onChange={setRevisionFilter} options={["Current", "Previous", "Unknown"]} />
          <Filter label="Owner Req." value={ownerReqFilter} onChange={setOwnerReqFilter} options={["Candidate", "Official", "Missing"]} />
          <Filter label="Markups" value={markupFilter} onChange={setMarkupFilter} options={["Open", "Closed", "None"]} />
        </div>
      </section>

      {drawings.length === 0 ? (
        <section className="ema-notice-warning p-8 text-center">
          <h3 className="text-base font-semibold text-ink">No drawing sheets indexed yet</h3>
          <p className="mt-2 text-sm text-muted">
            Add drawing PDFs under landing/{"<"}PROJECT{">"}/Drawings, rebuild the manifest, and run ingest. Specifications are indexed separately as evidence candidates.
          </p>
          <p className="mt-3 text-sm font-semibold text-ink">{specs.length} specification PDFs are currently indexed as evidence candidates.</p>
        </section>
      ) : (
        <section className="ema-card" data-no-glass>
          <div className="ema-card-header">
            <h3 className="text-lg font-semibold text-ink">Sheet Index</h3>
            <div className="ema-search-shell w-48">
              <Search size={15} className="text-muted" />
              <input
                placeholder="Search sheets..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-line text-sm">
              <thead className="bg-surface-2 text-left text-xs font-semibold text-muted uppercase">
                <tr>
                  <th className="px-5 py-3">Sheet Number</th>
                  <th className="px-5 py-3">Sheet Title</th>
                  <th className="px-5 py-3">Discipline</th>
                  <th className="px-5 py-3">Milestone</th>
                  <th className="px-5 py-3">Status</th>
                  <th className="px-5 py-3">Requirement Coverage</th>
                  <th className="px-5 py-3">Last Sync</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {filtered.length === 0 ? (
                  <tr>
                    <td className="px-5 py-8 text-center text-sm text-muted" colSpan={7}>
                      No drawings match current filters.
                    </td>
                  </tr>
                ) : (
                  filtered.map((sheet) => (
                    <tr key={sheet.id} className="hover:bg-surface-2">
                      <td className="px-5 py-4 font-semibold text-accent">
                        {sheet.sheet_number || (
                          <span className="text-warning" title="Inferred from filename — not actual sheet extraction">
                            {sheet.file_name.replace(/\.pdf$/i, "").slice(0, 20)}
                          </span>
                        )}
                        {!sheet.sheet_number && (
                          <span className="ml-1 text-[10px] text-muted">(inferred)</span>
                        )}
                      </td>
                      <td className="px-5 py-4 text-ink">{sheet.sheet_title || sheet.file_name}</td>
                      <td className="px-5 py-4 text-muted">{sheet.discipline || "-"}</td>
                      <td className="px-5 py-4 text-muted">{project?.phase || "Unassigned"}</td>
                      <td className="px-5 py-4"><StatusBadge value={evidenceLabel(sheet)} /></td>
                      <td className="px-5 py-4 text-muted">{evidenceLabel(sheet)}</td>
                      <td className="px-5 py-4 text-muted">{formatDateTime(sheet.indexed_at)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="ema-card p-4">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-2 text-sm font-semibold text-ink">{value}</div>
    </div>
  );
}

function Filter({
  label, value, onChange, options,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  options: string[];
}) {
  return (
    <label className="block text-xs font-semibold uppercase tracking-wide text-muted">
      {label}
      <select
        className="ema-select mt-2 h-9 w-full px-2 text-sm normal-case tracking-normal"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      >
        <option value="all">All</option>
        {options.map((opt) => <option key={opt} value={opt}>{opt}</option>)}
      </select>
    </label>
  );
}
