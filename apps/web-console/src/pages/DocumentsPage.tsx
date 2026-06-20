import { FileText, Search } from "lucide-react";
import { useMemo, useState } from "react";
import { api } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import { formatFileSize } from "../lib/format";
import type { LandingDocument, ProjectSummary } from "../types";

type TabKey = "all" | "revit_export" | "revit_meta" | "drawing" | "owner_requirements" | "specification" | "unknown";

const TABS: Array<{ key: TabKey; label: string }> = [
  { key: "all", label: "All" },
  { key: "revit_export", label: "Revit Exports" },
  { key: "revit_meta", label: "Revit Sidecars" },
  { key: "drawing", label: "Drawings" },
  { key: "owner_requirements", label: "Owner Requirements" },
  { key: "specification", label: "Specifications" },
  { key: "unknown", label: "Unknown" },
];

export function DocumentsPage({
  project,
  documents,
}: {
  project?: ProjectSummary;
  documents: LandingDocument[];
}) {
  const [activeTab, setActiveTab] = useState<TabKey>("all");
  const [search, setSearch] = useState("");
  const [disciplineFilter, setDisciplineFilter] = useState("all");
  const [parserFilter, setParserFilter] = useState("all");
  const [evidenceFilter, setEvidenceFilter] = useState("all");
  const [activeDoc, setActiveDoc] = useState<LandingDocument | null>(null);
  const [textPreview, setTextPreview] = useState("");
  const [previewMessage, setPreviewMessage] = useState("");

  const disciplines = useMemo(
    () => Array.from(new Set(documents.map((d) => d.discipline || "Unknown"))).sort(),
    [documents],
  );

  const filtered = useMemo(() => {
    let list = documents;

    if (activeTab === "revit_export") list = list.filter((d) => d.file_type === "revit_export" || d.document_category === "revit_export");
    else if (activeTab === "revit_meta") list = list.filter((d) => d.file_type === "revit_meta_json" || d.file_ext === ".meta.json" || d.file_name.endsWith(".meta.json"));
    else if (activeTab === "drawing") list = list.filter((d) => d.document_category === "drawing" || d.file_type === "drawing_pdf");
    else if (activeTab === "owner_requirements") list = list.filter((d) => d.document_category === "owner_requirements" || d.file_type === "owner_requirements");
    else if (activeTab === "specification") list = list.filter((d) => d.document_category === "specification" || d.file_type === "specification_pdf");
    else if (activeTab === "unknown") list = list.filter((d) => d.document_category === "unknown" || d.file_type === "unknown");

    if (disciplineFilter !== "all") list = list.filter((d) => (d.discipline || "Unknown") === disciplineFilter);
    if (parserFilter !== "all") list = list.filter((d) => {
      const status = (d.metadata_json?.parser_status as string) || d.ingestion_status || "indexed";
      return parserFilter === "unparsed" ? status === "indexed" : status === parserFilter;
    });
    if (evidenceFilter !== "all") list = list.filter((d) => d.evidence_status === evidenceFilter);

    const query = search.trim().toLowerCase();
    if (query) {
      list = list.filter((d) =>
        `${d.file_name} ${d.relative_path} ${d.discipline || ""} ${d.document_category || ""}`
          .toLowerCase().includes(query),
      );
    }
    return list;
  }, [documents, activeTab, disciplineFilter, parserFilter, evidenceFilter, search]);

  const counts = useMemo(() => {
    const byCat: Record<string, number> = {};
    for (const d of documents) {
      const cat = d.document_category || d.file_type || "unknown";
      byCat[cat] = (byCat[cat] || 0) + 1;
    }
    return byCat;
  }, [documents]);

  const handlePreview = (doc: LandingDocument) => {
    setActiveDoc(doc);
    if (!project?.id) {
      setPreviewMessage("Project context unavailable.");
      setTextPreview("");
      return;
    }
    api
      .getProjectDocumentText(project.id, doc.id)
      .then((result) => {
        setTextPreview(result.text_preview || "");
        setPreviewMessage(result.available ? "Text preview available." : result.message || "Text extraction unavailable.");
      })
      .catch(() => {
        setTextPreview("");
        setPreviewMessage("Text extraction unavailable.");
      });
  };

  const isSidecar = (doc: LandingDocument) =>
    doc.file_name.endsWith(".meta.json") || doc.file_type === "revit_meta_json";

  const isOwnerReq = (doc: LandingDocument) =>
    doc.document_category === "owner_requirements" || doc.file_type === "owner_requirements";

  const evidenceLabel = (doc: LandingDocument) => {
    if (isSidecar(doc)) {
      return "N/A (metadata)";
    }
    if (doc.evidence_status === "official") {
      return "Official evidence";
    }
    if (doc.evidence_status === "candidate") {
      return "Evidence candidate";
    }
    return "Indexed / not reviewed";
  };

  const requiresClientBinding = activeTab === "owner_requirements" && !project?.client_id;

  return (
    <div className="ema-page ema-page-shell space-y-5">
      {/* Header */}
      <div className="ema-card p-5">
        <h2 className="text-lg font-semibold text-ink">
          Documents for {project?.project_name || project?.project_title || "Project"}
        </h2>
        <p className="mt-1 text-sm text-muted">
          Indexed files are evidence candidates unless an official evidence workflow marks them covered.
        </p>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          <Metric label="Total" value={documents.length} />
          <Metric label="Revit Exports" value={counts.revit_export || 0} />
          <Metric label="Sidecars" value={counts.revit_meta_json || 0} />
          <Metric label="Specifications" value={counts.specification_pdf || counts.specification || 0} />
          <Metric label="Drawings" value={counts.drawing_pdf || counts.drawing || 0} />
        </div>
      </div>

      {/* Tabs */}
      <div className="flex flex-wrap gap-1 border-b border-line">
        {TABS.map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => setActiveTab(tab.key)}
            className={`px-4 py-2 text-sm font-semibold transition ${
              activeTab === tab.key
                ? "border-b-2 border-accent text-accent"
                : "text-muted hover:text-ink"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Filters */}
      <div className="ema-filter-row">
        <div className="ema-search-shell w-52">
          <Search size={15} className="text-muted" />
          <input
            placeholder="Search files..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <select
          className="ema-select h-9 px-2"
          value={disciplineFilter}
          onChange={(e) => setDisciplineFilter(e.target.value)}
        >
          <option value="all">All disciplines</option>
          {disciplines.map((d) => <option key={d} value={d}>{d}</option>)}
        </select>
        <select
          className="ema-select h-9 px-2"
          value={parserFilter}
          onChange={(e) => setParserFilter(e.target.value)}
        >
          <option value="all">All parser status</option>
          <option value="indexed">Indexed</option>
          <option value="parsed">Parsed</option>
          <option value="unparsed">Unparsed</option>
          <option value="failed">Failed</option>
        </select>
        <select
          className="ema-select h-9 px-2"
          value={evidenceFilter}
          onChange={(e) => setEvidenceFilter(e.target.value)}
        >
          <option value="all">All evidence</option>
          <option value="candidate">Candidate</option>
          <option value="official">Official</option>
        </select>
        <span className="text-xs text-muted">{filtered.length} of {documents.length} shown</span>
      </div>

      {/* Table */}
      <div className="ema-card" data-no-glass>
        <table className="min-w-full divide-y divide-line text-sm">
          <thead className="bg-surface-2 text-left text-xs font-semibold text-muted uppercase tracking-wide">
            <tr>
              <th className="px-4 py-3">Filename</th>
              <th className="px-4 py-3">Category</th>
              <th className="px-4 py-3">Discipline</th>
              <th className="px-4 py-3">Project</th>
              <th className="px-4 py-3">Pages</th>
              <th className="px-4 py-3">Size</th>
              <th className="px-4 py-3">Checksum</th>
              <th className="px-4 py-3">Parser</th>
              <th className="px-4 py-3">Evidence</th>
              <th className="px-4 py-3">Action</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {filtered.length === 0 ? (
              <tr>
                <td className="px-4 py-8 text-center text-sm text-muted" colSpan={10}>
                  {search || disciplineFilter !== "all" || parserFilter !== "all" || evidenceFilter !== "all"
                    ? "No documents match the current filters."
                    : activeTab === "drawing"
                      ? "No drawing sheets indexed yet. Add drawing PDFs under landing/Drawings, rebuild manifest, and run ingest."
                      : activeTab === "owner_requirements"
                        ? "No owner requirement files indexed. Add XLSX files under landing/Owner Requirements, rebuild manifest, and run ingest."
                        : activeTab === "revit_meta"
                          ? "No Revit sidecar metadata files indexed."
                          : activeTab === "revit_export"
                            ? "No Revit export files indexed. Export model from Revit ribbon or place JSON under Revit Exports."
                            : "No documents indexed yet."}
                </td>
              </tr>
            ) : (
              filtered.map((doc) => (
                <tr key={doc.id} className="hover:bg-surface-2">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <FileText size={14} className="shrink-0 text-muted" />
                      <div>
                        <div className="font-medium text-ink">{doc.file_name}</div>
                        <div className="text-[11px] text-muted font-mono truncate max-w-[300px]">
                          {doc.relative_path}
                        </div>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-muted">
                    {isSidecar(doc) ? (
                      <span className="text-warning">Metadata — not evidence</span>
                    ) : requiresClientBinding ? (
                      <span className="text-warning">Source File / Requires Client Binding</span>
                    ) : (
                      doc.document_category || doc.file_type || "-"
                    )}
                  </td>
                  <td className="px-4 py-3 text-muted">{doc.discipline || "-"}</td>
                  <td className="px-4 py-3 text-muted">{doc.project_folder || "-"}</td>
                  <td className="px-4 py-3 text-muted">{doc.page_count ?? "-"}</td>
                  <td className="px-4 py-3 text-muted font-mono text-xs">
                    {doc.file_size_bytes != null ? formatFileSize(doc.file_size_bytes) : "-"}
                  </td>
                  <td className="px-4 py-3 text-muted font-mono text-[10px]">
                    {doc.checksum_sha256 ? doc.checksum_sha256.slice(0, 12) + "…" : "-"}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge value={
                      isSidecar(doc) ? "meta" :
                      (doc.metadata_json?.parser_status as string) || doc.ingestion_status || "indexed"
                    } />
                  </td>
                  <td className="px-4 py-3">
                    {isSidecar(doc) ? (
                      <span className="text-[11px] text-muted">N/A</span>
                    ) : (
                      <span className="ema-chip ema-chip-warning text-[11px]">
                        {evidenceLabel(doc)}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {!isSidecar(doc) && (
                      <button
                        type="button"
                        className="ema-btn-ghost text-xs disabled:opacity-50"
                        disabled={!project?.id}
                        onClick={() => handlePreview(doc)}
                      >
                        Preview
                      </button>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Preview Panel */}
      {activeDoc && project?.id ? (
        <div className="ema-card p-4">
          <div className="flex items-center justify-between">
            <h3 className="font-semibold text-ink">Document Preview: {activeDoc.file_name}</h3>
            <button type="button" className="ema-btn-ghost text-sm" onClick={() => setActiveDoc(null)}>
              Close
            </button>
          </div>
          <div className="mt-3 grid gap-4 xl:grid-cols-2">
            <div className="min-h-[420px] ema-card">
              {activeDoc.file_ext.toLowerCase() === ".pdf" ? (
                <iframe
                  className="h-[420px] w-full rounded"
                  src={api.getProjectDocumentPdfUrl(project.id, activeDoc.id)}
                  title={`PDF preview ${activeDoc.file_name}`}
                />
              ) : (
                <div className="p-4 text-sm text-muted">PDF preview unavailable for this document type.</div>
              )}
            </div>
            <div className="space-y-3">
              <div className="ema-card p-3 text-sm">
                <p><strong>Category:</strong> {activeDoc.document_category || activeDoc.file_type}</p>
                <p><strong>Evidence:</strong> {evidenceLabel(activeDoc)}</p>
                <p><strong>Parser:</strong> {String(activeDoc.metadata_json?.parser_status || "indexed")}</p>
                {activeDoc.checksum_sha256 && (
                  <p className="truncate"><strong>SHA256:</strong> {activeDoc.checksum_sha256}</p>
                )}
              </div>
              <div className="ema-card p-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-muted">Text Preview</p>
                <p className="mt-2 text-sm text-muted">{previewMessage || "Loading preview..."}</p>
                <pre className="ema-solid-json-surface mt-3 max-h-64 overflow-auto rounded p-3 text-xs text-ink">
                  {textPreview || "No preview text."}
                </pre>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="ema-card p-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 text-xl font-semibold text-ink">{value}</div>
    </div>
  );
}
