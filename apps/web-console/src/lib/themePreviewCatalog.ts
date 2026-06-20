export type PreviewTone = "neutral" | "selected" | "success" | "warning" | "error" | "accent" | "info" | "muted";

export type PreviewLine = {
  label: string;
  value: string;
  detail?: string;
  tone?: PreviewTone;
};

export type PreviewBadge = {
  label: string;
  tone?: PreviewTone;
};

export type PreviewToken = {
  label: string;
  token: string;
  detail?: string;
};

export const previewShell = {
  activeItem: "Deliverable Tracker",
  inactiveItems: ["Processing / Sync", "Debug / Logs"],
  adminLabel: "Admin · EMA AI Engineering",
  bottomUserBadge: "Operator · Local Demo",
  projectSelector: "ROCHELL ES · Rockwall ISD",
  lastSync: "Last sync 5 min ago",
  topbar: ["Search", "Alerts", "Status"],
};

export const previewReadiness = {
  label: "Portfolio Readiness",
  value: "87%",
  status: "On Track",
  progress: 72,
  milestone: "Construction Documents",
  chips: ["Critical 3", "Behind 4", "Needs Review 9"],
};

export const previewEvidenceBadges: PreviewBadge[] = [
  { label: "Evidence Candidate", tone: "info" },
  { label: "Accepted Evidence", tone: "success" },
  { label: "Not Official Evidence", tone: "warning" },
  { label: "Indexed", tone: "muted" },
  { label: "Candidate", tone: "muted" },
  { label: "No Sync", tone: "error" },
  { label: "Operational", tone: "success" },
  { label: "Simulated", tone: "muted" },
];

export const previewRequirementRows: PreviewLine[] = [
  { label: "Coverage", value: "Covered", detail: "Accepted evidence linked", tone: "success" },
  { label: "Missing", value: "5", detail: "No accepted evidence", tone: "error" },
  { label: "Needs review", value: "9", detail: "Candidate evidence only", tone: "warning" },
  { label: "Non-actionable", value: "2", detail: "Excluded from denominator", tone: "muted" },
];

export const previewDocumentRows: PreviewLine[] = [
  { label: "Drawing PDF", value: "Evidence Candidate", detail: "Parser metadata only", tone: "info" },
  { label: "Specification PDF", value: "Candidate", detail: "Section reference", tone: "muted" },
  { label: "Owner Requirements", value: "Requires binding", detail: "Client link needed", tone: "warning" },
  { label: "Revit Export", value: "Indexed", detail: "Sidecar linked", tone: "success" },
];

export const previewDrawingRows: PreviewLine[] = [
  { label: "Sheet A-101", value: "Electrical", detail: "CD milestone", tone: "selected" },
  { label: "Sheet M-201", value: "Mechanical", detail: "Revision B", tone: "muted" },
  { label: "Sheet P-301", value: "Plumbing", detail: "Requirement link", tone: "info" },
  { label: "Sheet FA-401", value: "Fire Alarm", detail: "Package ready", tone: "success" },
];

export const previewTradeRows: PreviewLine[] = [
  { label: "Electrical", value: "87%", detail: "Missing 2 · Needs review 3", tone: "selected" },
  { label: "Mechanical", value: "74%", detail: "Missing 4 · Needs review 6", tone: "warning" },
  { label: "Plumbing", value: "91%", detail: "Missing 1 · Needs review 2", tone: "success" },
  { label: "Fire Protection", value: "68%", detail: "Critical 1 · Missing 5", tone: "error" },
];

export const previewModelRows: PreviewLine[] = [
  { label: "Issue", value: "Warning", detail: "Model sync stale", tone: "warning" },
  { label: "Issue", value: "Blocked", detail: "Export incomplete", tone: "error" },
  { label: "Issue", value: "Partial", detail: "Metadata only", tone: "selected" },
  { label: "Issue", value: "Success", detail: "Sidecar aligned", tone: "success" },
];

export const previewDebugRows: PreviewLine[] = [
  { label: "API /health", value: "OK", detail: "Freshness 2m", tone: "success" },
  { label: "Data freshness", value: "Warm", detail: "Local demo runtime", tone: "selected" },
  { label: "Log row", value: "Ingest complete", detail: "No duplicate key", tone: "muted" },
  { label: "Warning row", value: "Retry required", detail: "Write gate blocked", tone: "warning" },
  { label: "Error row", value: "Docker unavailable", detail: "Runtime gate blocked", tone: "error" },
  { label: "Success row", value: "Re-ingest safe", detail: "No duplicate key", tone: "success" },
];

