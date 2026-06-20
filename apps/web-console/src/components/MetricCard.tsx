import type { LucideIcon } from "lucide-react";

type MetricCardProps = {
  label: string;
  value: string;
  detail: string;
  icon: LucideIcon;
  tone?: "blue" | "emerald" | "amber" | "rose" | "slate";
};

const toneStyles = {
  blue: "bg-info-soft text-info",
  emerald: "bg-success-soft text-success",
  amber: "bg-warning-soft text-warning",
  rose: "bg-danger-soft text-danger",
  slate: "bg-surface-2 text-muted",
};

export function MetricCard({ label, value, detail, icon: Icon, tone = "slate" }: MetricCardProps) {
  return (
    <section className="ema-status-card ema-liquid-metric ema-anim-hover-lift p-4">
      <div className="flex items-center justify-between gap-3">
        <p className="ema-kpi-label text-xs font-semibold uppercase tracking-wide text-muted">{label}</p>
        <span className={`inline-flex h-9 w-9 items-center justify-center rounded-md ${toneStyles[tone]}`}>
          <Icon size={18} aria-hidden />
        </span>
      </div>
      <div className="ema-kpi-value mt-3 text-2xl font-semibold text-ink">{value}</div>
      <p className="ema-kpi-note mt-1 text-sm text-muted">{detail}</p>
    </section>
  );
}
