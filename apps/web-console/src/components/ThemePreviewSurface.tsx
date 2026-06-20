import type { ReactNode } from "react";
import {
  previewColorTokens,
  previewDebugRows,
  previewDocumentRows,
  previewDrawingRows,
  previewEvidenceBadges,
  previewGradientTokens,
  previewMaterialScale,
  previewModelRows,
  previewMotionTokens,
  previewReadiness,
  previewRequirementRows,
  previewShell,
  previewTokens,
  previewTransparencyTokens,
  previewTradeRows,
  previewTruthLabels,
  previewTypography,
  type PreviewBadge,
  type PreviewLine,
  type PreviewTone,
} from "../lib/themePreviewCatalog";

type ThemePreviewSurfaceProps = {
  currentPack: string;
  currentVariant: string;
  mode?: "page" | "audit";
};

type TokenTuple = readonly [string, string];

export function ThemePreviewSurface({ currentPack, currentVariant, mode = "page" }: ThemePreviewSurfaceProps) {
  const root = window.getComputedStyle(document.documentElement);
  const read = (token: string) => root.getPropertyValue(token).trim() || "fallback";
  const isAudit = mode === "audit";
  const colorTokens = previewColorTokens.map((token) => [token.label, read(token.token)] as const);
  const transparencyTokens = previewTransparencyTokens.map((token) => [token.label, read(token.token)] as const);
  const gradientTokens = previewGradientTokens.map((token) => [token.label, read(token.token)] as const);
  const motionTokens = previewMotionTokens.map((token) => [token.label, read(token.token)] as const);

  return (
    <section className={`ema-theme-preview-shell ${isAudit ? "is-audit" : ""}`}>
      {!isAudit ? (
        <div className="ema-theme-preview-header">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Visual QA / Calibration Board</div>
            <h3 className="mt-1 text-xl font-semibold text-ink">Theme Preview</h3>
            <p className="mt-1 max-w-3xl text-sm text-muted">
              A live diagnostic board for material depth, dense-data readability, semantic labels, and export calibration.
            </p>
          </div>
          <div className="flex flex-wrap justify-end gap-2 text-xs font-semibold">
            <span className="ema-pill bg-accent-soft text-accent border-accent">{currentPack}</span>
            <span className="ema-pill">{currentVariant}</span>
            <span className="ema-pill bg-local-demo-soft text-local-demo border-local-demo">Local Demo</span>
            <span className="ema-pill bg-warning-soft text-warning border-warning">Not Production</span>
          </div>
        </div>
      ) : null}

      <div className="ema-theme-preview-board">
        <PreviewPanel className="ema-preview-panel-feature" title="Ambient / Material / Typography" subtitle="Layered background, glass scale, and type hierarchy.">
          <AmbientFieldDemo />
          <MaterialScale />
          <TypographyScale />
        </PreviewPanel>

        <PreviewPanel title="Shell / Navigation / Controls" subtitle="Sidebar, topbar, capsules, inputs, and active states.">
          <ShellControlDemo />
        </PreviewPanel>

        <PreviewPanel title="Readiness / Project Status" subtitle="Executive KPI, milestone status, and operator controls.">
          <ReadinessDemo />
        </PreviewPanel>

        <PreviewPanel title="Evidence / Requirements Semantics" subtitle="Candidate vs accepted evidence and deterministic coverage.">
          <EvidenceRequirementDemo />
        </PreviewPanel>

        <PreviewPanel title="Documents / Drawing Reel" subtitle="Indexed docs, parser state, and sheet package rows.">
          <DocumentDrawingDemo />
        </PreviewPanel>

        <PreviewPanel title="Trade / Model Health" subtitle="Discipline readiness and model issue status.">
          <TradeModelDemo />
        </PreviewPanel>

        <PreviewPanel title="Data / Logs / Code" subtitle="Solid dense surfaces protected from glass washout.">
          <DataLogsDemo />
        </PreviewPanel>

        <PreviewPanel title="Semantic Badges / Truth Boundaries" subtitle="Status families with text, tone, and boundary labels.">
          <SemanticTruthDemo />
        </PreviewPanel>

        <PreviewPanel className="ema-preview-panel-feature" title="Token / Gradient / Calibration Inspector" subtitle="Compact swatches for the current token set.">
          <TokenInspectorDemo colorTokens={colorTokens} gradientTokens={gradientTokens} transparencyTokens={transparencyTokens} motionTokens={motionTokens} />
        </PreviewPanel>
      </div>
    </section>
  );
}

