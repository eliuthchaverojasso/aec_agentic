export type ThemePack = {
  name: string;
  mode: "Light" | "Dark" | "Adaptive";
  background: string;
  surface: string;
  text: string;
  muted: string;
  accent: string;
  border: string;
  glassLight?: string;
  glassDark?: string;
};

export type ThemeVariant = "Bold" | "Matte" | "Light" | "Dark" | "LiquidGlassLight" | "LiquidGlassDark";

export type ThemePackKey =
  | "EMA" | "Codex" | "Opencode" | "Matrix" | "Dracula" | "Catppuccin"
  | "Cobalt2" | "Cursor" | "Everforest" | "Flexoki" | "GitHub" | "Gruvbox"
  | "Kanagawa" | "Linear" | "Material" | "Mercury" | "Monokai" | "Nightowl"
  | "Nord" | "Notion" | "One" | "OneDark" | "Palenight" | "Raycast"
  | "RosePine" | "Solarized" | "Vercel" | "VSCodePlus" | "Xcode"
  | "LucentOrng" | "Orng" | "OsakaJade" | "Proof";

export const THEME_PACKS: Record<ThemePackKey, ThemePack> = {
  EMA: {
    name: "EMA", mode: "Light",
    background: "#F8FAF8", surface: "#FFFFFF", text: "#183C35", muted: "#64746F",
    accent: "#246B5F", border: "#D9E2DD",
    glassLight: "rgba(255,255,255,0.72)", glassDark: "rgba(20,41,34,0.82)",
  },
  Codex: {
    name: "Codex", mode: "Dark",
    background: "#0B0F0E", surface: "#121716", text: "#E6FFF1", muted: "#8FA99B",
    accent: "#10A37F", border: "#22332D",
    glassLight: "rgba(18,23,22,0.82)", glassDark: "rgba(18,23,22,0.88)",
  },
  Opencode: {
    name: "Opencode", mode: "Dark",
    background: "#050807", surface: "#0B120E", text: "#D9FFE5", muted: "#6FA77E",
    accent: "#2EFF72", border: "#173522",
    glassLight: "rgba(11,18,14,0.82)", glassDark: "rgba(11,18,14,0.88)",
  },
  Matrix: {
    name: "Matrix", mode: "Dark",
    background: "#000000", surface: "#06110A", text: "#39FF88", muted: "#1E8F4D",
    accent: "#00FF41", border: "#0F3D20",
    glassLight: "rgba(6,17,10,0.82)", glassDark: "rgba(6,17,10,0.88)",
  },
  Dracula: {
    name: "Dracula", mode: "Dark",
    background: "#282A36", surface: "#44475A", text: "#F8F8F2", muted: "#6272A4",
    accent: "#BD93F9", border: "#44475A",
    glassLight: "rgba(68,71,90,0.78)", glassDark: "rgba(68,71,90,0.84)",
  },
  Catppuccin: {
    name: "Catppuccin", mode: "Dark",
    background: "#1E1E2E", surface: "#313244", text: "#CDD6F4", muted: "#9399B2",
    accent: "#CBA6F7", border: "#45475A",
    glassLight: "rgba(49,50,68,0.78)", glassDark: "rgba(49,50,68,0.84)",
  },
  Cobalt2: {
    name: "Cobalt2", mode: "Dark",
    background: "#132839", surface: "#1E3D51", text: "#E1EFFF", muted: "#7C9EB2",
    accent: "#FF9D00", border: "#2B5270",
  },
  Cursor: {
    name: "Cursor", mode: "Dark",
    background: "#1C1C1C", surface: "#252526", text: "#D4D4D4", muted: "#858585",
    accent: "#DE6B6B", border: "#333333",
  },
  Everforest: {
    name: "Everforest", mode: "Dark",
    background: "#2F383E", surface: "#3D484D", text: "#D3C6AA", muted: "#859289",
    accent: "#A7C080", border: "#4B565C",
  },
  Flexoki: {
    name: "Flexoki", mode: "Light",
    background: "#FFFCF0", surface: "#F2F0E6", text: "#100F0F", muted: "#6F6E69",
    accent: "#AF3029", border: "#DAD8CE",
  },
  GitHub: {
    name: "GitHub", mode: "Light",
    background: "#FFFFFF", surface: "#F6F8FA", text: "#24292F", muted: "#57606A",
    accent: "#0969DA", border: "#D0D7DE",
    glassLight: "rgba(246,248,250,0.78)",
  },
  Gruvbox: {
    name: "Gruvbox", mode: "Dark",
    background: "#282828", surface: "#3C3836", text: "#EBDBB2", muted: "#928374",
    accent: "#B8BB26", border: "#504945",
  },
  Kanagawa: {
    name: "Kanagawa", mode: "Dark",
    background: "#1F1F28", surface: "#2A2A37", text: "#DCD7BA", muted: "#938AA9",
    accent: "#7E9CD8", border: "#363646",
  },
  Linear: {
    name: "Linear", mode: "Dark",
    background: "#08090A", surface: "#111214", text: "#F7F8F8", muted: "#8A8F98",
    accent: "#5E6AD2", border: "#24262B",
    glassLight: "rgba(17,18,20,0.82)", glassDark: "rgba(17,18,20,0.88)",
  },
  Material: {
    name: "Material", mode: "Dark",
    background: "#263238", surface: "#37474F", text: "#EEFFFF", muted: "#90A4AE",
    accent: "#82AAFF", border: "#546E7A",
  },
  Monokai: {
    name: "Monokai", mode: "Dark",
    background: "#272822", surface: "#3E3D32", text: "#F8F8F2", muted: "#75715E",
    accent: "#A6E22E", border: "#49483E",
  },
  Nightowl: {
    name: "Nightowl", mode: "Dark",
    background: "#011627", surface: "#1D3B53", text: "#D6DEEB", muted: "#5F7E97",
    accent: "#7E57C2", border: "#2E4A62",
  },
  Nord: {
    name: "Nord", mode: "Dark",
    background: "#2E3440", surface: "#3B4252", text: "#ECEFF4", muted: "#D8DEE9",
    accent: "#88C0D0", border: "#4C566A",
    glassLight: "rgba(59,66,82,0.78)", glassDark: "rgba(59,66,82,0.84)",
  },
  Notion: {
    name: "Notion", mode: "Light",
    background: "#FFFFFF", surface: "#F7F6F3", text: "#37352F", muted: "#9B9A97",
    accent: "#0F6AB4", border: "#E9E9E7",
  },
  One: {
    name: "One", mode: "Light",
    background: "#FAFAFA", surface: "#FFFFFF", text: "#383A42", muted: "#9D9D9F",
    accent: "#4078F2", border: "#E5E5E6",
  },
  OneDark: {
    name: "OneDark", mode: "Dark",
    background: "#282C34", surface: "#353941", text: "#ABB2BF", muted: "#7F848E",
    accent: "#61AFEF", border: "#3E4451",
    glassLight: "rgba(53,57,65,0.78)", glassDark: "rgba(53,57,65,0.84)",
  },
  Palenight: {
    name: "Palenight", mode: "Dark",
    background: "#292D3E", surface: "#323650", text: "#BFC7D5", muted: "#676E95",
    accent: "#C792EA", border: "#3C405B",
  },
  Raycast: {
    name: "Raycast", mode: "Light",
    background: "#FFFFFF", surface: "#F5F5F5", text: "#1D1D1D", muted: "#878787",
    accent: "#FF6363", border: "#E6E6E6",
  },
  RosePine: {
    name: "RosePine", mode: "Dark",
    background: "#191724", surface: "#1F1D2E", text: "#E0DEF4", muted: "#908CAA",
    accent: "#EB6F92", border: "#2A283E",
  },
  Solarized: {
    name: "Solarized", mode: "Dark",
    background: "#002B36", surface: "#073642", text: "#839496", muted: "#586E75",
    accent: "#268BD2", border: "#657B83",
    glassLight: "rgba(7,54,66,0.78)", glassDark: "rgba(7,54,66,0.84)",
  },
  Vercel: {
    name: "Vercel", mode: "Dark",
    background: "#000000", surface: "#111111", text: "#FAFAFA", muted: "#A1A1AA",
    accent: "#FFFFFF", border: "#333333",
    glassLight: "rgba(17,17,17,0.82)", glassDark: "rgba(17,17,17,0.88)",
  },
  VSCodePlus: {
    name: "VSCode+", mode: "Dark",
    background: "#1E1E1E", surface: "#252526", text: "#CCCCCC", muted: "#858585",
    accent: "#007ACC", border: "#333333",
  },
  Xcode: {
    name: "Xcode", mode: "Dark",
    background: "#1A1A2E", surface: "#222244", text: "#E0E0FF", muted: "#8888AA",
    accent: "#E86C4F", border: "#30305A",
  },
  Mercury: {
    name: "Mercury", mode: "Light",
    background: "#F2F2F2", surface: "#FFFFFF", text: "#1A1A1A", muted: "#7A7A7A",
    accent: "#5C5C5C", border: "#D4D4D4",
    glassLight: "rgba(255,255,255,0.78)", glassDark: "rgba(30,30,30,0.82)",
  },
  LucentOrng: {
    name: "Lucent Orng", mode: "Dark",
    background: "#1A0F0A", surface: "#2A1810", text: "#FFE8D6", muted: "#C4957A",
    accent: "#FF6B35", border: "#3D2218",
    glassLight: "rgba(42,24,16,0.82)", glassDark: "rgba(42,24,16,0.88)",
  },
  Orng: {
    name: "ORNG", mode: "Dark",
    background: "#0D0805", surface: "#1A110A", text: "#FFDDBB", muted: "#B8865E",
    accent: "#FF5500", border: "#2E1A0E",
    glassLight: "rgba(26,17,10,0.82)", glassDark: "rgba(26,17,10,0.88)",
  },
  OsakaJade: {
    name: "Osaka Jade", mode: "Dark",
    background: "#0E1412", surface: "#182622", text: "#D6F0E8", muted: "#8AAAA0",
    accent: "#2DD4A0", border: "#243A34",
    glassLight: "rgba(24,38,34,0.82)", glassDark: "rgba(24,38,34,0.88)",
  },
  Proof: {
    name: "Proof", mode: "Dark",
    background: "#0C0C10", surface: "#16161E", text: "#E0E0F0", muted: "#8888A0",
    accent: "#6C6CF0", border: "#28283A",
    glassLight: "rgba(22,22,30,0.82)", glassDark: "rgba(22,22,30,0.88)",
  },
};