export const previewTruthLabels = [
  "Local Demo ≠ Production",
  "Evidence Candidate ≠ Official Evidence",
  "AI Advisory ≠ Approval",
  "Deterministic Readiness",
  "Browser Smoke Manual",
  "Revit Runtime Pending",
  "Docker/PostgreSQL Runtime Gate",
];

export const previewTypography = [
  { label: "H1", value: "EMA AI", detail: "Dashboard heading", tone: "accent" },
  { label: "H2", value: "Liquid Glass", detail: "Section heading", tone: "selected" },
  { label: "Label", value: "Evidence Candidate", detail: "Status label", tone: "info" },
  { label: "Body", value: "Readable engineering copy.", detail: "Dense but calm", tone: "neutral" },
  { label: "Secondary", value: "Quiet supporting text.", detail: "Lower emphasis", tone: "muted" },
  { label: "Mono", value: "const readiness = 87;", detail: "Code / JSON", tone: "neutral" },
  { label: "KPI", value: "87%", detail: "Numeric emphasis", tone: "accent" },
];

export const previewMaterialScale = [
  { label: "Ambient background", token: "--ema-bg-gradient", detail: "Base field behind glass" },
  { label: "Glass subtle", token: "--ema-glass-gradient", detail: "Soft translucent panel" },
  { label: "Glass medium", token: "--ema-glass-bg", detail: "Balanced depth" },
  { label: "Glass strong", token: "--ema-glass-gradient-strong", detail: "Hero shell surface" },
  { label: "Readable surface", token: "--ema-readable-surface", detail: "Solid data safety" },
  { label: "Data surface", token: "--ema-data-surface", detail: "Dense rows stay legible" },
  { label: "Code / log", token: "--ema-code-surface", detail: "Console and JSON" },
  { label: "Warning surface", token: "--ema-warning-surface", detail: "Blocking or cautionary" },
  { label: "Error surface", token: "--ema-error-surface", detail: "Critical state" },
  { label: "Success surface", token: "--ema-success-surface", detail: "Positive state" },
];

export const previewTokens: PreviewToken[] = [
  { label: "App background", token: "--ema-bg-gradient", detail: "Ambient field" },
  { label: "Theme ambient primary", token: "--ema-theme-ambient-primary", detail: "Theme color wash" },
  { label: "Theme ambient secondary", token: "--ema-theme-ambient-secondary", detail: "Complementary wash" },
  { label: "Theme ambient tertiary", token: "--ema-theme-ambient-tertiary", detail: "Countertone depth" },
  { label: "Glass block", token: "--ema-glass-gradient", detail: "Soft glass" },
  { label: "Glass strong", token: "--ema-glass-gradient-strong", detail: "Prominent glass" },
  { label: "Capsule", token: "--ema-glass-gradient-capsule", detail: "Pills and chips" },
  { label: "Selected", token: "--ema-glass-gradient-selected", detail: "Active nav" },
  { label: "Sidebar bg", token: "--ema-sidebar-bg", detail: "Navigation shell" },
  { label: "Topbar bg", token: "--ema-topbar-bg", detail: "Header shell" },
  { label: "Table bg", token: "--ema-table-bg", detail: "Data safe surface" },
  { label: "Code bg", token: "--ema-code-bg", detail: "Console and JSON" },
  { label: "Log bg", token: "--ema-log-bg", detail: "Logs and payloads" },
  { label: "Liquid light", token: "--ema-bg-gradient-liquid-light", detail: "Light mode depth" },
  { label: "Liquid dark", token: "--ema-bg-gradient-liquid-dark", detail: "Dark mode depth" },
  { label: "Accent", token: "--ema-accent", detail: "Brand emphasis" },
  { label: "Text", token: "--ema-text", detail: "Primary copy" },
  { label: "Data surface", token: "--ema-data-surface", detail: "Dense content" },
  { label: "Border", token: "--ema-border", detail: "Edge clarity" },
  { label: "Ring", token: "--ema-ring", detail: "Focus visibility" },
  { label: "Pointer light", token: "--ema-pointer-light-strength", detail: "Optical reflection" },
];

