import { useEffect, useMemo, useState } from "react";
import { api } from "../api/client";
import type {
  ComplianceCorpus,
  ComplianceImportResult,
  ComplianceLoaderPreview,
  ComplianceRule,
  ComplianceStatus,
  ProjectSummary,
} from "../types";

export function CompliancePage({ project }: { project?: ProjectSummary }) {
  const [status, setStatus] = useState<ComplianceStatus | null>(null);
  const [corpora, setCorpora] = useState<ComplianceCorpus[]>([]);
  const [rules, setRules] = useState<ComplianceRule[]>([]);
  const [preview, setPreview] = useState<ComplianceLoaderPreview | null>(null);
  const [importResult, setImportResult] = useState<ComplianceImportResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [name, setName] = useState("NEC Candidate Corpus");
  const [blocksPath, setBlocksPath] = useState("");
  const [edgesPath, setEdgesPath] = useState("");
  const [auditPath, setAuditPath] = useState("");
  const [gatesPath, setGatesPath] = useState("");

  const refresh = () => {
    Promise.all([api.getComplianceStatus(), api.listComplianceCorpora(), api.listComplianceRules()])
      .then(([s, c, r]) => {
        setStatus(s);
        setCorpora(c);
        setRules(r);
        setError(null);
      })
      .catch((err: Error) => setError(err.message));
  };

  useEffect(() => {
    refresh();
  }, []);

  const payload = useMemo(
    () => ({
      name,
      code_family: "NEC",
      edition: "2023",
      jurisdiction: "US",
      source_type: "structured_nec",
      blocks_path: blocksPath || undefined,
      edges_path: edgesPath || undefined,
      structure_audit_path: auditPath || undefined,
      gates_path: gatesPath || undefined,
    }),
    [name, blocksPath, edgesPath, auditPath, gatesPath],
  );

  return (
    <section className="ema-page ema-page-shell space-y-5">
      <div className="ema-card p-5">
        <h2 className="text-lg font-semibold text-ink">Compliance Overview</h2>
        <p className="mt-1 text-sm text-muted">
          Candidate corpora and rules only. Compliance remains reviewable until deterministic evidence workflows approve records.
        </p>
        {error ? <p className="mt-3 text-sm text-danger">{error}</p> : null}
        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          <Metric label="Corpora" value={status?.corpora_count ?? 0} />
          <Metric label="Candidate Rules" value={status?.candidate_rules ?? 0} />
          <Metric label="Active Rules" value={status?.active_rules ?? 0} />
          <Metric label="Findings" value={status?.findings_count ?? 0} />
          <Metric label="Project" value={project?.project_name || project?.project_title || "No project"} />
        </div>
      </div>

      <div className="ema-card p-5">
        <h3 className="font-semibold text-ink">NEC Loader (Local Structured Corpus)</h3>
        <p className="mt-1 text-sm text-muted">
          Uses landing-relative paths only. Imported rules remain candidate until reviewed.
        </p>
        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <input className="ema-input h-10 px-3" value={name} onChange={(e) => setName(e.target.value)} placeholder="Corpus name" />
          <input className="ema-input h-10 px-3" value={blocksPath} onChange={(e) => setBlocksPath(e.target.value)} placeholder="blocks.jsonl relative path" />
          <input className="ema-input h-10 px-3" value={edgesPath} onChange={(e) => setEdgesPath(e.target.value)} placeholder="edges.csv relative path" />
          <input className="ema-input h-10 px-3" value={auditPath} onChange={(e) => setAuditPath(e.target.value)} placeholder="structure_audit.json relative path" />
          <input className="ema-input h-10 px-3 md:col-span-2" value={gatesPath} onChange={(e) => setGatesPath(e.target.value)} placeholder="research_grade_gates.json relative path" />
        </div>
        <div className="mt-4 flex gap-3">
          <button
            type="button"
            className="ema-btn-primary disabled:opacity-60"
            disabled={loading}
            onClick={() => {
              setLoading(true);
              api.previewNecCorpus(payload).then(setPreview).catch((err: Error) => setError(err.message)).finally(() => setLoading(false));
            }}
          >
            Preview
          </button>
          <button
            type="button"
            className="ema-btn-secondary disabled:opacity-60"
            disabled={loading}
            onClick={() => {
              setLoading(true);
              api.importNecCorpus(payload).then((result) => {
                setImportResult(result);
                refresh();
              }).catch((err: Error) => setError(err.message)).finally(() => setLoading(false));
            }}
          >
            Import as Candidate
          </button>
        </div>
        {preview ? (
          <div className="ema-notice-warning mt-4">
            Gate: <strong>{preview.gate_status}</strong> | Blocks: {preview.blocks_count} | Edges: {preview.edges_count}
            {preview.warnings.length > 0 ? <p className="mt-1 text-warning">{preview.warnings.join(" | ")}</p> : null}
          </div>
        ) : null}
        {importResult ? (
          <div className="ema-notice-success mt-3">
            Imported corpus #{importResult.corpus.id}. Rules created: {importResult.rules_created}. Review required: {String(importResult.review_required)}.
          </div>
        ) : null}
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <SimpleTable
          title="Code Corpora"
          columns={["Name", "Family", "Edition", "Status", "Health", "Gate"]}
          rows={corpora.map((item) => [item.name, item.code_family, item.edition || "-", item.loader_status, String(item.health_score ?? "-"), item.gate_status])}
          empty="No corpora imported yet."
        />
        <SimpleTable
          title="Rule Catalog"
          columns={["Reference", "Title", "Validation", "Status", "Review"]}
          rows={rules.slice(0, 25).map((item) => [item.reference, item.title || "-", item.validation_type, item.status, item.review_status])}
          empty="No rules loaded yet."
        />
      </div>
    </section>
  );
}

function Metric({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="ema-card p-3">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</div>
      <div className="mt-1 text-xl font-semibold text-ink">{value}</div>
    </div>
  );
}

function SimpleTable({
  title,
  columns,
  rows,
  empty,
}: {
  title: string;
  columns: string[];
  rows: string[][];
  empty: string;
}) {
  return (
    <div className="ema-card" data-no-glass>
      <div className="ema-card-header">
        <h3 className="font-semibold text-ink">{title}</h3>
      </div>
      {rows.length === 0 ? (
        <p className="p-4 text-sm text-muted">{empty}</p>
      ) : (
        <div className="overflow-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-surface-2 text-left text-xs uppercase tracking-wide text-muted">
              <tr>{columns.map((col) => <th key={col} className="px-3 py-2">{col}</th>)}</tr>
            </thead>
            <tbody>
              {rows.map((row, rowIdx) => (
                <tr key={`${title}-${rowIdx}`} className="border-t border-line hover:bg-surface-2">
                  {row.map((cell, cellIdx) => (
                    <td key={`${title}-${rowIdx}-${cellIdx}`} className="px-3 py-2 text-ink">{cell}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