function PreviewPanel({
  title,
  subtitle,
  className = "",
  children,
}: {
  title: string;
  subtitle: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <article className={`ema-preview-panel ${className}`}>
      <div className="ema-preview-panel-title">
        <div>
          <h4>{title}</h4>
          <p>{subtitle}</p>
        </div>
      </div>
      <div className="ema-preview-panel-body">{children}</div>
    </article>
  );
}

function AmbientFieldDemo() {
  return (
    <div className="ema-preview-ambient-field">
      <div className="ema-preview-mesh" />
      <div className="ema-preview-floating-glass">
        <div className="flex items-start justify-between gap-3">
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide text-muted">Ambient field</div>
            <div className="mt-1 text-lg font-semibold text-ink">Glass refracts layered background depth.</div>
          </div>
          <span className="ema-status-badge text-ink">Pointer light</span>
        </div>
        <div className="mt-4 grid gap-2 sm:grid-cols-3">
          <DepthChip label="Cool radial" />
          <DepthChip label="Warm countertone" />
          <DepthChip label="Technical grid" />
        </div>
      </div>
    </div>
  );
}

function DepthChip({ label }: { label: string }) {
  return (
    <div className="ema-glass-control px-3 py-2 text-xs font-semibold text-ink">
      {label}
    </div>
  );
}

function MaterialScale() {
  return (
    <div className="ema-material-scale-grid">
      {previewMaterialScale.map((item, index) => (
        <div key={item.label} className={`ema-material-swatch ema-material-swatch-${index}`}>
          <span className="ema-material-swatch-sample" />
          <span className="min-w-0">
            <span className="block truncate text-xs font-semibold text-ink">{item.label}</span>
            <span className="block truncate font-mono text-[10px] text-muted">{item.token}</span>
          </span>
        </div>
      ))}
    </div>
  );
}

function TypographyScale() {
  return (
    <div className="ema-typography-strip">
      {previewTypography.map((item) => (
        <div key={item.label} className="min-w-0">
          <div className="text-[10px] font-semibold uppercase tracking-wide text-muted">{item.label}</div>
          <div className={`truncate ${item.label === "KPI" ? "text-2xl font-semibold text-accent" : "text-sm font-semibold text-ink"}`}>
            {item.value}
          </div>
        </div>
      ))}
    </div>
  );
}

function ShellControlDemo() {
  return (
    <div className="grid gap-3 lg:grid-cols-[0.85fr_1.15fr]">
      <div className="ema-preview-sidebar">
        <div className="text-[10px] font-semibold uppercase tracking-wide text-muted">{previewShell.adminLabel}</div>
        <div className="mt-3 space-y-2">
          <div className="ema-preview-nav-row is-active">{previewShell.activeItem}</div>
          {previewShell.inactiveItems.map((item) => (
            <div key={item} className="ema-preview-nav-row">{item}</div>
          ))}
        </div>
        <div className="mt-5 rounded-full border border-line bg-surface px-3 py-2 text-xs font-semibold text-ink">
          {previewShell.bottomUserBadge}
        </div>
      </div>
      <div className="space-y-3">
        <div className="ema-preview-topbar">
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold text-ink">{previewShell.projectSelector}</div>
            <div className="text-xs text-muted">{previewShell.lastSync}</div>
          </div>
          <div className="flex shrink-0 gap-2">
            {previewShell.topbar.map((item) => (
              <span key={item} className="ema-glass-control px-3 py-1.5 text-xs font-semibold text-ink">{item}</span>
            ))}
          </div>
        </div>
        <div className="ema-preview-control-grid">
          <button type="button" className="ema-btn-primary text-xs">Primary</button>
          <button type="button" className="ema-btn-secondary text-xs">Secondary</button>
          <input className="ema-input px-3 py-2 text-xs" value="Filter documents" readOnly />
          <select className="ema-select px-3 py-2 text-xs" value="CD" onChange={() => undefined}>
            <option value="CD">CD milestone</option>
          </select>
          <label className="flex items-center gap-2 rounded-md border border-line bg-surface px-3 py-2 text-xs font-semibold text-ink">
            <input className="ema-checkbox h-4 w-4" type="checkbox" checked readOnly />
            Checked
          </label>
          <input className="ema-slider w-full" type="range" min="0" max="100" value="72" readOnly />
        </div>
      </div>
    </div>
  );
}