export const THEME_VARIANTS: ThemeVariant[] = [
  "Bold", "Matte", "Light", "Dark", "LiquidGlassLight", "LiquidGlassDark",
];

export const THEME_PACK_KEYS = Object.keys(THEME_PACKS) as ThemePackKey[];

export function variantForPack(pack: ThemePack): ThemeVariant {
  return pack.mode === "Dark" ? "Dark" : "Light";
}

export type EffectiveThemeMode = "light" | "dark";

function clamp(value: number, min = 0, max = 255): number {
  return Math.max(min, Math.min(max, Math.round(value)));
}

function mix(hexA: string, hexB: string, weight = 0.5): string {
  const [r1, g1, b1] = hexToRgb(hexA);
  const [r2, g2, b2] = hexToRgb(hexB);
  return rgbToHex(
    r1 + (r2 - r1) * weight,
    g1 + (g2 - g1) * weight,
    b1 + (b2 - b1) * weight,
  );
}

function alpha(hex: string, amount: number): string {
  const [r, g, b] = hexToRgb(hex);
  return `rgba(${clamp(r)}, ${clamp(g)}, ${clamp(b)}, ${Math.max(0, Math.min(1, amount)).toFixed(3)})`;
}

function rgbTuple(hex: string): string {
  const [r, g, b] = hexToRgb(hex);
  return `${clamp(r)} ${clamp(g)} ${clamp(b)}`;
}

