// Pipeline step keys
export type PipelineStepKey =
  | "received"
  | "validation"
  | "parsing"
  | "qa_qc_checks"
  | "dashboard_update";

export type PipelineState = {
  step: PipelineStepKey | "idle";
  lastUpdated: string | null;
  message: string | null;
};

export type PipelineStatus = "idle" | PipelineStepKey | "completed";

export type PipelineStateStore = {
  /** Current step, or "idle" before any operation runs. */
  currentStep: PipelineStepKey | "idle";
  stepState: Record<PipelineStepKey, PipelineState | null>;
  /** Set to true when any step enters "running". */
  isRunning: boolean;
  /** Reset to "idle" and clear all steps. */
  reset(): void;
  /** Transition the current step to "running" and start the chain. */
  start(step: PipelineStepKey): void;
  /** Mark a step as completed. Auto-advances to next step, setting it to "running". */
  complete(step: PipelineStepKey, message?: string): void;
  /** Mark a step as failed and stop the chain. */
  fail(step: PipelineStepKey, message: string | null): void;
  /** Returns the next step after the current one, or null. */
  nextStep(): PipelineStepKey | null;
  /** Returns the current PipelineStatus label. */
  getLabel(): PipelineStatus;
  /** Is any step in a terminal state (completed or failed)? */
  isTerminal(): boolean;
};

// Ordered list of pipeline steps
export const PIPELINE_ORDER: PipelineStepKey[] = [
  "received",
  "validation",
  "parsing",
  "qa_qc_checks",
  "dashboard_update",
];

export function getStepIndex(step: PipelineStepKey): number {
  return PIPELINE_ORDER.indexOf(step);
}

export function getNextStep(current: PipelineStepKey | "idle" | null): PipelineStepKey | null {
  if (!current || current === "idle") return PIPELINE_ORDER[0];
  const idx = getStepIndex(current);
  if (idx < 0 || idx >= PIPELINE_ORDER.length - 1) return null;
  return PIPELINE_ORDER[idx + 1];
}