function ReadinessDemo() {
  return (
    <div className="grid gap-3 md:grid-cols-[0.85fr_1.15fr]">
      <div className="ema-kpi-glass">
        <div className="text-xs font-semibold uppercase tracking-wide text-muted">{previewReadiness.label}</div>
        <div className="mt-1 text-5xl font-semibold text-ink">{previewReadiness.value}</div>
        <div className="mt-1 text-sm font-semibold text-accent">{previewReadiness.status}</div>
        <Progress value={previewReadiness.progress} />
      </div>
      <div className="space-y-2">
        <div className="ema-data-row">
          <span>Milestone</span>
          <strong>{previewReadiness.milestone}</strong>
          <em>Local demo</em>
        </div>
        {previewReadiness.chips.map((chip, index) => (
          <Badge key={chip} label={chip} tone={index === 0 ? "error" : index === 1 ? "warning" : "selected"} />
        ))}
        <div className="ema-semantic-info-surface p-3 text-xs font-semibold text-ink">
          Deterministic readiness remains the source of truth.
        </div>
      </div>
    </div>
  );
}

function EvidenceRequirementDemo() {
  return (
    <div className="space-y-3">
      <BadgeWrap badges={previewEvidenceBadges} />
      <Rows rows={previewRequirementRows} />
      <div className="ema-solid-warning-surface rounded-lg border border-warning p-3 text-xs font-semibold text-ink">
        Candidate != Accepted. Accepted evidence still does not imply official compliance.
      </div>
    </div>
  );
}

function DocumentDrawingDemo() {
  return (
    <div className="grid gap-3 xl:grid-cols-2">
      <Rows rows={previewDocumentRows} />
      <Rows rows={previewDrawingRows} />
    </div>
  );
}

function TradeModelDemo() {
  return (
    <div className="grid gap-3 xl:grid-cols-2">
      <Rows rows={previewTradeRows} />
      <Rows rows={previewModelRows} />
    </div>
  );
}

function DataLogsDemo() {
  return (
    <div className="grid gap-3 lg:grid-cols-[1fr_0.9fr]">
      <Rows rows={previewDebugRows} />
      <pre className="ema-solid-json-surface rounded-lg border border-line p-4 text-xs text-ink">
{`{
  "evidence_status": "candidate",
  "coverage": "needs_review",
  "official_evidence": false
}`}
      </pre>
    </div>
  );
}

function SemanticTruthDemo() {
  const environmentBadges: PreviewBadge[] = [
    { label: "Local Demo", tone: "selected" },
    { label: "UI Only", tone: "muted" },
    { label: "Stored Locally", tone: "muted" },
    { label: "Not Production", tone: "warning" },
  ];
  const runtimeBadges: PreviewBadge[] = [
    { label: "Advisory", tone: "info" },
    { label: "Browser Smoke Manual", tone: "warning" },
    { label: "Revit Runtime Pending", tone: "warning" },
    { label: "Docker/PostgreSQL Gate", tone: "selected" },
    { label: "Deterministic Readiness", tone: "success" },
  ];
  return (
    <div className="space-y-3">
      <BadgeWrap badges={environmentBadges} />
      <BadgeWrap badges={previewEvidenceBadges} />
      <BadgeWrap badges={runtimeBadges} />
      <div className="grid gap-2 sm:grid-cols-2">
        {previewTruthLabels.map((label) => (
          <div key={label} className="ema-truth-row">{label}</div>
        ))}
      </div>
    </div>
  );
}

