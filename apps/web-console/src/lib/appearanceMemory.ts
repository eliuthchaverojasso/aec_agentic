export const APPEARANCE_NOTICE =
  "Appearance settings are stored locally in this browser and do not affect backend readiness, source data, or official compliance status." as const;

export const APPEARANCE_STORAGE_KEY = "appearance.settings.v1" as const;
export const APPEARANCE_LEGACY_STORAGE_KEYS = ["ema-ai-appearance-settings"] as const;

export const DEFAULT_APPEARANCE_SETTINGS = {
  theme: "system",
  density: "comfortable",
  accentColor: "default",
  radius: "soft",
  motion: "normal",
  fontScale: "normal",
  dataDisplay: "detailed",
  mapStyle: "standard",
  dashboardStyle: "standard",
} as const;

export const APPEARANCE_DESIGN_PRINCIPLES = [
  "Serious, institutional, data-driven, and sober.",
  "Readable first: utility and truth take priority over decoration.",
  "Visual settings stay local and never imply backend approval.",
  "Glass is reserved for shells, headers, and summary surfaces.",
  "Dense data remains solid or semi-solid for legibility.",
  "Active state, semantic state, and limitation state must stay distinct.",
  "Theme propagation should reuse tokens before adding page-specific styles.",
] as const;

export const APPEARANCE_TOKEN_FAMILIES = [
  "background",
  "foreground",
  "muted background",
  "muted foreground",
  "card background",
  "card foreground",
  "border",
  "input",
  "ring / focus",
  "primary",
  "secondary",
  "accent",
  "destructive",
  "warning",
  "success",
  "info",
  "map colors",
  "chart colors",
  "table row states",
  "badge states",
] as const;

export const APPEARANCE_COMPONENT_RULES = [
  { component: "AppShell", rule: "Keep shell chrome readable and theme-driven." },
  { component: "PageHeader", rule: "Show title, subtitle, and local-state notes without visual noise." },
  { component: "SectionCard", rule: "Use consistent section framing and spacing." },
  { component: "DataTableShell", rule: "Keep rows solid and legible; never treat dense data as decorative glass." },
  { component: "MetricCard", rule: "Prioritize numeric hierarchy and scanability." },
  { component: "StatusBadge", rule: "Preserve semantic meaning in light and dark modes." },
  { component: "EmptyState", rule: "Be explicit about missing data or pending inputs." },
  { component: "NoticeBanner", rule: "Use for local-only boundaries and limitations." },
] as const;

export const APPEARANCE_QWEN_PROPAGATION_RULES = [
  "Read docs/APPEARANCE_MEMORY.md before editing page styling.",
  "Reuse tokens and shared components before inventing page-specific styles.",
  "Keep local appearance settings strictly local to the browser.",
  "Do not modify backend, APIs, source data, readiness, compliance, ingestion, auth, or calculations.",
  "Do not imply official approval through appearance settings.",
  "Preserve the exact local-only appearance notice.",
  "Keep evidence, data, and status UI sober and readable.",
  "Run lint, typecheck, and build after propagation work.",
] as const;

export const APPEARANCE_SETTINGS_SCHEMA = [
  { key: "theme", values: ["light", "dark", "system"], localOnly: true },
  { key: "density", values: ["comfortable", "compact"], localOnly: true },
  { key: "accentColor", values: ["default", "blue", "green", "amber", "red", "purple", "neutral"], localOnly: true },
  { key: "radius", values: ["sharp", "soft", "rounded"], localOnly: true },
  { key: "motion", values: ["normal", "reduced"], localOnly: true },
  { key: "fontScale", values: ["small", "normal", "large"], localOnly: true },
  { key: "dataDisplay", values: ["simple", "detailed"], localOnly: true },
  { key: "mapStyle", values: ["standard", "contrast", "muted"], localOnly: true },
  { key: "dashboardStyle", values: ["standard", "compact", "executive"], localOnly: true },
] as const;

export const APPEARANCE_LOCAL_STORAGE_CONTRACT = {
  key: APPEARANCE_STORAGE_KEY,
  legacyKeys: APPEARANCE_LEGACY_STORAGE_KEYS,
  defaultValues: DEFAULT_APPEARANCE_SETTINGS,
  migration: "Legacy appearance payloads from ema-ai-appearance-settings should hydrate into appearance.settings.v1 and then be treated as migrated.",
  reset: "Clear local appearance storage and restore shipped defaults.",
  hydration: "Read from window.localStorage only in browser-safe code paths; fall back to defaults when storage is unavailable.",
} as const;