export const previewColorTokens = [
  { label: "Background", token: "--ema-bg" },
  { label: "Ambient Base", token: "--ema-ambient-base" },
  { label: "Ambient A", token: "--ema-ambient-a" },
  { label: "Ambient B", token: "--ema-ambient-b" },
  { label: "Ambient C", token: "--ema-ambient-c" },
  { label: "Ambient Highlight", token: "--ema-ambient-highlight" },
  { label: "Theme Ambient Primary", token: "--ema-theme-ambient-primary" },
  { label: "Theme Ambient Secondary", token: "--ema-theme-ambient-secondary" },
  { label: "Theme Ambient Tertiary", token: "--ema-theme-ambient-tertiary" },
  { label: "Theme Ambient Warm", token: "--ema-theme-ambient-warm" },
  { label: "Theme Ambient Cool", token: "--ema-theme-ambient-cool" },
  { label: "Theme Mesh Color", token: "--ema-theme-mesh-color" },
  { label: "Theme Refraction Color", token: "--ema-theme-refraction-color" },
  { label: "Surface", token: "--ema-surface" },
  { label: "Surface 2", token: "--ema-surface-2" },
  { label: "Surface Solid", token: "--ema-surface-solid" },
  { label: "Readable Surface", token: "--ema-readable-surface" },
  { label: "Data Surface", token: "--ema-data-surface" },
  { label: "Sidebar BG", token: "--ema-sidebar-bg" },
  { label: "Topbar BG", token: "--ema-topbar-bg" },
  { label: "Table BG", token: "--ema-table-bg" },
  { label: "Log BG", token: "--ema-log-bg" },
  { label: "Code BG", token: "--ema-code-bg" },
  { label: "Text", token: "--ema-text" },
  { label: "Muted", token: "--ema-text-muted" },
  { label: "Accent", token: "--ema-accent" },
  { label: "Accent RGB", token: "--ema-accent-rgb" },
  { label: "Accent Soft", token: "--ema-accent-soft" },
  { label: "Border", token: "--ema-border" },
  { label: "Selected", token: "--ema-selected" },
  { label: "Ring", token: "--ema-ring" },
  { label: "Control BG", token: "--ema-control-bg" },
  { label: "Control Text", token: "--ema-control-text" },
  { label: "Glass BG", token: "--ema-glass-bg" },
  { label: "Glass Border", token: "--ema-glass-border" },
  { label: "Glass Glow", token: "--ema-glass-glow" },
  { label: "Badge Surface", token: "--ema-badge-surface" },
];

export const previewTransparencyTokens = [
  { label: "Glass opacity", token: "--ema-glass-opacity" },
  { label: "Readable glass", token: "--ema-glass-opacity-readable" },
  { label: "Data opacity", token: "--ema-glass-opacity-data" },
  { label: "Blur", token: "--ema-glass-blur" },
  { label: "Saturate", token: "--ema-glass-saturate" },
  { label: "Highlight", token: "--ema-glass-highlight-opacity" },
  { label: "Edge opacity", token: "--ema-glass-edge-opacity" },
  { label: "Specular", token: "--ema-glass-specular-opacity" },
  { label: "Background depth", token: "--ema-background-depth" },
  { label: "Background intensity", token: "--ema-background-intensity" },
  { label: "Background vividness", token: "--ema-background-vividness" },
  { label: "Background color", token: "--ema-background-color-strength" },
  { label: "Mesh / grid", token: "--ema-background-mesh-opacity" },
  { label: "Refraction", token: "--ema-background-refraction-opacity" },
  { label: "Pointer light", token: "--ema-pointer-light-strength" },
  { label: "Data surface", token: "--ema-data-opacity" },
  { label: "Capsule opacity", token: "--ema-capsule-opacity" },
  { label: "Control opacity", token: "--ema-control-opacity" },
  { label: "Ambient noise", token: "--ema-ambient-noise-opacity" },
  { label: "Ambient grid", token: "--ema-ambient-grid-opacity" },
];

export const previewGradientTokens = [
  { label: "App background", token: "--ema-bg-gradient" },
  { label: "Glass block", token: "--ema-glass-gradient" },
  { label: "Glass strong", token: "--ema-glass-gradient-strong" },
  { label: "Capsule", token: "--ema-glass-gradient-capsule" },
  { label: "Selected", token: "--ema-glass-gradient-selected" },
  { label: "Liquid light", token: "--ema-bg-gradient-liquid-light" },
  { label: "Liquid dark", token: "--ema-bg-gradient-liquid-dark" },
];

export const previewMotionTokens = [
  { label: "Fast", token: "--ema-motion-duration-fast" },
  { label: "Normal", token: "--ema-motion-duration-normal" },
  { label: "Slow", token: "--ema-motion-duration-slow" },
  { label: "Heartbeat", token: "--ema-motion-heartbeat-duration" },
];
