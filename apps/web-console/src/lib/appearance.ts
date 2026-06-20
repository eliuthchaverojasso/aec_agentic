import { type ThemePackKey, type ThemeVariant, THEME_PACKS, applyThemePack, type EffectiveThemeMode } from "./themePacks";
import {
  APPEARANCE_LEGACY_STORAGE_KEYS,
  APPEARANCE_STORAGE_KEY as APPEARANCE_STORAGE_KEY_IN_MEMORY,
} from "./appearanceMemory";
export { APPEARANCE_LEGACY_STORAGE_KEYS };
export const APPEARANCE_STORAGE_KEY = APPEARANCE_STORAGE_KEY_IN_MEMORY;

export type ColorSchemePreference = "light" | "dark" | "system";
export type ResolvedColorScheme = "light" | "dark";
export type VisualTheme = "minimalCorporate" | "liquidGlass" | "materialEngineering" | "highContrast";
export type AccentProfile = "emaTeal" | "gold" | "slate" | "blue" | "graphite" | "customLocal";
export type Density = "comfortable" | "compact" | "dense";
export type GlassIntensity = "none" | "subtle" | "medium" | "strong";
export type MotionPreference = "normal" | "reduced" | "system";
export type ChartStyle = "clean" | "executive" | "dense";
export type SidebarMode = "expanded" | "compact" | "iconOnly" | "auto";
export type UiScale = 90 | 100 | 110 | 125;
export type DiffMarkerStyle = "color" | "symbols" | "both";
export type AmbientMotion = "off" | "subtle" | "normal" | "vivid";
export type CalibrationMode = "operational" | "strong" | "extreme";

export interface WhiteLabelSettings {
  organizationName: string;
  dashboardTitle: string;
  localDemoWatermark: boolean;
  logoUrl: string;
  footerText: string;
}

export interface AppearanceSettings {
  colorScheme: ColorSchemePreference;
  visualTheme: VisualTheme;
  accent: AccentProfile;
  density: Density;
  glassIntensity: GlassIntensity;
  sidebarMode: SidebarMode;
  chartStyle: ChartStyle;
  motion: MotionPreference;
  uiScale: UiScale;
  themePack: ThemePackKey;
  themeVariant: ThemeVariant;
  translucentSidebar: boolean;
  contrast: number;
  glassOpacity: number;
  glassBlur: number;
  glassSaturate: number;
  rimHighlight: number;
  backgroundDepth: number;
  backgroundIntensity: number;
  backgroundVividness: number;
  backgroundColorStrength: number;
  backgroundMeshOpacity: number;
  backgroundRefractionOpacity: number;
  ambientMotion: AmbientMotion;
  ambientMotionSpeed: number;
  ambientHueDrift: number;
  calibrationMode: CalibrationMode;
  pointerLightStrength: number;
  dataSurfaceOpacity: number;
  capsuleOpacity: number;
  uiFont: string;
  codeFont: string;
  usePointerCursors: boolean;
  diffMarkers: DiffMarkerStyle;
  whiteLabel: WhiteLabelSettings;
  showPrototypeLabels: boolean;
  showEvidenceCandidateLabels: boolean;
  showAdvisoryLabels: boolean;
  showFallbackLabels: boolean;
  showLocalDemoLabels: boolean;
}

export const APPEARANCE_SCHEMA_VERSION = 1;

export const defaultWhiteLabel: WhiteLabelSettings = {
  organizationName: "EMA Engineering",
  dashboardTitle: "EMA AI",
  localDemoWatermark: true,
  logoUrl: "",
  footerText: "Local Demo · Not Production · Not Official Compliance",
};

