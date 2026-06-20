import type { LucideIcon } from "lucide-react";

type KpiCardProps = {
  label: string;
  value: string;
  detail: string;
  icon: LucideIcon;
  tone?: "teal" | "blue" | "amber" | "rose" | "violet" | "slate";
};

const toneStyles = {
  teal: "bg-accent-soft text-accent",
  blue: "bg-info-soft text-info",
  amber: "bg-warning-soft text-warning",
  rose: "bg-danger-soft text-danger",
  violet: "bg-advisory-soft text-advisory",
  slate: "bg-surface-2 text-muted",
};

export function KpiCard({ label, value, detail, icon: Icon, tone = "slate" }: KpiCardProps) {
  return (
    <section className="ema-kpi-card ema-liquid-kpi ema-anim-hover-lift p-4">
      <div className="flex items-start justify-between gap-3">
        <p className="ema-kpi-label max-w-28 text-sm font-semibold leading-snug text-muted">{label}</p>
        <span className={`inline-flex h-9 w-9 items-center justify-center rounded-lg ${toneStyles[tone]}`}>
          <Icon size={18} aria-hidden />
        </span>
      </div>
      <div className="ema-kpi-value mt-4 text-3xl font-semibold text-ink">{value}</div>
      <p className="ema-kpi-note mt-2 text-sm text-muted">{detail}</p>
    </section>
  );
}