function TokenInspectorDemo({
  colorTokens,
  gradientTokens,
  transparencyTokens,
  motionTokens,
}: {
  colorTokens: readonly TokenTuple[];
  gradientTokens: readonly TokenTuple[];
  transparencyTokens: readonly TokenTuple[];
  motionTokens: readonly TokenTuple[];
}) {
  return (
    <div className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
      <div>
        <div className="text-xs font-semibold uppercase tracking-wide text-muted">Gradient / material strips</div>
        <div className="mt-2 grid gap-2">
          {previewTokens.slice(0, 18).map((item) => {
            const value = findTokenValue(item.label, gradientTokens, colorTokens, transparencyTokens);
            return <TokenRow key={item.label} label={item.label} value={value || item.token} />;
          })}
        </div>
      </div>
      <div className="space-y-3">
        <CalibrationStrip tokens={transparencyTokens} />
        <TokenList title="Motion" tokens={motionTokens} />
        <TokenList title="Core colors" tokens={colorTokens.slice(0, 8)} />
      </div>
    </div>
  );
}

function Rows({ rows }: { rows: PreviewLine[] }) {
  return (
    <div className="grid gap-2">
      {rows.map((row) => (
        <div key={`${row.label}-${row.value}`} className={`ema-data-row ${toneClass(row.tone)}`}>
          <span>{row.label}</span>
          <strong>{row.value}</strong>
          <em>{row.detail}</em>
        </div>
      ))}
    </div>
  );
}

function BadgeWrap({ badges }: { badges: PreviewBadge[] }) {
  return (
    <div className="flex flex-wrap gap-2">
      {badges.map((badge) => <Badge key={badge.label} label={badge.label} tone={badge.tone} />)}
    </div>
  );
}

function Badge({ label, tone }: { label: string; tone?: PreviewTone }) {
  return <span className={`ema-qa-badge ${toneClass(tone)}`}>{label}</span>;
}

function TokenRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="ema-token-row">
      <span className="ema-token-swatch" style={{ background: value.startsWith("linear") || value.startsWith("radial") ? value : value }} />
      <strong>{label}</strong>
      <code title={value}>{value}</code>
    </div>
  );
}

function CalibrationStrip({ tokens }: { tokens: readonly TokenTuple[] }) {
  return (
    <div>
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">Calibration strip</div>
      <div className="mt-2 grid gap-2">
        {tokens.map(([label, value]) => (
          <div key={label} className="ema-calibration-row">
            <span>{label}</span>
            <strong>{value}</strong>
          </div>
        ))}
      </div>
    </div>
  );
}

function TokenList({ title, tokens }: { title: string; tokens: readonly TokenTuple[] }) {
  return (
    <div>
      <div className="text-xs font-semibold uppercase tracking-wide text-muted">{title}</div>
      <div className="mt-2 grid gap-2">
        {tokens.map(([label, value]) => (
          <div key={label} className="ema-calibration-row">
            <span>{label}</span>
            <strong title={value}>{value}</strong>
          </div>
        ))}
      </div>
    </div>
  );
}

function Progress({ value }: { value: number }) {
  return (
    <div className="mt-4">
      <div className="h-2.5 overflow-hidden rounded-full bg-surface-2">
        <div className="h-full rounded-full bg-accent" style={{ width: `${value}%` }} />
      </div>
      <div className="mt-2 text-xs font-semibold text-muted">{value}% milestone package confidence</div>
    </div>
  );
}

function findTokenValue(label: string, ...groups: readonly (readonly TokenTuple[])[]) {
  for (const group of groups) {
    const match = group.find(([candidate]) => candidate === label);
    if (match) return match[1];
  }
  return "";
}

function toneClass(tone?: PreviewTone) {
  switch (tone) {
    case "success":
      return "is-success";
    case "warning":
      return "is-warning";
    case "error":
      return "is-error";
    case "selected":
    case "accent":
      return "is-selected";
    case "info":
      return "is-info";
    case "muted":
      return "is-muted";
    default:
      return "is-neutral";
  }
}