export const defaultAppearance: AppearanceSettings = {
  colorScheme: "system",
  visualTheme: "minimalCorporate",
  accent: "emaTeal",
  density: "dense",
  glassIntensity: "subtle",
  sidebarMode: "expanded",
  chartStyle: "clean",
  motion: "system",
  uiScale: 100,
  themePack: "EMA",
  themeVariant: "Light",
  translucentSidebar: false,
  contrast: 50,
  glassOpacity: 0.72,
  glassBlur: 14,
  glassSaturate: 1.24,
  rimHighlight: 0.55,
  backgroundDepth: 0.72,
  backgroundIntensity: 0.85,
  backgroundVividness: 0.70,
  backgroundColorStrength: 0.85,
  backgroundMeshOpacity: 0.18,
  backgroundRefractionOpacity: 0.25,
  ambientMotion: "subtle" as AmbientMotion,
  ambientMotionSpeed: 0.35,
  ambientHueDrift: 0,
  calibrationMode: "operational" as CalibrationMode,
  pointerLightStrength: 0.45,
  dataSurfaceOpacity: 0.98,
  capsuleOpacity: 0.74,
  uiFont: "Inter",
  codeFont: "JetBrains Mono",
  usePointerCursors: true,
  diffMarkers: "color",
  whiteLabel: { ...defaultWhiteLabel },
  showPrototypeLabels: true,
  showEvidenceCandidateLabels: true,
  showAdvisoryLabels: true,
  showFallbackLabels: true,
  showLocalDemoLabels: true,
};

type LegacyAppearance = {
  theme?: "ema-light" | "ema-glass" | "high-contrast";
  density?: "comfortable" | "compact";
  accent?: "teal" | "gold" | "neutral";
  showPrototypeLabels?: boolean;
  showEvidenceCandidateLabels?: boolean;
};

function normalizeColorScheme(value: unknown): ColorSchemePreference {
  return value === "light" || value === "dark" || value === "system" ? value : defaultAppearance.colorScheme;
}

function normalizeVisualTheme(value: unknown): VisualTheme {
  return value === "minimalCorporate" || value === "liquidGlass" || value === "materialEngineering" || value === "highContrast"
    ? value
    : defaultAppearance.visualTheme;
}

function normalizeAccent(value: unknown): AccentProfile {
  return value === "emaTeal" || value === "gold" || value === "slate" || value === "blue" || value === "graphite" || value === "customLocal"
    ? value
    : defaultAppearance.accent;
}

function normalizeDensity(value: unknown): Density {
  return value === "comfortable" || value === "compact" || value === "dense" ? value : defaultAppearance.density;
}

function normalizeGlass(value: unknown): GlassIntensity {
  return value === "none" || value === "subtle" || value === "medium" || value === "strong" ? value : defaultAppearance.glassIntensity;
}

function normalizeSidebarMode(value: unknown): SidebarMode {
  return value === "expanded" || value === "compact" || value === "iconOnly" || value === "auto"
    ? value
    : defaultAppearance.sidebarMode;
}

function normalizeChartStyle(value: unknown): ChartStyle {
  return value === "clean" || value === "executive" || value === "dense" ? value : defaultAppearance.chartStyle;
}

function normalizeMotion(value: unknown): MotionPreference {
  return value === "normal" || value === "reduced" || value === "system" ? value : defaultAppearance.motion;
}

function normalizeUiScale(value: unknown): UiScale {
  return value === 90 || value === 100 || value === 110 || value === 125 ? value : defaultAppearance.uiScale;
}

function normalizeThemePack(value: unknown): ThemePackKey {
  const valid: ThemePackKey[] = Object.keys(THEME_PACKS) as ThemePackKey[];
  return valid.includes(value as ThemePackKey) ? (value as ThemePackKey) : defaultAppearance.themePack;
}

function normalizeThemeVariant(value: unknown): ThemeVariant {
  const valid: ThemeVariant[] = ["Bold", "Matte", "Light", "Dark", "LiquidGlassLight", "LiquidGlassDark"];
  return valid.includes(value as ThemeVariant) ? (value as ThemeVariant) : defaultAppearance.themeVariant;
}

