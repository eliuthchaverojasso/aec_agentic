type StatusBadgeProps = {
  value: string;
  variant?: "default" | "outline" | "pill";
};

const toneByValue: Record<string, "selected" | "success" | "warning" | "error" | "info" | "muted"> = {
  Ready: "success",
  "Needs manifest": "warning",
  "Needs client binding": "warning",
  Partial: "warning",
  "Has errors": "error",
  Empty: "muted",
  "On Track": "selected",
  "At Risk": "warning",
  Behind: "warning",
  Critical: "error",
  Watch: "warning",
  Synced: "success",
  Completed: "success",
  Indexed: "info",
  Advisory: "info",
  advisory: "info",
  suggested: "info",
  "Evidence candidate": "warning",
  "Evidence Candidate": "warning",
  candidate: "warning",
  "Official evidence": "success",
  "Accepted Evidence": "success",
  accepted: "success",
  "In Progress": "selected",
  Pending: "muted",
  completed: "success",
  in_progress: "selected",
  pending: "muted",
  open: "warning",
  high: "warning",
  critical: "error",
  rejected: "error",
  "needs_review": "warning",
  none: "muted",
  covered: "success",
  missing: "error",
  not_evaluated: "muted",
  "not_applicable": "muted",
  compliant: "success",
  non_compliant: "error",
  blocked: "error",
  "No Evidence": "muted",
  "Covered": "success",
  "Missing": "error",
  "Not Evaluated": "muted",
  "Not Applicable": "muted",
  stale: "muted",
  superseded: "muted",
  simulated: "muted",
  Rejected: "error",
};

const semanticClasses: Record<string, string> = {
  Advisory: "ema-advisory-label",
  advisory: "ema-advisory-label",
  suggested: "ema-advisory-label",
  "Evidence candidate": "ema-evidence-candidate-label",
  "Evidence Candidate": "ema-evidence-candidate-label",
  candidate: "ema-evidence-candidate-label",
  simulated: "ema-prototype-label ema-fallback-label",
};

const stateClasses: Record<string, string> = {
  success: "ema-state-success",
  warning: "ema-state-warning",
  error: "ema-state-error",
  info: "ema-state-info",
  selected: "ema-state-operational",
  muted: "ema-state-pending",
};

export function StatusBadge({ value, variant = "default" }: StatusBadgeProps) {
  const tone = toneByValue[value] ?? "muted";
  const semantic = semanticClasses[value] ?? "";
  const state = stateClasses[tone] ?? "";

  const baseClasses = "ema-semantic-badge ema-qa-badge inline-flex h-6 items-center px-2 text-xs font-semibold border";
  const variantClasses = {
    default: "rounded-md",
    outline: "rounded-md bg-transparent",
    pill: "rounded-full",
  };

  return <span className={`${baseClasses} is-${tone} ${variantClasses[variant]} ${semantic} ${state}`.trimEnd()}>{value}</span>;
}
