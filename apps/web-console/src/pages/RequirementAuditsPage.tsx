import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { api } from "../api/client";
import type {
  RequirementAuditRecord,
  RequirementAuditRun,
  RequirementCoherenceFinding,
} from "../types";

type RequirementAuditsPageProps = {
  projectId?: number;
  onToast?: (message: string, tone?: "success" | "info" | "warning") => void;
};

const REVIEW_ACTIONS = ["accept", "reject", "override", "request_changes", "lock", "supersede"];

function str(value: unknown): string {
  if (value === null || value === undefined) return "";
  if (typeof value === "string") return value;
  return String(value);
}

function decisionTone(status: string): string {
  switch (status) {
    case "Compliant":
      return "text-emerald-600";
    case "NonCompliant":
      return "text-rose-600";
    case "NeedsReview":
      return "text-amber-600";
    case "InsufficientData":
      return "text-sky-600";
    default:
      return "text-muted";
  }
}

function severityTone(severity: string): string {
  switch (severity) {
    case "Critical":
    case "High":
      return "text-rose-600";
    case "Medium":
      return "text-amber-600";
    case "Low":
      return "text-sky-600";
    default:
      return "text-muted";
  }
}

function refLabel(ref: Record<string, unknown> | null | undefined): string {
  if (!ref) return "(unknown)";
  const id = str(ref.requirementId) || "(no id)";
  const sheet = str(ref.sourceWorksheet);
  const row = ref.sourceRow ? `row ${str(ref.sourceRow)}` : "";
  const locator = [sheet, row].filter(Boolean).join(" ");
  return locator ? `${id} (${locator})` : id;
}