function normalizeContrast(value: unknown): number {
  return typeof value === "number" && value >= 0 && value <= 100 ? value : defaultAppearance.contrast;
}

function normalizeAmbientMotion(value: unknown): AmbientMotion {
  return value === "off" || value === "subtle" || value === "normal" || value === "vivid"
    ? value
    : defaultAppearance.ambientMotion;
}

function normalizeCalibrationMode(value: unknown): CalibrationMode {
  return value === "operational" || value === "strong" || value === "extreme"
    ? value
    : defaultAppearance.calibrationMode;
}

function normalizeRange(value: unknown, fallback: number, min: number, max: number): number {
  if (typeof value !== "number" || Number.isNaN(value)) {
    return fallback;
  }
  return Math.min(max, Math.max(min, value));
}

function normalizeFont(value: unknown, fallback: string): string {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : fallback;
}

function normalizeDiffMarkers(value: unknown): DiffMarkerStyle {
  return value === "color" || value === "symbols" || value === "both" ? value : defaultAppearance.diffMarkers;
}

function normalizeWhiteLabel(value: unknown): WhiteLabelSettings {
  if (!value || typeof value !== "object") return { ...defaultWhiteLabel };
  const wl = value as Record<string, unknown>;
  return {
    organizationName: typeof wl.organizationName === "string" ? wl.organizationName : defaultWhiteLabel.organizationName,
    dashboardTitle: typeof wl.dashboardTitle === "string" ? wl.dashboardTitle : defaultWhiteLabel.dashboardTitle,
    localDemoWatermark: typeof wl.localDemoWatermark === "boolean" ? wl.localDemoWatermark : defaultWhiteLabel.localDemoWatermark,
    logoUrl: typeof wl.logoUrl === "string" ? wl.logoUrl : defaultWhiteLabel.logoUrl,
    footerText: typeof wl.footerText === "string" ? wl.footerText : defaultWhiteLabel.footerText,
  };
}

function normalizeBoolean(value: unknown, fallback: boolean): boolean {
  return typeof value === "boolean" ? value : fallback;
}

function migrateLegacyAppearance(legacy: LegacyAppearance): Partial<AppearanceSettings> {
  const visualTheme: VisualTheme =
    legacy.theme === "high-contrast"
      ? "highContrast"
      : legacy.theme === "ema-glass"
        ? "liquidGlass"
        : "minimalCorporate";
  const colorScheme: ColorSchemePreference = legacy.theme === "ema-light" ? "light" : "system";
  const accent: AccentProfile = legacy.accent === "gold" ? "gold" : legacy.accent === "neutral" ? "graphite" : "emaTeal";
  return {
    visualTheme,
    colorScheme,
    accent,
    density: legacy.density ?? defaultAppearance.density,
    showPrototypeLabels: legacy.showPrototypeLabels,
    showEvidenceCandidateLabels: legacy.showEvidenceCandidateLabels,
  };
}

