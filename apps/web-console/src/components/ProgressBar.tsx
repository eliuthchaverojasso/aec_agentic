type ProgressBarProps = {
  value: number;
  tone?: "teal" | "blue" | "amber" | "rose" | "violet";
  label?: string;
};

const toneStyles = {
  teal: "bg-accent",
  blue: "bg-info",
  amber: "bg-warning",
  rose: "bg-danger",
  violet: "bg-advisory-soft",
};

export function ProgressBar({ value, tone = "teal", label }: ProgressBarProps) {
  const bounded = Math.max(0, Math.min(100, value));
  return (
    <div className="ema-progress-track flex items-center gap-2">
      <div className="h-2 w-24 rounded-full bg-surface-2">
        <div className={`ema-progress-fill h-2 rounded-full ${toneStyles[tone]}`} style={{ width: `${bounded}%` }} />
      </div>
      {label && <span className="text-xs font-semibold text-muted">{label}</span>}
    </div>
  );
}