export function RequirementAuditsPage({ projectId, onToast }: RequirementAuditsPageProps) {
  const [runs, setRuns] = useState<RequirementAuditRun[]>([]);
  const [selectedRunId, setSelectedRunId] = useState<number | null>(null);
  const [records, setRecords] = useState<RequirementAuditRecord[]>([]);
  const [findings, setFindings] = useState<RequirementCoherenceFinding[]>([]);
  const [activeRecord, setActiveRecord] = useState<RequirementAuditRecord | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedRun = useMemo(
    () => runs.find((run) => run.id === selectedRunId) ?? null,
    [runs, selectedRunId],
  );

  const loadRuns = useCallback(async () => {
    if (!projectId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await api.listRequirementAuditRuns(projectId);
      setRuns(data);
      setSelectedRunId((current) => current ?? (data[0]?.id ?? null));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load audit runs");
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  useEffect(() => {
    void loadRuns();
  }, [loadRuns]);

  useEffect(() => {
    if (!projectId || selectedRunId === null) {
      setRecords([]);
      setFindings([]);
      return;
    }
    let cancelled = false;
    setLoading(true);
    Promise.all([
      api.listRequirementAuditRecords(projectId, selectedRunId),
      api.listRequirementCoherenceFindings(projectId, selectedRunId),
    ])
      .then(([recordsData, findingsData]) => {
        if (cancelled) return;
        setRecords(recordsData);
        setFindings(findingsData);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load run detail");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [projectId, selectedRunId]);

  const refreshRecords = useCallback(async () => {
    if (!projectId || selectedRunId === null) return;
    const recordsData = await api.listRequirementAuditRecords(projectId, selectedRunId);
    setRecords(recordsData);
    setActiveRecord((current) =>
      current ? recordsData.find((r) => r.id === current.id) ?? null : null,
    );
  }, [projectId, selectedRunId]);

  if (!projectId) {
    return (
      <section className="ema-page ema-page-shell ema-card" data-no-glass>
        <div className="ema-card-header">
          <div>
            <h2 className="text-sm font-semibold text-ink">Requirement Audits</h2>
            <p className="mt-0.5 text-xs text-muted">Select a project to view evaluation runs.</p>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="ema-page ema-page-shell ema-card" data-no-glass>
      <div className="ema-card-header">
        <div>
          <h2 className="text-sm font-semibold text-ink">Requirement Audits &amp; Evaluation Bundles</h2>
          <p className="mt-0.5 text-xs text-muted">
            Reproducible evaluation runs ingested from the C# engine. Each run records how every
            requirement was decided and the coherence of the requirement set. Statuses are the
            engine&apos;s; reviews are append-only and never overwrite them.
          </p>
        </div>
        <button type="button" className="ema-btn ema-btn-ghost text-xs" onClick={() => void loadRuns()}>
          Refresh
        </button>
      </div>

      {error && <div className="ema-callout ema-callout-danger text-xs">{error}</div>}

      {runs.length === 0 && !loading && (
        <div className="ema-callout text-xs">
          No evaluation runs have been ingested for this project yet. Run a requirement check in
          Revit and ingest its Evaluation Bundle via{" "}
          <code>POST /api/v1/projects/{projectId}/requirement-audits</code>.
        </div>
      )}

      {/* Run selector */}
      {runs.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {runs.map((run) => (
            <button
              type="button"
              key={run.id}
              onClick={() => {
                setSelectedRunId(run.id);
                setActiveRecord(null);
              }}
              className={`ema-liquid-capsule px-3 py-2 text-left text-xs ${
                run.id === selectedRunId ? "text-accent font-semibold" : "text-muted"
              }`}
            >
              <div className="font-mono">{run.run_uid.slice(0, 12)}…</div>
              <div>{new Date(run.ingested_at).toLocaleString()}</div>
              <div>
                {run.requirements_total} reqs ·{" "}
                <span className={run.coherence_grade === "Coherent" ? "text-emerald-600" : "text-rose-600"}>
                  {run.coherence_grade ?? "—"}
                </span>
              </div>
            </button>
          ))}
        </div>
      )}

      {selectedRun && (
        <RunSummary run={selectedRun} />
      )}

      {/* Coherence findings */}
      {selectedRun && (
        <CoherenceFindings findings={findings} />
      )}

      {/* Records table */}
      {selectedRun && (
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-line text-sm">
            <thead className="bg-surface-2 text-left text-xs font-semibold uppercase tracking-wide text-muted">
              <tr>
                <th className="px-3 py-2">Requirement</th>
                <th className="px-3 py-2">Decision</th>
                <th className="px-3 py-2">Type</th>
                <th className="px-3 py-2">Validation</th>
                <th className="px-3 py-2">Confidence</th>
                <th className="px-3 py-2">Findings</th>
                <th className="px-3 py-2">Reviews</th>
                <th className="px-3 py-2"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {records.map((record) => (
                <tr key={record.id} className="hover:bg-surface-2">
                  <td className="px-3 py-2 font-mono text-xs text-ink">{record.requirement_uid}</td>
                  <td className={`px-3 py-2 font-semibold ${decisionTone(record.decision_status)}`}>
                    {record.decision_status}
                  </td>
                  <td className="px-3 py-2 text-xs text-muted">{record.requirement_type}</td>
                  <td className="px-3 py-2 text-xs text-muted">{record.validation_type}</td>
                  <td className="px-3 py-2 text-xs">
                    {record.confidence !== null ? record.confidence.toFixed(2) : "—"}
                  </td>
                  <td className="px-3 py-2 text-xs">{record.coherence_finding_ids.length}</td>
                  <td className="px-3 py-2 text-xs">{record.review_decisions.length}</td>
                  <td className="px-3 py-2 text-right">
                    <button
                      type="button"
                      className="ema-btn ema-btn-ghost text-xs"
                      onClick={() => setActiveRecord(record)}
                    >
                      Open
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {activeRecord && (
        <RecordDrawer
          projectId={projectId}
          runId={selectedRunId as number}
          record={activeRecord}
          onClose={() => setActiveRecord(null)}
          onReviewed={async () => {
            await refreshRecords();
            onToast?.("Review decision recorded", "success");
          }}
          onError={(message) => onToast?.(message, "warning")}
        />
      )}
    </section>
  );
}

function RunSummary({ run }: { run: RequirementAuditRun }) {
  return (
    <div className="ema-liquid-capsule grid grid-cols-2 gap-3 p-3 text-xs md:grid-cols-4">
      <Stat label="Run id" value={run.run_uid.slice(0, 16) + "…"} mono />
      <Stat label="As of" value={new Date(run.as_of).toLocaleString()} />
      <Stat label="Engine" value={run.engine_version ?? "—"} />
      <Stat label="Ruleset" value={run.ruleset_version ?? "—"} />
      <Stat label="Met" value={str(run.status_counts.met ?? 0)} />
      <Stat label="Not Met" value={str(run.status_counts.not_met ?? 0)} />
      <Stat label="Needs Review" value={str(run.status_counts.needs_human_review ?? 0)} />
      <Stat
        label="Coherence"
        value={`${run.coherence_grade ?? "—"} (${run.coherence_findings_total})`}
      />
      <Stat label="Output hash" value={(run.output_hash ?? "—").slice(0, 16) + "…"} mono />
      <Stat label="Input hash" value={(run.input_hash ?? "—").slice(0, 16) + "…"} mono />
    </div>
  );
}

function Stat({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <div className="text-[10px] uppercase tracking-wide text-muted">{label}</div>
      <div className={`text-ink ${mono ? "font-mono" : ""}`}>{value}</div>
    </div>
  );
}

function CoherenceFindings({ findings }: { findings: RequirementCoherenceFinding[] }) {
  if (findings.length === 0) {
    return (
      <div className="ema-callout text-xs">
        No duplicate or conflicting requirements were detected for this run.
      </div>
    );
  }
  return (
    <div>
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted">
        Coherence findings ({findings.length})
      </h3>
      <div className="mt-2 space-y-2">
        {findings.map((finding) => (
          <div key={finding.id} className="rounded-md border border-line p-2 text-xs">
            <div className="flex flex-wrap items-center gap-2">
              <span className={`font-semibold ${severityTone(finding.severity)}`}>
                {finding.finding_type}
              </span>
              <span className="text-muted">· {finding.severity}</span>
              <span className="text-muted">· type: {finding.requirement_type ?? "—"}</span>
            </div>
            <div className="mt-1 text-ink">{finding.rationale}</div>
            <div className="mt-1 text-muted">
              {refLabel(finding.primary_requirement)}
              {finding.related_requirement ? ` ↔ ${refLabel(finding.related_requirement)}` : ""}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function RecordDrawer({
  projectId,
  runId,
  record,
  onClose,
  onReviewed,
  onError,
}: {
  projectId: number;
  runId: number;
  record: RequirementAuditRecord;
  onClose: () => void;
  onReviewed: () => Promise<void> | void;
  onError: (message: string) => void;
}) {
  const [action, setAction] = useState<string>("accept");
  const [reason, setReason] = useState<string>("");
  const [reviewer, setReviewer] = useState<string>("");
  const [resultingStatus, setResultingStatus] = useState<string>("");
  const [submitting, setSubmitting] = useState(false);

  const submit = async () => {
    if (!reason.trim()) {
      onError("A reason is required to record a review decision.");
      return;
    }
    setSubmitting(true);
    try {
      await api.createRequirementReviewDecision(projectId, runId, record.id, {
        action,
        reason: reason.trim(),
        reviewer_name: reviewer.trim() || undefined,
        resulting_status: resultingStatus.trim() || undefined,
      });
      setReason("");
      setResultingStatus("");
      await onReviewed();
    } catch (err) {
      onError(err instanceof Error ? err.message : "Failed to record review");
    } finally {
      setSubmitting(false);
    }
  };

  const source = record.source_provenance ?? {};
  const policy = record.evidence_policy ?? {};
  const funnel = record.candidate_funnel ?? {};

  return (
    <div className="ema-drawer-overlay" role="dialog" aria-modal="true" onClick={onClose}>
      <div className="ema-drawer max-w-2xl" onClick={(event) => event.stopPropagation()}>
        <div className="ema-card-header">
          <div>
            <h3 className="text-sm font-semibold text-ink">{record.requirement_uid}</h3>
            <p className={`text-xs font-semibold ${decisionTone(record.decision_status)}`}>
              {record.decision_status} · {record.lifecycle_status}
            </p>
          </div>
          <button type="button" className="ema-btn ema-btn-ghost text-xs" onClick={onClose}>
            Close
          </button>
        </div>

        <div className="space-y-3 overflow-y-auto p-1 text-xs">
          <Section title="Source &amp; provenance">
            <KV k="File" v={str(source.sourceFile)} />
            <KV k="Worksheet" v={str(source.sourceWorksheet)} />
            <KV k="Row" v={str(source.sourceRow)} />
            <KV k="Content hash" v={str(record.requirement_content_hash).slice(0, 24)} mono />
            <KV k="Traceability complete" v={str(source.traceabilityComplete)} />
          </Section>

          <Section title="Decision">
            <KV k="Rule applied" v={str(record.rule_applied)} />
            <KV k="Reason" v={str(record.decision_reason)} />
            <KV k="Confidence" v={record.confidence !== null ? record.confidence.toFixed(3) : "—"} />
            <KV k="Direct evidence" v={str(record.direct_evidence_count)} />
            <KV k="Supporting evidence" v={str(record.supporting_evidence_count)} />
            <KV k="Next best action" v={str(record.next_best_action)} />
          </Section>

          <Section title="Evidence policy">
            <KV k="Operator" v={str(policy.operator)} />
            <KV k="Required types" v={Array.isArray(policy.requiredEvidenceTypes) ? (policy.requiredEvidenceTypes as string[]).join(", ") : ""} />
            <KV k="Closure requires review" v={str(policy.closureRequiresHumanReview)} />
          </Section>

          <Section title="Candidate funnel">
            <KV k="Universe" v={str(funnel.universeCount)} />
            <KV k="Qualified" v={str(funnel.qualifiedCount)} />
            <KV k="Broad match" v={str(funnel.broadMatch)} />
          </Section>

          {record.coherence_finding_ids.length > 0 && (
            <Section title="Coherence findings">
              <ul className="list-disc pl-4 text-muted">
                {record.coherence_finding_ids.map((id) => (
                  <li key={id} className="font-mono">{id}</li>
                ))}
              </ul>
            </Section>
          )}

          <Section title={`Review history (${record.review_decisions.length})`}>
            {record.review_decisions.length === 0 ? (
              <div className="text-muted">No human review decisions yet.</div>
            ) : (
              <ul className="space-y-1">
                {record.review_decisions.map((decision) => (
                  <li key={decision.id} className="rounded border border-line p-2">
                    <span className="font-semibold text-ink">{decision.action}</span>
                    {" · "}
                    {decision.previous_status ?? "—"} → {decision.resulting_status ?? "—"}
                    {" · "}
                    <span className="text-muted">{decision.reviewer_name ?? "anonymous"}</span>
                    <div className="text-muted">{decision.reason}</div>
                  </li>
                ))}
              </ul>
            )}
          </Section>

          <Section title="Record an immutable review">
            <div className="grid grid-cols-2 gap-2">
              <label className="text-muted">
                Action
                <select
                  className="ema-input mt-1 w-full"
                  value={action}
                  onChange={(event) => setAction(event.target.value)}
                >
                  {REVIEW_ACTIONS.map((value) => (
                    <option key={value} value={value}>
                      {value}
                    </option>
                  ))}
                </select>
              </label>
              <label className="text-muted">
                Resulting status (optional)
                <input
                  className="ema-input mt-1 w-full"
                  value={resultingStatus}
                  onChange={(event) => setResultingStatus(event.target.value)}
                  placeholder="Compliant"
                />
              </label>
            </div>
            <label className="mt-2 block text-muted">
              Reviewer name
              <input
                className="ema-input mt-1 w-full"
                value={reviewer}
                onChange={(event) => setReviewer(event.target.value)}
                placeholder="Your name"
              />
            </label>
            <label className="mt-2 block text-muted">
              Reason (required)
              <textarea
                className="ema-input mt-1 w-full"
                rows={2}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                placeholder="Why is this decision being recorded?"
              />
            </label>
            <button
              type="button"
              className="ema-btn ema-btn-primary mt-2 text-xs"
              disabled={submitting}
              onClick={() => void submit()}
            >
              {submitting ? "Recording…" : "Record review"}
            </button>
          </Section>
        </div>
      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="rounded-md border border-line p-2">
      <h4 className="text-[10px] font-semibold uppercase tracking-wide text-muted">{title}</h4>
      <div className="mt-1 space-y-0.5">{children}</div>
    </div>
  );
}

function KV({ k, v, mono }: { k: string; v: string; mono?: boolean }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-muted">{k}</span>
      <span className={`text-right text-ink ${mono ? "font-mono" : ""}`}>{v || "—"}</span>
    </div>
  );
}