export function sanitizeAppearanceInput(input: unknown): AppearanceSettings {
  if (!input || typeof input !== "object") {
    return defaultAppearance;
  }
  const record = input as Record<string, unknown>;
  const migrated = record.theme ? migrateLegacyAppearance(record as LegacyAppearance) : {};
  const source = { ...record, ...migrated };
  return {
    colorScheme: normalizeColorScheme(source.colorScheme),
    visualTheme: normalizeVisualTheme(source.visualTheme),
    accent: normalizeAccent(source.accent),
    density: normalizeDensity(source.density),
    glassIntensity: normalizeGlass(source.glassIntensity),
    sidebarMode: normalizeSidebarMode(source.sidebarMode),
    chartStyle: normalizeChartStyle(source.chartStyle),
    motion: normalizeMotion(source.motion),
    uiScale: normalizeUiScale(source.uiScale),
    themePack: normalizeThemePack(source.themePack),
    themeVariant: normalizeThemeVariant(source.themeVariant),
    translucentSidebar: normalizeBoolean(source.translucentSidebar, defaultAppearance.translucentSidebar),
    contrast: normalizeContrast(source.contrast),
    glassOpacity: normalizeRange(source.glassOpacity, defaultAppearance.glassOpacity, 0.3, 0.98),
    glassBlur: normalizeRange(source.glassBlur, defaultAppearance.glassBlur, 0, 40),
    glassSaturate: normalizeRange(source.glassSaturate, defaultAppearance.glassSaturate, 0.8, 2.4),
    rimHighlight: normalizeRange(source.rimHighlight, defaultAppearance.rimHighlight, 0, 1),
    backgroundDepth: normalizeRange(source.backgroundDepth, defaultAppearance.backgroundDepth, 0, 1.5),
    backgroundIntensity: normalizeRange(source.backgroundIntensity, defaultAppearance.backgroundIntensity, 0, 3),
    backgroundVividness: normalizeRange(source.backgroundVividness, defaultAppearance.backgroundVividness, 0, 3),
    backgroundColorStrength: normalizeRange(source.backgroundColorStrength, defaultAppearance.backgroundColorStrength, 0, 3),
    backgroundMeshOpacity: normalizeRange(source.backgroundMeshOpacity, defaultAppearance.backgroundMeshOpacity, 0, 0.8),
    backgroundRefractionOpacity: normalizeRange(source.backgroundRefractionOpacity, defaultAppearance.backgroundRefractionOpacity, 0, 1),
    ambientMotion: normalizeAmbientMotion(source.ambientMotion),
    ambientMotionSpeed: normalizeRange(source.ambientMotionSpeed, defaultAppearance.ambientMotionSpeed, 0, 1.5),
    ambientHueDrift: normalizeRange(source.ambientHueDrift, defaultAppearance.ambientHueDrift, -90, 90),
    calibrationMode: normalizeCalibrationMode(source.calibrationMode),
    pointerLightStrength: normalizeRange(source.pointerLightStrength, defaultAppearance.pointerLightStrength, 0, 1.5),
    dataSurfaceOpacity: normalizeRange(source.dataSurfaceOpacity, defaultAppearance.dataSurfaceOpacity, 0.8, 1),
    capsuleOpacity: normalizeRange(source.capsuleOpacity, defaultAppearance.capsuleOpacity, 0.35, 1),
    uiFont: normalizeFont(source.uiFont, defaultAppearance.uiFont),
    codeFont: normalizeFont(source.codeFont, defaultAppearance.codeFont),
    usePointerCursors: normalizeBoolean(source.usePointerCursors, defaultAppearance.usePointerCursors),
    diffMarkers: normalizeDiffMarkers(source.diffMarkers),
    whiteLabel: normalizeWhiteLabel(source.whiteLabel),
    showPrototypeLabels: normalizeBoolean(source.showPrototypeLabels, defaultAppearance.showPrototypeLabels),
    showEvidenceCandidateLabels: normalizeBoolean(source.showEvidenceCandidateLabels, defaultAppearance.showEvidenceCandidateLabels),
    showAdvisoryLabels: normalizeBoolean(source.showAdvisoryLabels, defaultAppearance.showAdvisoryLabels),
    showFallbackLabels: normalizeBoolean(source.showFallbackLabels, defaultAppearance.showFallbackLabels),
    showLocalDemoLabels: normalizeBoolean(source.showLocalDemoLabels, defaultAppearance.showLocalDemoLabels),
  };
}

export function resolveColorScheme(colorScheme: ColorSchemePreference): ResolvedColorScheme {
  if (colorScheme === "system") {
    try {
      return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    } catch {
      return "light";
    }
  }
  return colorScheme;
}

export function resolveEffectiveThemeMode(
  colorScheme: ColorSchemePreference,
  variant: ThemeVariant,
): EffectiveThemeMode {
  if (variant === "Dark" || variant === "LiquidGlassDark") return "dark";
  if (variant === "Light" || variant === "LiquidGlassLight") return "light";
  return resolveColorScheme(colorScheme);
}

