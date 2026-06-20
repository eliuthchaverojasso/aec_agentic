import { AlertTriangle, CheckCircle, ChevronDown, ChevronRight, XCircle } from "lucide-react";
import { useState } from "react";
import type {
  LandingIngestAllResponse,
  LandingManifestBatchResponse,
} from "../types";

type BatchResult = LandingManifestBatchResponse | LandingIngestAllResponse;

type Props = {
  result: BatchResult | null;
  onClose?: () => void;
};

export function BatchResultPanel({ result, onClose }: Props) {
  const [expandedProject, setExpandedProject] = useState<string | null>(null);

  if (!result) return null;

  const isManifestBatch = "updated" in result && "skipped" in result;
  const projects = "projects" in result ? (result.projects ?? []) : [];
  const dryRun = result.dry_run;

  const successCount = isManifestBatch
    ? (result as LandingManifestBatchResponse).updated
    : (result as LandingIngestAllResponse).success;
  const partialCount = isManifestBatch
    ? 0
    : (result as LandingIngestAllResponse).partial;
  const failedCount = isManifestBatch
    ? (result as LandingManifestBatchResponse).skipped
    : (result as LandingIngestAllResponse).failed;

  return (
    <section data-no-glass className="ema-status-card rounded-lg border border-line bg-surface">
      <div className="flex items-center justify-between border-b border-line px-5 py-4">
        <div className="flex items-center gap-3">
          <h3 className="text-lg font-semibold text-ink">Batch Result</h3>
          {dryRun ? (
            <span className="rounded-md bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800">
              Dry Run
            </span>
          ) : (
            <span className="rounded-md bg-teal-100 px-2 py-0.5 text-xs font-semibold text-teal-800">
              Write completed
            </span>
          )}
        </div>
        {onClose && (
          <button type="button" className="text-sm text-muted hover:text-ink" onClick={onClose}>
            Close
          </button>
        )}
      </div>

      <div className="grid gap-3 border-b border-line px-5 py-4 sm:grid-cols-4">
        <div className="rounded-lg border border-line bg-surface-2 p-3 text-center">
          <div className="text-xs font-semibold text-muted">Projects</div>
          <div className="mt-1 text-xl font-semibold text-ink">{result.project_count}</div>
        </div>
        <div className="rounded-lg border border-teal-200 bg-teal-50 p-3 text-center">
          <div className="text-xs font-semibold text-teal-700">Success</div>
          <div className="mt-1 text-xl font-semibold text-teal-800">{successCount}</div>
        </div>
        {partialCount > 0 && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-center">
            <div className="text-xs font-semibold text-amber-700">Partial</div>
            <div className="mt-1 text-xl font-semibold text-amber-800">{partialCount}</div>
          </div>
        )}
        <div className={`rounded-lg border p-3 text-center ${failedCount > 0 ? "border-rose-200 bg-rose-50" : "border-line bg-surface-2"}`}>
          <div className={`text-xs font-semibold ${failedCount > 0 ? "text-rose-700" : "text-muted"}`}>
            {failedCount > 0 ? "Failed" : "Skipped"}
          </div>
          <div className={`mt-1 text-xl font-semibold ${failedCount > 0 ? "text-rose-800" : "text-ink"}`}>
            {isManifestBatch ? (result as LandingManifestBatchResponse).skipped : failedCount}
          </div>
        </div>
        {isManifestBatch && (
          <div className="rounded-lg border border-line bg-surface p-3 text-center">
            <div className="text-xs font-semibold text-muted">Landing Root</div>
            <div className="mt-1 truncate text-sm font-mono text-ink">{(result as LandingManifestBatchResponse).landing_root}</div>
          </div>
        )}
      </div>

      {projects.length > 0 && (
        <div className="px-5 py-4">
          <div className="space-y-2">
            {projects.map((project: Record<string, unknown>) => {
              const folder = (project.project_folder ?? project.project_folder_name ?? "?") as string;
              const isExpanded = expandedProject === folder;
              const warnings = (project.warnings ?? []) as string[];
              const errors = (project.errors ?? []) as string[];
              const counts = (project.counts ?? {}) as Record<string, number>;
              const hasWarnings = warnings.length > 0;
              const hasErrors = errors.length > 0;

              let statusLabel: string;
              let statusIcon: React.ReactNode;
              if (hasErrors && !dryRun) {
                statusLabel = "Failed";
                statusIcon = <XCircle size={16} className="text-danger shrink-0" />;
              } else if (hasErrors) {
                statusLabel = "Partial";
                statusIcon = <AlertTriangle size={16} className="text-warning shrink-0" />;
              } else if (hasWarnings) {
                statusLabel = "Warning";
                statusIcon = <AlertTriangle size={16} className="text-amber-500 shrink-0" />;
              } else {
                statusLabel = "Success";
                statusIcon = <CheckCircle size={16} className="text-success shrink-0" />;
              }

              const nextAction = (project.next_action ?? project.next_action ?? "") as string;

              return (
                <div key={folder} className="rounded-lg border border-line bg-white">
                  <button
                    type="button"
                    className="flex w-full items-center gap-3 px-4 py-3 text-left transition hover:bg-surface-2"
                    onClick={() => setExpandedProject(isExpanded ? null : folder)}
                  >
                    {statusIcon}
                    <span className="flex-1 font-semibold text-ink">{folder}</span>
                    <div className="flex items-center gap-3 text-xs text-muted">
                      {Object.entries(counts).slice(0, 3).map(([key, val]) => (
                        <span key={key} className="rounded bg-surface-2 px-2 py-0.5">
                          {key}: {val}
                        </span>
                      ))}
                    </div>
                    {nextAction && (
                      <span className="text-xs font-semibold text-accent">→ {nextAction}</span>
                    )}
                    {isExpanded ? (
                      <ChevronDown size={16} className="text-muted shrink-0" />
                    ) : (
                      <ChevronRight size={16} className="text-muted shrink-0" />
                    )}
                  </button>

                  {isExpanded && (
                    <div className="border-t border-line px-4 py-3 space-y-3">
                      {hasErrors && (
                        <div>
                          <p className="text-xs font-semibold text-danger mb-1">Errors</p>
                          {errors.map((err, i) => (
                            <p key={i} className="text-xs text-danger ml-2">• {err}</p>
                          ))}
                        </div>
                      )}

                      {hasWarnings && (
                        <details>
                          <summary className="cursor-pointer text-xs font-semibold text-warning">
                            Warnings ({warnings.length})
                          </summary>
                          <div className="mt-1 space-y-1 ml-2">
                            {warnings.map((w, i) => (
                              <p key={i} className="text-xs text-amber-700">• {w}</p>
                            ))}
                          </div>
                        </details>
                      )}

                      {Object.entries(counts).length > 0 && (
                        <details>
                          <summary className="cursor-pointer text-xs font-semibold text-muted">
                            File counts ({Object.keys(counts).length})
                          </summary>
                          <div className="mt-1 flex flex-wrap gap-2 ml-2">
                            {Object.entries(counts).map(([key, val]) => (
                              <span key={key} className="rounded bg-surface-2 px-2 py-0.5 text-xs text-muted">
                                {key}: {val}
                              </span>
                            ))}
                          </div>
                        </details>
                      )}

                      {nextAction && (
                        <p className="text-xs text-muted">
                          <span className="font-semibold">Next:</span> {nextAction}
                        </p>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}

      <div className="border-t border-line px-5 py-3 text-[11px] text-subtle">
        Local Demo ·{" "}
        {dryRun ? "Read-only preview — no data written" : "Writes to local PostgreSQL"}{" "}
        · Evidence Candidate · Operator Controlled · Not Official Compliance
      </div>
    </section>
  );
}