function luminance(hex: string): number {
  const [r, g, b] = hexToRgb(hex).map((v) => v / 255).map((channel) =>
    channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4,
  );
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function readableTextFor(bg: string): string {
  return luminance(bg) > 0.5 ? "#111827" : "#F8FAFC";
}

function surfaceTextFor(surface: string, darkMode: boolean): string {
  const text = readableTextFor(surface);
  if (darkMode && text !== "#F8FAFC") return "#F8FAFC";
  if (!darkMode && text !== "#111827") return "#111827";
  return text;
}

function isDarkVariant(variant: ThemeVariant): boolean {
  return variant === "Dark" || variant === "LiquidGlassDark";
}

function isLightVariant(variant: ThemeVariant): boolean {
  return variant === "Light" || variant === "LiquidGlassLight";
}

function shouldUseDarkMode(basePack: ThemePack, variant: ThemeVariant, preferredMode: EffectiveThemeMode): boolean {
  if (isDarkVariant(variant)) return true;
  if (isLightVariant(variant)) return false;
  if (variant === "Matte" || variant === "Bold") {
    return preferredMode === "dark";
  }
  return basePack.mode === "Dark";
}

export function applyThemePack(packKey: ThemePackKey, variant: ThemeVariant, preferredMode: EffectiveThemeMode = "light"): EffectiveThemeMode {
  const pack = THEME_PACKS[packKey];
  if (!pack) return preferredMode;
  const root = document.documentElement;
  const darkMode = shouldUseDarkMode(pack, variant, preferredMode);
  const effectiveMode: EffectiveThemeMode = darkMode ? "dark" : "light";

  const bgBase = darkMode ? mix(pack.background, "#05090B", 0.35) : mix(pack.background, "#F9FBFC", 0.3);
  const surfaceBase = darkMode ? mix(pack.surface, "#0F1A1F", 0.3) : mix(pack.surface, "#FFFFFF", 0.2);
  const surface2 = darkMode ? adjustBrightness(surfaceBase, 8) : adjustBrightness(surfaceBase, -4);
  const surface3 = darkMode ? adjustBrightness(surfaceBase, 14) : adjustBrightness(surfaceBase, -10);
  const surfaceSolid = darkMode ? adjustBrightness(surfaceBase, 6) : adjustBrightness(surfaceBase, -2);
  const border = darkMode ? alpha(mix(pack.border, "#D9F1E8", 0.2), 0.24) : alpha(mix(pack.border, "#0F172A", 0.15), 0.22);
  const borderStrong = darkMode ? alpha(mix(pack.border, "#FFFFFF", 0.35), 0.38) : alpha(mix(pack.border, "#0F172A", 0.3), 0.34);

  let accent = pack.accent;
  if (variant === "Bold") accent = darkMode ? adjustBrightness(pack.accent, 22) : adjustBrightness(pack.accent, -20);
  if (variant === "Matte") accent = desaturate(pack.accent);

  const text = surfaceTextFor(surfaceBase, darkMode);
  const textMuted = darkMode ? mix(text, "#8DA6B0", 0.45) : mix(text, "#5E6A74", 0.52);
  const textSubtle = darkMode ? mix(textMuted, "#89A2AC", 0.52) : mix(textMuted, "#6C7A86", 0.56);
  const textInverse = darkMode ? "#06100D" : "#F8FAFC";
  const accentText = readableTextFor(accent);
  const ambientPrimary = alpha(mix(accent, bgBase, darkMode ? 0.18 : 0.12), darkMode ? 0.22 : 0.16);
  const ambientSecondary = alpha(mix(pack.accent, surfaceBase, darkMode ? 0.24 : 0.18), darkMode ? 0.16 : 0.13);
  const ambientTertiary = alpha(mix(pack.border, accent, 0.34), darkMode ? 0.12 : 0.1);
  const ambientWarm = alpha(mix("#C9A86A", surfaceBase, 0.4), darkMode ? 0.1 : 0.12);
  const ambientCool = alpha(mix("#5BB8A8", surfaceBase, 0.42), darkMode ? 0.12 : 0.14);
  const meshColor = alpha(mix(borderStrong, accent, 0.28), darkMode ? 0.18 : 0.12);
  const refractionColor = alpha(mix(accent, pack.background, 0.22), darkMode ? 0.16 : 0.12);
  const semanticSuccess = mix("#18794e", accent, 0.14);
  const semanticWarning = mix("#a86812", accent, 0.12);
  const semanticDanger = mix("#b02f34", accent, 0.1);
  const semanticInfo = mix("#1f63a8", accent, 0.18);
  const semanticAdvisory = mix("#7650b6", accent, 0.16);
  const semanticEvidence = mix("#0f7f78", accent, 0.16);
  const semanticPrototype = mix("#7b7f88", accent, 0.12);
  const semanticOfficial = mix("#0b6f4a", accent, 0.16);
  const semanticBlocked = mix("#a0343a", accent, 0.12);
  const semanticPartial = mix("#b8841a", accent, 0.12);
  const semanticLocalDemo = mix("#636a80", accent, 0.1);
  const bgGradient = darkMode
    ? `radial-gradient(ellipse 140% 60% at 8% -8%, ${ambientPrimary} 0, transparent 44rem), radial-gradient(ellipse 120% 50% at 92% 6%, ${ambientSecondary} 0, transparent 38rem), radial-gradient(ellipse 100% 40% at 50% 42%, ${ambientWarm} 0, transparent 36rem), radial-gradient(ellipse 80% 30% at 80% 64%, ${ambientCool} 0, transparent 28rem), radial-gradient(ellipse 60% 25% at 24% 76%, ${ambientTertiary} 0, transparent 22rem), linear-gradient(180deg, ${bgBase} 0%, ${surfaceBase} 56%, ${surface3} 140%)`
    : `radial-gradient(ellipse 140% 60% at 8% -8%, ${ambientPrimary} 0, transparent 44rem), radial-gradient(ellipse 120% 50% at 92% 6%, ${ambientSecondary} 0, transparent 38rem), radial-gradient(ellipse 100% 40% at 50% 42%, ${ambientWarm} 0, transparent 36rem), radial-gradient(ellipse 80% 30% at 80% 64%, ${ambientCool} 0, transparent 28rem), radial-gradient(ellipse 60% 25% at 24% 76%, ${ambientTertiary} 0, transparent 22rem), linear-gradient(180deg, ${bgBase} 0%, ${surfaceBase} 56%, ${surface3} 140%)`;

  const glassBg =
    variant === "LiquidGlassDark"
      ? pack.glassDark || alpha(surface2, 0.78)
      : variant === "LiquidGlassLight"
        ? pack.glassLight || alpha(surface2, 0.76)
        : alpha(surface2, darkMode ? 0.9 : 0.94);

  const isGlassVariant = variant === "LiquidGlassDark" || variant === "LiquidGlassLight";
  const ring = alpha(accent, darkMode ? 0.4 : 0.32);

  const vars: Array<[string, string]> = [
    ["--ema-bg", bgBase],
    ["--ema-bg-subtle", darkMode ? adjustBrightness(bgBase, 5) : adjustBrightness(bgBase, 3)],
    ["--ema-bg-elevated", darkMode ? adjustBrightness(surface3, 5) : "#FFFFFF"],
    ["--ema-bg-gradient", bgGradient],
    ["--ema-surface", surfaceBase],
    ["--ema-surface-2", surface2],
    ["--ema-surface-3", surface3],
    ["--ema-surface-solid", surfaceSolid],
    ["--ema-surface-solid-rgb", rgbTuple(surfaceSolid)],
    ["--ema-surface-glass", glassBg],
    ["--ema-text", text],
    ["--ema-text-muted", textMuted],
    ["--ema-text-subtle", textSubtle],
    ["--ema-text-inverse", textInverse],
    ["--ema-muted", textMuted],
    ["--ema-subtle", textSubtle],
    ["--ema-border", border],
    ["--ema-border-strong", borderStrong],
    ["--ema-ring", ring],
    ["--ema-accent", accent],
    ["--ema-accent-rgb", rgbTuple(accent)],
    ["--ema-accent-2", darkMode ? adjustBrightness(accent, 14) : adjustBrightness(accent, 10)],
    ["--ema-accent-soft", alpha(accent, darkMode ? 0.2 : 0.16)],
    ["--ema-accent-strong", darkMode ? adjustBrightness(accent, 24) : adjustBrightness(accent, -12)],
    ["--ema-accent-text", accentText],
    ["--ema-accent-muted", alpha(mix(accent, textMuted, 0.45), darkMode ? 0.28 : 0.22)],
    ["--ema-accent-contrast", accentText],
    ["--ema-selected", alpha(accent, darkMode ? 0.34 : 0.22)],
    ["--ema-selected-surface", alpha(accent, darkMode ? 0.34 : 0.22)],
    ["--ema-selected-border", borderStrong],
    ["--ema-selected-text", text],
    ["--ema-theme-ambient-primary", ambientPrimary],
    ["--ema-theme-ambient-secondary", ambientSecondary],
    ["--ema-theme-ambient-tertiary", ambientTertiary],
    ["--ema-theme-ambient-warm", ambientWarm],
    ["--ema-theme-ambient-cool", ambientCool],
    ["--ema-theme-mesh-color", meshColor],
    ["--ema-theme-refraction-color", refractionColor],
    ["--ema-sidebar-bg", alpha(surfaceBase, darkMode ? 0.92 : 0.88)],
    ["--ema-sidebar-border", border],
    ["--ema-sidebar-text", text],
    ["--ema-sidebar-muted", textMuted],
    ["--ema-sidebar-active-bg", alpha(accent, darkMode ? 0.28 : 0.18)],
    ["--ema-sidebar-active-text", text],
    ["--ema-sidebar-active-border", borderStrong],
    ["--ema-topbar-bg", alpha(surfaceBase, darkMode ? 0.9 : 0.86)],
    ["--ema-topbar-border", border],
    ["--ema-topbar-text", text],
    ["--ema-control-bg", surface3],
    ["--ema-control-bg-hover", darkMode ? adjustBrightness(surface3, 6) : adjustBrightness(surface3, -4)],
    ["--ema-control-bg-active", darkMode ? adjustBrightness(surface3, 10) : adjustBrightness(surface3, -8)],
    ["--ema-control-text", text],
    ["--ema-control-text-muted", textMuted],
    ["--ema-control-border", border],
    ["--ema-control-border-hover", borderStrong],
    ["--ema-control-placeholder", textSubtle],
    ["--ema-control-focus", ring],
    ["--ema-control-disabled-bg", darkMode ? adjustBrightness(surface2, -2) : adjustBrightness(surface2, 2)],
    ["--ema-control-disabled-text", textSubtle],
    ["--ema-control-hover-bg", darkMode ? adjustBrightness(surface3, 4) : adjustBrightness(surface3, -2)],
    ["--ema-control-selected-bg", alpha(accent, darkMode ? 0.24 : 0.18)],
    ["--ema-control-selected-text", text],
    ["--ema-segment-bg", surface2],
    ["--ema-segment-border", border],
    ["--ema-segment-text", textMuted],
    ["--ema-segment-active-bg", alpha(accent, darkMode ? 0.36 : 0.2)],
    ["--ema-segment-active-text", text],
    ["--ema-segment-hover-bg", darkMode ? adjustBrightness(surface2, 7) : adjustBrightness(surface2, -3)],
    ["--ema-tab-bg", surface2],
    ["--ema-tab-selected-bg", alpha(accent, darkMode ? 0.3 : 0.18)],
    ["--ema-tab-selected-text", text],
    ["--ema-checkbox-bg", surface3],
    ["--ema-checkbox-border", borderStrong],
    ["--ema-checkbox-checked-bg", accent],
    ["--ema-checkbox-checked-text", accentText],
    ["--ema-slider-track", darkMode ? adjustBrightness(surface2, 8) : adjustBrightness(surface2, -8)],
    ["--ema-slider-fill", accent],
    ["--ema-slider-thumb", accentText],
    ["--ema-toggle-bg", darkMode ? adjustBrightness(surface2, 8) : adjustBrightness(surface2, -8)],
    ["--ema-toggle-active-bg", accent],
    ["--ema-toggle-thumb", "#FFFFFF"],
    ["--ema-glass-bg", glassBg],
    ["--ema-glass-bg-strong", darkMode ? alpha(surface3, 0.88) : alpha(surface3, 0.94)],
    ["--ema-glass-border", alpha(mix(borderStrong, "#FFFFFF", darkMode ? 0.15 : 0.4), darkMode ? 0.44 : 0.32)],
    ["--ema-glass-border-strong", alpha(mix(borderStrong, "#FFFFFF", darkMode ? 0.35 : 0.55), darkMode ? 0.58 : 0.44)],
    ["--ema-glass-highlight", darkMode ? alpha("#FFFFFF", 0.14) : alpha("#FFFFFF", 0.65)],
    ["--ema-glass-highlight-2", darkMode ? alpha(accent, 0.16) : alpha(accent, 0.18)],
    ["--ema-glass-highlight-opacity", darkMode ? "0.22" : "0.48"],
    ["--ema-glass-shadow", darkMode ? "0 12px 28px rgba(0,0,0,0.42)" : "0 10px 24px rgba(16,24,40,0.14)"],
    ["--ema-glass-inner-shadow", darkMode ? "inset 0 1px 0 rgba(255,255,255,0.10), inset 0 -1px 0 rgba(255,255,255,0.04)" : "inset 0 1px 0 rgba(255,255,255,0.72), inset 0 -1px 0 rgba(255,255,255,0.28)"],
    ["--ema-glass-blur", isGlassVariant ? "10px" : "0px"],
    ["--ema-glass-saturate", isGlassVariant ? "1.2" : "1"],
    ["--ema-glass-opacity", isGlassVariant ? "0.82" : "1"],
    ["--ema-glass-radius", "12px"],
    ["--ema-glass-glow", alpha(accent, darkMode ? 0.16 : 0.22)],
    ["--ema-glass-edge", darkMode ? alpha("#FFFFFF", 0.16) : alpha("#FFFFFF", 0.82)],
    ["--ema-glass-chromatic", alpha(accent, darkMode ? 0.12 : 0.1)],
    ["--ema-glass-noise-opacity", isGlassVariant ? "0.035" : "0"],
    ["--ema-table-bg", alpha(surfaceSolid, darkMode ? 0.98 : 0.99)],
    ["--ema-table-row-bg", alpha(surfaceSolid, darkMode ? 0.98 : 0.99)],
    ["--ema-table-row-hover-bg", alpha(accent, darkMode ? 0.08 : 0.06)],
    ["--ema-table-border", border],
    ["--ema-table-text", text],
    ["--ema-code-bg", alpha(surfaceSolid, darkMode ? 0.98 : 0.99)],
    ["--ema-code-text", text],
    ["--ema-log-bg", alpha(surfaceSolid, darkMode ? 0.98 : 0.99)],
    ["--ema-log-text", text],
    ["--ema-success", semanticSuccess],
    ["--ema-success-soft", alpha(semanticSuccess, darkMode ? 0.16 : 0.14)],
    ["--ema-success-surface", alpha(semanticSuccess, darkMode ? 0.16 : 0.12)],
    ["--ema-success-border", alpha(semanticSuccess, darkMode ? 0.46 : 0.36)],
    ["--ema-warning", semanticWarning],
    ["--ema-warning-soft", alpha(semanticWarning, darkMode ? 0.18 : 0.16)],
    ["--ema-warning-surface", alpha(semanticWarning, darkMode ? 0.18 : 0.14)],
    ["--ema-warning-border", alpha(semanticWarning, darkMode ? 0.48 : 0.38)],
    ["--ema-danger", semanticDanger],
    ["--ema-danger-soft", alpha(semanticDanger, darkMode ? 0.16 : 0.14)],
    ["--ema-error", semanticDanger],
    ["--ema-error-surface", alpha(semanticDanger, darkMode ? 0.16 : 0.14)],
    ["--ema-error-border", alpha(semanticDanger, darkMode ? 0.48 : 0.38)],
    ["--ema-info", semanticInfo],
    ["--ema-info-soft", alpha(semanticInfo, darkMode ? 0.14 : 0.12)],
    ["--ema-info-surface", alpha(semanticInfo, darkMode ? 0.14 : 0.12)],
    ["--ema-info-border", alpha(semanticInfo, darkMode ? 0.44 : 0.34)],
    ["--ema-advisory", semanticAdvisory],
    ["--ema-advisory-soft", alpha(semanticAdvisory, darkMode ? 0.14 : 0.12)],
    ["--ema-advisory-surface", alpha(semanticAdvisory, darkMode ? 0.14 : 0.12)],
    ["--ema-evidence", semanticEvidence],
    ["--ema-evidence-soft", alpha(semanticEvidence, darkMode ? 0.14 : 0.12)],
    ["--ema-prototype", semanticPrototype],
    ["--ema-prototype-soft", alpha(semanticPrototype, darkMode ? 0.14 : 0.12)],
    ["--ema-prototype-surface", alpha(semanticPrototype, darkMode ? 0.14 : 0.12)],
    ["--ema-official", semanticOfficial],
    ["--ema-official-soft", alpha(semanticOfficial, darkMode ? 0.14 : 0.12)],
    ["--ema-blocked", semanticBlocked],
    ["--ema-blocked-soft", alpha(semanticBlocked, darkMode ? 0.16 : 0.14)],
    ["--ema-partial", semanticPartial],
    ["--ema-partial-soft", alpha(semanticPartial, darkMode ? 0.18 : 0.16)],
    ["--ema-local-demo", semanticLocalDemo],
    ["--ema-local-demo-soft", alpha(semanticLocalDemo, darkMode ? 0.14 : 0.12)],
    ["--ema-candidate", semanticEvidence],
    ["--ema-candidate-surface", alpha(semanticEvidence, darkMode ? 0.14 : 0.12)],
    ["--ema-not-production", semanticLocalDemo],
    ["--ema-not-compliance", semanticBlocked],
    ["--ema-chart-1", accent],
    ["--ema-chart-2", semanticInfo],
    ["--ema-chart-3", semanticWarning],
    ["--ema-chart-4", semanticAdvisory],
    ["--ema-chart-5", semanticDanger],
    ["--ema-chart-grid", darkMode ? alpha("#DDEDEA", 0.16) : alpha("#22302B", 0.14)],
    ["--ema-chart-axis", darkMode ? alpha("#EAF4F1", 0.84) : alpha("#162622", 0.72)],
    ["--ema-chart-tooltip-bg", darkMode ? adjustBrightness(surface3, 4) : "#FFFFFF"],
    ["--ema-chart-tooltip-text", text],
  ];

  for (const [name, value] of vars) {
    root.style.setProperty(name, value);
  }

  root.dataset.colorScheme = effectiveMode;
  return effectiveMode;
}

function hexToRgb(hex: string): [number, number, number] {
  const clean = hex.trim().replace("#", "");
  if (!/^[0-9a-fA-F]{6}$/.test(clean)) {
    return [128, 128, 128];
  }
  return [
    parseInt(clean.slice(0, 2), 16),
    parseInt(clean.slice(2, 4), 16),
    parseInt(clean.slice(4, 6), 16),
  ];
}

function rgbToHex(r: number, g: number, b: number): string {
  return `#${[r, g, b].map((x) => clamp(x).toString(16).padStart(2, "0")).join("")}`;
}

function adjustBrightness(hex: string, amount: number): string {
  const [r, g, b] = hexToRgb(hex);
  return rgbToHex(r + amount, g + amount, b + amount);
}

function desaturate(hex: string): string {
  const [r, g, b] = hexToRgb(hex);
  const gray = Math.round(r * 0.3 + g * 0.59 + b * 0.11);
  return rgbToHex(
    r + Math.round((gray - r) * 0.3),
    g + Math.round((gray - g) * 0.3),
    b + Math.round((gray - b) * 0.3),
  );
}
