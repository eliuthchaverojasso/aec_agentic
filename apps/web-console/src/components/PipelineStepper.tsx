import { Check, FileJson, RefreshCw, ListChecks, Database, X, Clock } from "lucide-react";
import type { PipelineStepKey } from "../lib/pipelineState";

type StepState = "idle" | "queued" | "running" | "completed" | "partial" | "warning" | "failed" | "skipped" | "stale";

type PipelineStepperProps = {
  steps: readonly PipelineStepKey[];
  stepStates: Record<string, { status: StepState; message?: string | null }>;
  className?: string;
};

const stepIcons: Record<string, React.ComponentType<{ size?: number }>> = {
  received: Check,
  validation: FileJson,
  parsing: RefreshCw,
  qa_qc_checks: ListChecks,
  dashboard_update: Database,
};

const stepLabels: Record<string, string> = {
  received: "Received",
  validation: "Validation",
  parsing: "Parsing",
  qa_qc_checks: "QA/QC",
  dashboard_update: "Dashboard",
};

function statusNodeClass(state: StepState): string {
  switch (state) {
    case "completed": return "ema-stepper-node-completed";
    case "running": return "ema-stepper-node-active ema-loading-pulse";
    case "queued": return "ema-stepper-node-active";
    case "partial":
    case "warning": return "ema-stepper-node-active";
    case "failed": return "ema-stepper-node-failed";
    default: return "";
  }
}

function statusConnectorClass(state: StepState): string {
  switch (state) {
    case "completed": return "ema-stepper-connector-completed";
    case "running": return "ema-stepper-connector-active";
    case "queued":
    case "partial":
    case "warning":
    case "stale": return "ema-stepper-connector-active";
    case "failed": return "ema-stepper-connector-failed";
    default: return "";
  }
}

function statusIcon(state: StepState) {
  if (state === "completed") return Check;
  if (state === "failed") return X;
  if (state === "running") return RefreshCw;
  if (state === "queued" || state === "partial" || state === "warning" || state === "stale") return Clock;
  return Clock;
}

export function PipelineStepper({ steps, stepStates, className = "" }: PipelineStepperProps) {
  return (
    <div className={`flex items-center ${className}`}>
      {steps.map((step, index) => {
        const state = stepStates[step]?.status || "idle";
        const Icon = statusIcon(state);
        const NodeIcon = stepIcons[step] || stepIcons.received;
        const isLast = index === steps.length - 1;

        return (
          <div key={step} className="flex items-center flex-1 last:flex-none">
            <div className="flex flex-col items-center">
              <span className={`ema-stepper-node ${statusNodeClass(state)} ${state === "running" ? "animate-pulse" : ""}`}>
                {state === "idle" ? (
                  <NodeIcon size={16} aria-hidden />
                ) : (
                  <Icon size={16} aria-hidden />
                )}
              </span>
              <span className={`mt-2 text-xs font-semibold ${
                state === "completed" ? "text-success" :
                state === "running" ? "text-accent" :
                state === "failed" ? "text-danger" :
                "text-muted"
              }`}>
                {stepLabels[step] || step}
              </span>
              {stepStates[step]?.message && (
                <span className="mt-0.5 text-[10px] text-subtle max-w-24 truncate text-center">
                  {stepStates[step]!.message}
                </span>
              )}
            </div>
            {!isLast && (
              <div className={`ema-stepper-connector mx-2 ${statusConnectorClass(state)}`} />
            )}
          </div>
        );
      })}
    </div>
  );
}