export function readAppearanceSettings(): AppearanceSettings {
  try {
    const raw = window.localStorage.getItem(APPEARANCE_STORAGE_KEY);
    if (raw) {
      return sanitizeAppearanceInput(JSON.parse(raw));
    }
    for (const legacyKey of APPEARANCE_LEGACY_STORAGE_KEYS) {
      const legacyRaw = window.localStorage.getItem(legacyKey);
      if (!legacyRaw) {
        continue;
      }
      const sanitized = sanitizeAppearanceInput(JSON.parse(legacyRaw));
      window.localStorage.setItem(APPEARANCE_STORAGE_KEY, JSON.stringify(sanitized));
      window.localStorage.removeItem(legacyKey);
      return sanitized;
    }
    return defaultAppearance;
  } catch {
    return defaultAppearance;
  }
}

export function writeAppearanceSettings(settings: AppearanceSettings): void {
  try {
    const sanitized = sanitizeAppearanceInput(settings);
    window.localStorage.setItem(APPEARANCE_STORAGE_KEY, JSON.stringify(sanitized));
    for (const legacyKey of APPEARANCE_LEGACY_STORAGE_KEYS) {
      window.localStorage.removeItem(legacyKey);
    }
  } catch {
    // Appearance is local UI state only; storage failures must never blank the app.
  }
}

export function resetAppearanceSettings(): AppearanceSettings {
  try {
    window.localStorage.removeItem(APPEARANCE_STORAGE_KEY);
    for (const legacyKey of APPEARANCE_LEGACY_STORAGE_KEYS) {
      window.localStorage.removeItem(legacyKey);
    }
  } catch {
    // Ignore storage failures and still apply visible defaults.
  }
  applyAppearanceSettings(defaultAppearance);
  return defaultAppearance;
}

function datasetValueForTheme(theme: VisualTheme): string {
  switch (theme) {
    case "minimalCorporate": return "minimal-corporate";
    case "liquidGlass": return "liquid-glass";
    case "materialEngineering": return "material-engineering";
    case "highContrast": return "high-contrast";
  }
}

function datasetValueForAccent(accent: AccentProfile): string {
  switch (accent) {
    case "emaTeal": return "ema-teal";
    case "gold": return "gold";
    case "slate": return "slate";
    case "blue": return "blue";
    case "graphite": return "graphite";
    case "customLocal": return "custom-local";
  }
}

