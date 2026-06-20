import { APPEARANCE_NOTICE } from "../../lib/appearanceMemory";

type AppearancePanelProps = {
  effectiveMode: string;
  themePackLabel: string;
  themeVariant: string;
  glassIntensity: string;
  liquidGlassActive: boolean;
};

export function AppearancePanel({
  effectiveMode,
  themePackLabel,
  themeVariant,
  glassIntensity,
  liquidGlassActive,
}: AppearancePanelProps) {
  return (
    <header className="ema-card p-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold text-ink">Appearance</h2>
          <p className="mt-1 max-w-3xl text-sm text-muted">{APPEARANCE_NOTICE}</p>
        </div>
        <div className="flex flex-wrap gap-2 text-xs font-semibold">
          <span className="ema-pill bg-local-demo-soft text-local-demo border-local-demo">Local Demo</span>
          <span className="ema-pill">UI Only</span>
          <span className="ema-pill">Stored Locally</span>
          <span className="ema-pill bg-warning-soft text-warning border-warning">Not Production</span>
        </div>
      </div>

      <div className="mt-4 flex flex-wrap gap-2 text-xs font-semibold">
        <span className="ema-status-badge text-ink">Effective: {effectiveMode}</span>
        <span className="ema-status-badge text-ink">{themePackLabel}</span>
        <span className="ema-status-badge text-ink">{themeVariant}</span>
        <span className="ema-status-badge text-ink">Glass: {glassIntensity}</span>
        {liquidGlassActive ? (
          <>
            <span className="ema-status-badge text-ink">Ambient depth active</span>
            <span className="ema-status-badge text-ink">Pointer light on</span>
          </>
        ) : null}
      </div>
    </header>
  );
}