export function applyAppearanceSettings(settings: AppearanceSettings): ResolvedColorScheme {
  const safeSettings = sanitizeAppearanceInput(settings);
  const resolved = resolveColorScheme(safeSettings.colorScheme);
  const effectiveMode = resolveEffectiveThemeMode(safeSettings.colorScheme, safeSettings.themeVariant);
  const root = document.documentElement;
  root.dataset.theme = safeSettings.themePack;
  root.dataset.themePack = safeSettings.themePack;
  root.dataset.themeVariant = safeSettings.themeVariant;
  root.dataset.colorScheme = effectiveMode;
  root.dataset.visualTheme = datasetValueForTheme(safeSettings.visualTheme);
  root.dataset.accent = datasetValueForAccent(safeSettings.accent);
  root.dataset.density = safeSettings.density;
  root.dataset.glass = safeSettings.glassIntensity;
  root.dataset.glassIntensity = safeSettings.glassIntensity;
  root.dataset.sidebar = safeSettings.sidebarMode;
  root.dataset.chart = safeSettings.chartStyle;
  const resolvedMotion =
    safeSettings.motion === "system"
      ? (safeMatchMedia("(prefers-reduced-motion: reduce)") ? "reduced" : "normal")
      : safeSettings.motion;
  root.dataset.motion = resolvedMotion;
  root.dataset.uiScale = String(safeSettings.uiScale);
  root.dataset.effectiveMode = effectiveMode;
  root.dataset.themePack = safeSettings.themePack;
  root.dataset.themeVariant = safeSettings.themeVariant
    .replace(/([a-z])([A-Z])/g, "$1_$2")
    .replace(/([A-Z]+)/g, (match) => match.toLowerCase());
  root.dataset.translucentSidebar = String(safeSettings.translucentSidebar);
  root.dataset.contrast = String(safeSettings.contrast);
  root.dataset.uiFont = safeSettings.uiFont;
  root.dataset.codeFont = safeSettings.codeFont;
  root.dataset.usePointerCursors = String(safeSettings.usePointerCursors);
  root.dataset.diffMarkers = safeSettings.diffMarkers;
  root.style.setProperty("--ema-ui-font", safeSettings.uiFont);
  root.style.setProperty("--ema-code-font", safeSettings.codeFont);
  root.style.setProperty("--ema-ui-scale", `${safeSettings.uiScale / 100}`);
  root.style.setProperty("--ema-contrast-level", String(safeSettings.contrast / 100));
  root.style.setProperty("--ema-muted", "var(--ema-text-muted)");
  root.style.setProperty("--ema-subtle", "var(--ema-text-subtle)");
  root.style.setProperty("--ema-accent-muted", "color-mix(in srgb, var(--ema-accent) 68%, var(--ema-text-muted) 32%)");
  root.style.setProperty("--ema-accent-contrast", "var(--ema-accent-text)");
  root.style.setProperty("--ema-selected-surface", "var(--ema-selected)");
  root.style.setProperty("--ema-selected-border", "color-mix(in srgb, var(--ema-accent) 36%, var(--ema-border-strong) 64%)");
  root.style.setProperty("--ema-rim-highlight", "var(--ema-rim-highlight-strength)");
  root.style.setProperty("--ema-glass-saturation", "var(--ema-glass-saturate)");
  root.style.setProperty("--ema-focus-ring", `0 0 0 3px rgba(${effectiveMode === "dark" ? "91, 184, 168" : "30, 106, 90"}, ${(safeSettings.contrast / 100) * 0.35 + 0.15})`);

  const densitySpacing: Record<string, { card: string; section: string; row: string; control: string }> = {
    comfortable: { card: "20px", section: "24px", row: "16px", control: "12px" },
    compact: { card: "14px", section: "18px", row: "10px", control: "8px" },
    dense: { card: "10px", section: "14px", row: "6px", control: "6px" },
  };
  const spacing = densitySpacing[safeSettings.density] ?? densitySpacing.dense;
  root.style.setProperty("--ema-card-padding", spacing.card);
  root.style.setProperty("--ema-section-gap", spacing.section);
  root.style.setProperty("--ema-row-gap", spacing.row);
  root.style.setProperty("--ema-control-gap", spacing.control);

  root.dataset.ambientMotion = safeSettings.ambientMotion;
  root.dataset.calibrationMode = safeSettings.calibrationMode;
  root.style.setProperty("--ema-ambient-motion-speed", String(safeSettings.ambientMotionSpeed));
  root.style.setProperty("--ema-ambient-hue-drift", `${safeSettings.ambientHueDrift}deg`);

  root.dataset.showPrototypeLabels = String(safeSettings.showPrototypeLabels);
  root.dataset.showEvidenceCandidateLabels = String(safeSettings.showEvidenceCandidateLabels);
  root.dataset.showAdvisoryLabels = String(safeSettings.showAdvisoryLabels);
  root.dataset.showFallbackLabels = String(safeSettings.showFallbackLabels);
  root.dataset.showLocalDemoLabels = String(safeSettings.showLocalDemoLabels);
  root.style.setProperty("--ema-org-name", JSON.stringify(safeSettings.whiteLabel.organizationName));
  root.style.setProperty("--ema-dashboard-title", JSON.stringify(safeSettings.whiteLabel.dashboardTitle));
  root.style.setProperty("--ema-footer-text", JSON.stringify(safeSettings.whiteLabel.footerText));
  root.dataset.localDemoWatermark = String(safeSettings.whiteLabel.localDemoWatermark);
  const appliedMode = applyThemePack(safeSettings.themePack, safeSettings.themeVariant, effectiveMode);
  applyLiquidGlassCalibration(root, safeSettings, appliedMode);
  root.dataset.colorScheme = appliedMode;
  root.dataset.effectiveThemeMode = appliedMode;
  return resolved;
}

function applyLiquidGlassCalibration(
  root: HTMLElement,
  settings: AppearanceSettings,
  appliedMode: EffectiveThemeMode,
): void {
  const modeDefaults =
    appliedMode === "dark"
      ? { glassOpacity: 0.58, backgroundDepth: 0.76, capsuleOpacity: 0.68 }
      : { glassOpacity: 0.76, backgroundDepth: 0.68, capsuleOpacity: 0.78 };
  const glassOpacity = settings.glassOpacity ?? modeDefaults.glassOpacity;
  const subtle = Math.max(0.24, glassOpacity - 0.18);
  const medium = glassOpacity;
  const strong = Math.min(0.98, glassOpacity + 0.12);

  /* ── Effective (clamped) values for operational routes ── */
  const glassMin = appliedMode === "dark" ? 0.58 : 0.72;
  const glassMax = 0.90;
  const glassEffective = Math.min(glassMax, Math.max(glassMin, glassOpacity));
  const dataMin = appliedMode === "dark" ? 0.92 : 0.98;
  const dataEffective = Math.max(dataMin, settings.dataSurfaceOpacity);
  const bgDepthEffective = Math.min(0.85, Math.max(0, settings.backgroundDepth));
  const bgIntensityEffective = Math.min(0.75, Math.max(0, settings.backgroundIntensity));
  const vividnessEffective = Math.min(0.75, Math.max(0, settings.backgroundVividness));
  const bgColorStrengthEffective = Math.min(0.75, Math.max(0, settings.backgroundColorStrength));
  const bgMeshEffective = Math.min(0.45, Math.max(0, settings.backgroundMeshOpacity));
  const bgRefractionEffective = Math.min(0.35, Math.max(0, settings.backgroundRefractionOpacity));
  const ambientMotionEffective = Math.min(0.75, Math.max(0, settings.ambientMotionSpeed));
  const pointerEffective = Math.min(0.25, Math.max(0, settings.pointerLightStrength));
  const capsuleEffective = Math.min(0.9, Math.max(0.52, settings.capsuleOpacity));
  const controlEffective = Math.min(0.9, Math.max(0.52, settings.capsuleOpacity));

  /* ── Main variables are raw QA values for the live Appearance/Theme Preview board ── */
  root.style.setProperty("--ema-glass-opacity", glassOpacity.toFixed(2));
  root.style.setProperty("--ema-glass-opacity-subtle", subtle.toFixed(2));
  root.style.setProperty("--ema-glass-opacity-medium", medium.toFixed(2));
  root.style.setProperty("--ema-glass-opacity-strong", strong.toFixed(2));
  root.style.setProperty("--ema-glass-opacity-effective", glassEffective.toFixed(2));
  root.style.setProperty("--ema-glass-blur", `${settings.glassBlur.toFixed(0)}px`);
  root.style.setProperty("--ema-glass-blur-effective", `${Math.min(14, Math.max(0, settings.glassBlur)).toFixed(0)}px`);
  root.style.setProperty("--ema-glass-saturate", settings.glassSaturate.toFixed(2));
  root.style.setProperty("--ema-glass-saturate-effective", Math.min(1.35, Math.max(0.8, settings.glassSaturate)).toFixed(2));
  root.style.setProperty("--ema-glass-saturation", settings.glassSaturate.toFixed(2));
  root.style.setProperty("--ema-rim-highlight-strength", settings.rimHighlight.toFixed(2));
  root.style.setProperty("--ema-rim-highlight-strength-effective", Math.min(1, Math.max(0, settings.rimHighlight)).toFixed(2));
  root.style.setProperty("--ema-glass-highlight-opacity", settings.rimHighlight.toFixed(2));
  root.style.setProperty("--ema-glass-edge-opacity", `${Math.round(settings.rimHighlight * 100)}%`);
  root.style.setProperty("--ema-glass-specular-opacity", settings.rimHighlight.toFixed(2));
  root.style.setProperty("--ema-background-depth", settings.backgroundDepth.toFixed(2));
  root.style.setProperty("--ema-background-depth-effective", bgDepthEffective.toFixed(2));
  root.style.setProperty("--ema-bg-mesh-opacity", settings.backgroundDepth.toFixed(2));
  root.style.setProperty("--ema-background-intensity", settings.backgroundIntensity.toFixed(2));
  root.style.setProperty("--ema-background-intensity-effective", bgIntensityEffective.toFixed(2));
  root.style.setProperty("--ema-background-vividness", settings.backgroundVividness.toFixed(2));
  root.style.setProperty("--ema-background-vividness-effective", vividnessEffective.toFixed(2));
  root.style.setProperty("--ema-background-color-strength", settings.backgroundColorStrength.toFixed(2));
  root.style.setProperty("--ema-background-color-strength-effective", bgColorStrengthEffective.toFixed(2));
  root.style.setProperty("--ema-background-mesh-opacity", settings.backgroundMeshOpacity.toFixed(2));
  root.style.setProperty("--ema-background-mesh-opacity-effective", bgMeshEffective.toFixed(2));
  root.style.setProperty("--ema-background-refraction-opacity", settings.backgroundRefractionOpacity.toFixed(2));
  root.style.setProperty("--ema-background-refraction-opacity-effective", bgRefractionEffective.toFixed(2));
  root.style.setProperty("--ema-ambient-motion-speed", settings.ambientMotionSpeed.toFixed(2));
  root.style.setProperty("--ema-ambient-motion-speed-effective", ambientMotionEffective.toFixed(2));
  root.style.setProperty("--ema-pointer-light-strength", settings.pointerLightStrength.toFixed(2));
  root.style.setProperty("--ema-pointer-light-strength-effective", pointerEffective.toFixed(2));
  root.style.setProperty("--ema-data-opacity", dataEffective.toFixed(2));
  root.style.setProperty("--ema-data-opacity-effective", dataEffective.toFixed(2));
  root.style.setProperty("--ema-capsule-opacity", settings.capsuleOpacity.toFixed(2));
  root.style.setProperty("--ema-capsule-opacity-effective", capsuleEffective.toFixed(2));
  root.style.setProperty("--ema-control-opacity", settings.capsuleOpacity.toFixed(2));
  root.style.setProperty("--ema-control-opacity-effective", controlEffective.toFixed(2));

  /* ── Raw values (for QA/preview surfaces only) ── */
  root.style.setProperty("--ema-glass-opacity-raw", glassOpacity.toFixed(2));
  root.style.setProperty("--ema-background-intensity-raw", settings.backgroundIntensity.toFixed(2));
  root.style.setProperty("--ema-background-vividness-raw", settings.backgroundVividness.toFixed(2));
  root.style.setProperty("--ema-background-refraction-opacity-raw", settings.backgroundRefractionOpacity.toFixed(2));
  root.style.setProperty("--ema-data-opacity-raw", settings.dataSurfaceOpacity.toFixed(2));
}

export function initializeAppearance(): AppearanceSettings {
  const settings = readAppearanceSettings();
  try {
    applyAppearanceSettings(settings);
    return settings;
  } catch {
    return resetAppearanceSettings();
  }
}

function safeMatchMedia(query: string): boolean {
  try {
    return Boolean(window.matchMedia && window.matchMedia(query).matches);
  } catch {
    return false;
  }
}
