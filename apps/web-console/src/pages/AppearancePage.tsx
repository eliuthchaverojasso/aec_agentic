import { useState } from "react";
import type React from "react";
import { AppearancePanel } from "../components/appearance/AppearancePanel";
import { useAppearanceSettings } from "../hooks/useAppearanceSettings";
import { ThemePreviewSurface } from "../components/ThemePreviewSurface";
import { defaultAppearance, type AmbientMotion, type CalibrationMode, type DiffMarkerStyle, type MotionPreference, type SidebarMode, type UiScale } from "../lib/appearance";
import { resolveEffectiveThemeMode } from "../lib/appearance";
import { THEME_PACK_KEYS, THEME_PACKS, THEME_VARIANTS, type ThemePackKey, type ThemeVariant } from "../lib/themePacks";

const visualThemes = ["minimalCorporate", "liquidGlass", "materialEngineering", "highContrast"] as const;
const accents = ["emaTeal", "gold", "slate", "blue", "graphite", "customLocal"] as const;
const densityOptions = ["comfortable", "compact", "dense"] as const;
const glassOptions = ["none", "subtle", "medium", "strong"] as const;
const sidebarOptions = ["expanded", "compact", "iconOnly", "auto"] as const;
const chartOptions = ["clean", "executive", "dense"] as const;
const ambientMotionOptions = ["off", "subtle", "normal", "vivid"] as const;
const calibrationModeOptions = ["operational", "strong", "extreme"] as const;

export function AppearancePage() {
  const {
    settings,
    resolvedColorScheme,
    updateAppearanceSetting,
    setAppearanceSettings,
    updateWhiteLabel,
    resetAppearanceSettings,
    exportAppearanceJson,
    importAppearanceJson,
    storageKey,
  } = useAppearanceSettings();
  const [importText, setImportText] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const effectiveMode = resolveEffectiveThemeMode(settings.colorScheme, settings.themeVariant);
  const pack = THEME_PACKS[settings.themePack];
  const operationalGlass = effectiveMode === "dark" ? Math.min(0.90, Math.max(0.58, settings.glassOpacity)) : Math.min(0.90, Math.max(0.72, settings.glassOpacity));
  const operationalData = effectiveMode === "dark" ? Math.max(0.92, settings.dataSurfaceOpacity) : Math.max(0.98, settings.dataSurfaceOpacity);
  const operationalBackground = Math.min(0.85, Math.max(0, settings.backgroundDepth));
  const operationalIntensity = Math.min(0.75, Math.max(0, settings.backgroundIntensity));
  const operationalVividness = Math.min(0.75, Math.max(0, settings.backgroundVividness));
  const operationalColorStrength = Math.min(0.75, Math.max(0, settings.backgroundColorStrength));
  const operationalMesh = Math.min(0.45, Math.max(0, settings.backgroundMeshOpacity));
  const operationalRefraction = Math.min(0.35, Math.max(0, settings.backgroundRefractionOpacity));
  const operationalPointer = Math.min(0.25, Math.max(0, settings.pointerLightStrength));
  const operationalCapsule = Math.min(0.9, Math.max(0.52, settings.capsuleOpacity));

  const normalizeTheme = () => {
    const nextVariant =
      settings.colorScheme === "light" && (settings.themeVariant === "Dark" || settings.themeVariant === "LiquidGlassDark")
        ? "Light"
        : settings.colorScheme === "dark" && (settings.themeVariant === "Light" || settings.themeVariant === "LiquidGlassLight")
          ? "Dark"
          : settings.themeVariant;
    updateAppearanceSetting("themeVariant", nextVariant as ThemeVariant);
    setMessage("Theme normalized to a coherent effective mode.");
  };

  const copyThemeJson = async () => {
    await navigator.clipboard.writeText(exportAppearanceJson());
    setMessage("Theme JSON copied.");
  };

  const resetGlassCalibration = () => {
    const darkDefaults = effectiveMode === "dark";
    setAppearanceSettings({
      ...settings,
      glassOpacity: darkDefaults ? 0.58 : 0.76,
      glassBlur: defaultAppearance.glassBlur,
      glassSaturate: defaultAppearance.glassSaturate,
      rimHighlight: defaultAppearance.rimHighlight,
      backgroundDepth: darkDefaults ? 0.76 : 0.68,
      backgroundIntensity: defaultAppearance.backgroundIntensity,
      backgroundVividness: defaultAppearance.backgroundVividness,
      backgroundColorStrength: defaultAppearance.backgroundColorStrength,
      backgroundMeshOpacity: defaultAppearance.backgroundMeshOpacity,
      backgroundRefractionOpacity: defaultAppearance.backgroundRefractionOpacity,
      ambientMotion: defaultAppearance.ambientMotion,
      ambientMotionSpeed: defaultAppearance.ambientMotionSpeed,
      ambientHueDrift: defaultAppearance.ambientHueDrift,
      calibrationMode: defaultAppearance.calibrationMode,
      pointerLightStrength: defaultAppearance.pointerLightStrength,
      dataSurfaceOpacity: defaultAppearance.dataSurfaceOpacity,
      capsuleOpacity: darkDefaults ? 0.68 : 0.78,
    });
    setMessage("Liquid Glass calibration reset for current mode.");
  };

  const copyCalibrationJson = async () => {
    await navigator.clipboard.writeText(JSON.stringify({
      schemaVersion: 1,
      exportedAt: new Date().toISOString(),
      product: "EMA AI",
      calibrationMode: settings.calibrationMode,
      glassOpacity: settings.glassOpacity,
      glassBlur: settings.glassBlur,
      glassSaturate: settings.glassSaturate,
      rimHighlight: settings.rimHighlight,
      backgroundDepth: settings.backgroundDepth,
      backgroundIntensity: settings.backgroundIntensity,
      backgroundVividness: settings.backgroundVividness,
      backgroundColorStrength: settings.backgroundColorStrength,
      backgroundMeshOpacity: settings.backgroundMeshOpacity,
      backgroundRefractionOpacity: settings.backgroundRefractionOpacity,
      ambientMotion: settings.ambientMotion,
      ambientMotionSpeed: settings.ambientMotionSpeed,
      ambientHueDrift: settings.ambientHueDrift,
      pointerLightStrength: settings.pointerLightStrength,
      dataSurfaceOpacity: settings.dataSurfaceOpacity,
      capsuleOpacity: settings.capsuleOpacity,
      effective: {
        glassOpacity: operationalGlass,
        dataSurfaceOpacity: operationalData,
        backgroundDepth: operationalBackground,
        backgroundIntensity: operationalIntensity,
        backgroundVividness: operationalVividness,
        backgroundColorStrength: operationalColorStrength,
        backgroundMeshOpacity: operationalMesh,
        backgroundRefractionOpacity: operationalRefraction,
        pointerLightStrength: operationalPointer,
        capsuleOpacity: operationalCapsule,
      },
    }, null, 2));
    setMessage("Liquid Glass calibration JSON copied.");
  };

  const importTheme = () => {
    const result = importAppearanceJson(importText);
    setMessage(result.ok ? "Theme imported." : `Import failed: ${result.error}`);
  };

  return (
    <section className="ema-page ema-page-shell space-y-5">
      <AppearancePanel
        effectiveMode={effectiveMode}
        themePackLabel={pack?.name ?? settings.themePack}
        themeVariant={settings.themeVariant}
        glassIntensity={settings.glassIntensity}
        liquidGlassActive={settings.visualTheme === "liquidGlass" || settings.themeVariant === "LiquidGlassLight" || settings.themeVariant === "LiquidGlassDark"}
      />

      <section className="ema-glass-hero ema-anim-soft-rise p-5">
        <div className="grid gap-5 lg:grid-cols-[1.35fr_0.65fr]">
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide text-muted">Liquid Glass Material Preview</div>
            <h3 className="mt-2 text-2xl font-semibold text-ink">EMA AI Dashboard</h3>
            <p className="mt-2 max-w-2xl text-sm text-muted">
              A local-only control center for display preferences. Glass is applied to approved surfaces only; logs,
              JSON, warnings, and dense tables remain solid for readability.
            </p>
            <div className="mt-4 flex flex-wrap gap-2 text-xs font-semibold">
              <span className="ema-status-badge text-ink">Effective: {effectiveMode}</span>
              <span className="ema-status-badge text-ink">{pack?.name ?? settings.themePack}</span>
              <span className="ema-status-badge text-ink">{settings.themeVariant}</span>
              <span className="ema-status-badge text-ink">Glass: {settings.glassIntensity}</span>
              {settings.visualTheme === "liquidGlass" || settings.themeVariant === "LiquidGlassLight" || settings.themeVariant === "LiquidGlassDark" ? (
                <>
                  <span className="ema-status-badge text-ink">Ambient depth active</span>
                  <span className="ema-status-badge text-ink">Pointer light on</span>
                </>
              ) : null}
            </div>
          </div>
          <div className="ema-glass-card ema-glass-surface-strong ema-anim-glass-shimmer-once p-4">
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm font-semibold text-ink">Preview shell</span>
              <span className="h-4 w-4 rounded-full border border-line" style={{ background: "var(--ema-accent)" }} />
            </div>
            <div className="mt-4 space-y-2">
              <div className="ema-glass-capsule ema-status-badge h-8 px-3 py-1 text-sm text-muted">Sidebar capsule</div>
              <div className="ema-glass-toolbar ema-glass-control h-8 px-3 py-1 text-sm text-muted">Topbar control</div>
              <div className="ema-readable-surface rounded-lg p-3 text-xs text-muted">
                Solid data surfaces stay crisp under every theme.
              </div>
            </div>
            {(settings.visualTheme === "liquidGlass" || settings.themeVariant === "LiquidGlassLight" || settings.themeVariant === "LiquidGlassDark") ? (
              <p className="mt-3 text-xs text-accent">
                Move your pointer over this card to see the optical surface reaction.
              </p>
            ) : null}
          </div>
        </div>
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.05fr_0.95fr]">
        <div className="space-y-5">
          <SettingsCard title="Theme Configuration" subtitle="Palette, brightness, surface behavior, and normalized effective mode.">
            <div className="grid gap-4 md:grid-cols-2">
              <SegmentedControl
                label="Color Scheme"
                value={settings.colorScheme}
                options={[
                  ["light", "Light"],
                  ["dark", "Dark"],
                  ["system", `System (${resolvedColorScheme})`],
                ]}
                onChange={(value) => updateAppearanceSetting("colorScheme", value as typeof settings.colorScheme)}
              />
              <SelectControl label="Visual Theme" value={settings.visualTheme} options={[...visualThemes]} onChange={(value) => updateAppearanceSetting("visualTheme", value as typeof settings.visualTheme)} />
              <ThemePackSelect value={settings.themePack} onChange={(value) => updateAppearanceSetting("themePack", value as ThemePackKey)} />
              <SelectControl label="Theme Variant" value={settings.themeVariant} options={THEME_VARIANTS} onChange={(value) => updateAppearanceSetting("themeVariant", value as ThemeVariant)} />
              <SelectControl label="Accent Profile" value={settings.accent} options={[...accents]} onChange={(value) => updateAppearanceSetting("accent", value as typeof settings.accent)} />
              <div className="rounded-lg border border-line bg-surface-2 p-3">
                <div className="text-xs font-semibold uppercase tracking-wide text-muted">Effective Mode</div>
                <div className="mt-1 text-lg font-semibold text-ink">{effectiveMode}</div>
                <p className="mt-1 text-xs text-muted">Effective mode controls the actual applied token set.</p>
              </div>
            </div>
            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" className="ema-btn-secondary" onClick={normalizeTheme}>Normalize Theme</button>
              <button type="button" className="ema-btn-ghost" onClick={resetAppearanceSettings}>Reset to Defaults</button>
            </div>
          </SettingsCard>

          <SettingsCard title="Glass Material" subtitle="Optical depth without sacrificing data readability.">
            <div className="grid gap-4 md:grid-cols-2">
              <SelectControl label="Glass Intensity" value={settings.glassIntensity} options={[...glassOptions]} onChange={(value) => updateAppearanceSetting("glassIntensity", value as typeof settings.glassIntensity)} />
              <ToggleControl label="Translucent sidebar" help="Affects the sidebar surface only." checked={settings.translucentSidebar} onChange={(checked) => updateAppearanceSetting("translucentSidebar", checked)} />
            </div>
            <div className="ema-glass-calibration-panel mt-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="text-sm font-semibold text-ink">Liquid Glass Calibration</div>
                  <p className="mt-1 text-xs text-muted">Tune opacity, blur, background depth, and optical edge strength.</p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <button type="button" className="ema-btn-secondary text-xs" onClick={resetGlassCalibration}>Reset Calibration</button>
                  <button type="button" className="ema-btn-ghost text-xs" onClick={copyCalibrationJson}>Copy Calibration JSON</button>
                </div>
              </div>
              <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <CalibrationSlider label="Glass Opacity" value={settings.glassOpacity} min={0.3} max={0.98} step={0.01} valueLabel={settings.glassOpacity.toFixed(2)} onChange={(value) => updateAppearanceSetting("glassOpacity", value)} />
                <CalibrationSlider label="Glass Blur" value={settings.glassBlur} min={0} max={40} step={1} valueLabel={`${settings.glassBlur.toFixed(0)}px`} onChange={(value) => updateAppearanceSetting("glassBlur", value)} />
                <CalibrationSlider label="Saturation" value={settings.glassSaturate} min={0.8} max={2.4} step={0.01} valueLabel={settings.glassSaturate.toFixed(2)} onChange={(value) => updateAppearanceSetting("glassSaturate", value)} />
                <CalibrationSlider label="Rim Highlight" value={settings.rimHighlight} min={0} max={1} step={0.01} valueLabel={settings.rimHighlight.toFixed(2)} onChange={(value) => updateAppearanceSetting("rimHighlight", value)} />
                <CalibrationSlider label="Background Depth" value={settings.backgroundDepth} min={0} max={1.5} step={0.01} valueLabel={settings.backgroundDepth.toFixed(2)} onChange={(value) => updateAppearanceSetting("backgroundDepth", value)} />
                <CalibrationSlider label="Background Intensity" value={settings.backgroundIntensity} min={0} max={3} step={0.01} valueLabel={settings.backgroundIntensity.toFixed(2)} onChange={(value) => updateAppearanceSetting("backgroundIntensity", value)} />
                <CalibrationSlider label="Pointer Light" value={settings.pointerLightStrength} min={0} max={1.5} step={0.01} valueLabel={settings.pointerLightStrength.toFixed(2)} onChange={(value) => updateAppearanceSetting("pointerLightStrength", value)} />
                <CalibrationSlider label="Data Surface" value={settings.dataSurfaceOpacity} min={0.8} max={1} step={0.01} valueLabel={settings.dataSurfaceOpacity.toFixed(2)} onChange={(value) => updateAppearanceSetting("dataSurfaceOpacity", value)} />
                <CalibrationSlider label="Capsule Opacity" value={settings.capsuleOpacity} min={0.35} max={1} step={0.01} valueLabel={settings.capsuleOpacity.toFixed(2)} onChange={(value) => updateAppearanceSetting("capsuleOpacity", value)} />
              </div>
              <div className="ema-calibration-strip mt-4">
                {[
                  ["Opacity", settings.glassOpacity.toFixed(2), settings.glassOpacity],
                  ["Blur", `${settings.glassBlur.toFixed(0)}px`, settings.glassBlur / 40],
                  ["Saturate", settings.glassSaturate.toFixed(2), (settings.glassSaturate - 0.8) / 1.6],
                  ["Rim", settings.rimHighlight.toFixed(2), settings.rimHighlight],
                  ["Depth", settings.backgroundDepth.toFixed(2), settings.backgroundDepth],
                  ["Intensity", settings.backgroundIntensity.toFixed(2), settings.backgroundIntensity / 3],
                  ["Vivid", settings.backgroundVividness.toFixed(2), settings.backgroundVividness / 3],
                  ["Color", settings.backgroundColorStrength.toFixed(2), settings.backgroundColorStrength / 3],
                  ["Mesh", settings.backgroundMeshOpacity.toFixed(2), settings.backgroundMeshOpacity / 0.8],
                  ["Refract", settings.backgroundRefractionOpacity.toFixed(2), settings.backgroundRefractionOpacity / 1],
                  ["Speed", settings.ambientMotionSpeed.toFixed(2), settings.ambientMotionSpeed / 1.5],
                  ["Hue", `${settings.ambientHueDrift > 0 ? "+" : ""}${settings.ambientHueDrift}°`, Math.abs(settings.ambientHueDrift) / 90],
                  ["Pointer", settings.pointerLightStrength.toFixed(2), settings.pointerLightStrength / 1.5],
                  ["Data", settings.dataSurfaceOpacity.toFixed(2), settings.dataSurfaceOpacity],
                  ["Capsule", settings.capsuleOpacity.toFixed(2), settings.capsuleOpacity],
                  ["Mode", settings.calibrationMode, settings.calibrationMode === "extreme" ? 1 : settings.calibrationMode === "strong" ? 0.65 : 0.35],
                ].map(([label, value, amount]) => (
                  <div key={label} className="ema-calibration-chip">
                    <span>{label}</span>
                    <strong>{value}</strong>
                    <i style={{ width: `${Math.max(8, Number(amount) * 100)}%` }} />
                  </div>
                ))}
              </div>
              <div className="ema-transparency-proof mt-4">
                <div className="flex flex-wrap items-end justify-between gap-2">
                  <div>
                    <div className="text-xs font-semibold uppercase tracking-wide text-muted">Transparency Proof</div>
                    <p className="mt-1 text-xs text-muted">At low opacity, the gradient and grid behind glass must remain visible.</p>
                  </div>
                  <span className="ema-status-badge">Glass {settings.glassOpacity.toFixed(2)}</span>
                </div>
                <div className="ema-transparency-proof-field mt-3">
                  {[
                    ["glass-surface-subtle", "Glass subtle", "background should show"],
                    ["glass-surface-medium", "Glass medium", "calibrated opacity"],
                    ["glass-surface-strong", "Glass strong", "rim and depth"],
                    ["data-safe-surface", "Data-safe", "mostly solid"],
                  ].map(([className, label, note]) => (
                    <div key={label} className={`ema-transparency-proof-block ${className}`}>
                      <strong>{label}</strong>
                      <span>{note}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
            <div className="ema-glass-material-lab mt-4">
              <div className="ema-glass-demo-card">
                <div className="ema-glass-demo-noise" />
                <div className="ema-glass-demo-surface is-transparent">
                  <div className="text-xs font-semibold uppercase tracking-wide text-muted">Transparency</div>
                  <div className="mt-1 text-sm font-semibold text-ink">Background remains visible through glass.</div>
                  <div className="mt-3 grid grid-cols-3 gap-2">
                    <span className="ema-glass-demo-chip">Subtle</span>
                    <span className="ema-glass-demo-chip">Medium</span>
                    <span className="ema-glass-demo-chip">Strong</span>
                  </div>
                </div>
              </div>
              <div className="ema-glass-demo-card">
                <div className="ema-glass-demo-noise is-dense" />
                <div className="ema-glass-demo-surface is-blur">
                  <div className="text-xs font-semibold uppercase tracking-wide text-muted">Blur / Saturate</div>
                  <div className="mt-1 text-sm font-semibold text-ink">{settings.glassIntensity === "none" ? "Disabled" : "Surface-only refraction"}</div>
                  <p className="mt-2 text-xs text-muted">The visual texture behind this sample diffuses while text stays crisp.</p>
                </div>
              </div>
              <div className="ema-glass-demo-card">
                <div className="ema-glass-demo-shell">
                  <div className={`ema-glass-demo-sidebar ${settings.translucentSidebar ? "is-translucent" : ""}`}>
                    <span />
                    <span />
                    <span />
                  </div>
                  <div className="ema-glass-demo-main">
                    <div className="text-xs font-semibold uppercase tracking-wide text-muted">Highlight / Sidebar</div>
                    <div className="mt-1 text-sm font-semibold text-ink">Rim light and tactile capsules.</div>
                    <div className="mt-3 h-2 rounded-full bg-accent" style={{ width: settings.glassIntensity === "strong" ? "86%" : settings.glassIntensity === "medium" ? "68%" : settings.glassIntensity === "subtle" ? "48%" : "22%" }} />
                  </div>
                </div>
              </div>
            </div>
          </SettingsCard>

          <SettingsCard title="Dynamic Ambient Background" subtitle="Control color depth, gradient movement, mesh visibility, and calibration strength.">
            <div className="grid gap-4 md:grid-cols-2">
              <CalibrationSlider label="Gradient Vividness" value={settings.backgroundVividness} min={0} max={3} step={0.01} valueLabel={settings.backgroundVividness.toFixed(2)} onChange={(value) => updateAppearanceSetting("backgroundVividness", value)} />
              <CalibrationSlider label="Color Strength" value={settings.backgroundColorStrength} min={0} max={3} step={0.01} valueLabel={settings.backgroundColorStrength.toFixed(2)} onChange={(value) => updateAppearanceSetting("backgroundColorStrength", value)} />
              <CalibrationSlider label="Mesh / Grid" value={settings.backgroundMeshOpacity} min={0} max={0.8} step={0.01} valueLabel={settings.backgroundMeshOpacity.toFixed(2)} onChange={(value) => updateAppearanceSetting("backgroundMeshOpacity", value)} />
              <CalibrationSlider label="Refraction Field" value={settings.backgroundRefractionOpacity} min={0} max={1} step={0.01} valueLabel={settings.backgroundRefractionOpacity.toFixed(2)} onChange={(value) => updateAppearanceSetting("backgroundRefractionOpacity", value)} />
              <SelectControl label="Ambient Motion" value={settings.ambientMotion} options={[...ambientMotionOptions]} onChange={(value) => updateAppearanceSetting("ambientMotion", value as AmbientMotion)} />
              <CalibrationSlider label="Motion Speed" value={settings.ambientMotionSpeed} min={0} max={1.5} step={0.01} valueLabel={settings.ambientMotionSpeed.toFixed(2)} onChange={(value) => updateAppearanceSetting("ambientMotionSpeed", value)} />
              <CalibrationSlider label="Hue Drift" value={settings.ambientHueDrift} min={-90} max={90} step={1} valueLabel={`${settings.ambientHueDrift > 0 ? "+" : ""}${settings.ambientHueDrift}°`} onChange={(value) => updateAppearanceSetting("ambientHueDrift", value)} />
              <SelectControl label="Calibration Mode" value={settings.calibrationMode} options={[...calibrationModeOptions]} onChange={(value) => updateAppearanceSetting("calibrationMode", value as CalibrationMode)} />
            </div>
            <p className="mt-3 text-xs text-muted">
              Operational routes are clamped for readability. Strong/Extreme QA affects preview and the live board. Data-safe tables, logs, and code remain solid.
            </p>
          </SettingsCard>

          <SettingsCard title="Layout & Density" subtitle="Tune scanning density and shell behavior for demo rooms or operator workstations.">
            <div className="grid gap-4 md:grid-cols-2">
              <SelectControl label="Density" value={settings.density} options={[...densityOptions]} onChange={(value) => updateAppearanceSetting("density", value as typeof settings.density)} />
              <SelectControl label="Sidebar Mode" value={settings.sidebarMode} options={[...sidebarOptions]} onChange={(value) => updateAppearanceSetting("sidebarMode", value as SidebarMode)} />
              <SelectControl label="Chart Style" value={settings.chartStyle} options={[...chartOptions]} onChange={(value) => updateAppearanceSetting("chartStyle", value as typeof settings.chartStyle)} />
              <SelectControl label="UI Scale" value={String(settings.uiScale)} options={["90", "100", "110", "125"]} onChange={(value) => updateAppearanceSetting("uiScale", Number(value) as UiScale)} />
              <ToggleControl label="Pointer cursors" help="Shows clickable affordances on interactive controls." checked={settings.usePointerCursors} onChange={(checked) => updateAppearanceSetting("usePointerCursors", checked)} />
            </div>
            <p className="mt-3 text-xs text-muted">
              Density, UI Scale, and Pointer cursors apply live. Sidebar Mode auto currently matches Expanded (pending responsive behavior). Chart Style is preview-only — it styles the Theme Preview chart, not operational dashboards yet.
            </p>
          </SettingsCard>

          <SettingsCard title="Typography" subtitle="Local font preferences. No font files or credentials are stored.">
            <div className="grid gap-4 md:grid-cols-2">
              <TextFieldControl label="UI Font" value={settings.uiFont} onChange={(value) => updateAppearanceSetting("uiFont", value)} />
              <TextFieldControl label="Code Font" value={settings.codeFont} onChange={(value) => updateAppearanceSetting("codeFont", value)} />
            </div>
            <div className="mt-4 rounded-lg border border-line bg-surface-solid p-4">
              <div className="text-sm font-semibold text-ink">Engineering readiness dashboard</div>
              <code className="mt-2 block rounded-md border border-line bg-surface-2 p-3 text-xs text-ink" style={{ fontFamily: "var(--ema-code-font)" }}>
                const readiness = 87;
              </code>
            </div>
          </SettingsCard>
        </div>

        <div className="space-y-5">
          <SettingsCard title="Motion & Interaction" subtitle="Reduced motion disables pointer light, pulses, shimmer, and large transitions.">
            <SegmentedControl
              label="Motion"
              value={settings.motion}
              options={[
                ["system", "System"],
                ["normal", "Normal"],
                ["reduced", "Reduced"],
              ]}
              onChange={(value) => updateAppearanceSetting("motion", value as MotionPreference)}
            />
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <SelectControl label="Diff Markers" value={settings.diffMarkers} options={["color", "symbols", "both"]} onChange={(value) => updateAppearanceSetting("diffMarkers", value as DiffMarkerStyle)} />
              <SliderControl label="Contrast" value={settings.contrast} onChange={(value) => updateAppearanceSetting("contrast", value)} />
            </div>
            {(settings.visualTheme === "liquidGlass" || settings.themeVariant === "LiquidGlassLight" || settings.themeVariant === "LiquidGlassDark") ? (
              <p className="mt-4 text-xs text-muted">
                Pointer-responsive optical light is active on glass surfaces. Move your cursor across the dashboard to see the ambient highlight track your pointer. Reduced motion disables this effect.
              </p>
            ) : null}
          </SettingsCard>

          <SettingsCard title="White Label" subtitle="Demo branding only. This does not change backend identity or readiness data.">
            <div className="grid gap-4 md:grid-cols-2">
              <TextFieldControl label="Organization Name" value={settings.whiteLabel.organizationName} onChange={(value) => updateWhiteLabel("organizationName", value)} />
              <TextFieldControl label="Dashboard Title" value={settings.whiteLabel.dashboardTitle} onChange={(value) => updateWhiteLabel("dashboardTitle", value)} />
              <TextFieldControl label="Logo URL" value={settings.whiteLabel.logoUrl} onChange={(value) => updateWhiteLabel("logoUrl", value)} />
              <TextFieldControl label="Footer Text" value={settings.whiteLabel.footerText} onChange={(value) => updateWhiteLabel("footerText", value)} />
              <ToggleControl label="Local Demo watermark" help="Keeps the demo boundary visible." checked={settings.whiteLabel.localDemoWatermark} onChange={(checked) => updateWhiteLabel("localDemoWatermark", checked)} />
            </div>
            <p className="mt-3 text-xs text-muted">
              Organization Name and Dashboard Title appear on the Audit Export card. Logo URL and Footer Text are persisted but pending (no live topbar/footer consumer yet). Local Demo watermark is applied live.
            </p>
          </SettingsCard>

          <SettingsCard title="Semantic Labels" subtitle="These labels protect the meaning of prototype, advisory, and evidence candidate data.">
            <div className="grid gap-3">
              <ToggleControl label="Prototype labels" help="Marks incomplete or future-facing surfaces." checked={settings.showPrototypeLabels} onChange={(checked) => updateAppearanceSetting("showPrototypeLabels", checked)} />
              <ToggleControl label="Evidence Candidate labels" help="Separates indexed candidates from official evidence." checked={settings.showEvidenceCandidateLabels} onChange={(checked) => updateAppearanceSetting("showEvidenceCandidateLabels", checked)} />
              <ToggleControl label="Advisory labels" help="Keeps AI/SEION suggestions advisory-only." checked={settings.showAdvisoryLabels} onChange={(checked) => updateAppearanceSetting("showAdvisoryLabels", checked)} />
              <ToggleControl label="Fallback labels" help="Marks demo fallback data clearly." checked={settings.showFallbackLabels} onChange={(checked) => updateAppearanceSetting("showFallbackLabels", checked)} />
              <ToggleControl label="Local Demo labels" help="Keeps non-production status visible." checked={settings.showLocalDemoLabels} onChange={(checked) => updateAppearanceSetting("showLocalDemoLabels", checked)} />
            </div>
          </SettingsCard>

          <SettingsCard title="Import / Export" subtitle="Only non-sensitive UI preferences are persisted.">
            <div className="flex flex-wrap gap-2">
              <button type="button" className="ema-btn-primary" onClick={copyThemeJson}>Copy theme JSON</button>
              <button type="button" className="ema-btn-secondary" onClick={importTheme}>Import theme JSON</button>
            </div>
            <p className="mt-3 text-xs text-muted">LocalStorage key: <code className="font-mono text-ink">{storageKey}</code></p>
            <textarea
              className="ema-textarea mt-3 min-h-28 w-full p-3 text-xs"
              placeholder="Paste appearance JSON to import"
              value={importText}
              onChange={(event) => setImportText(event.target.value)}
            />
            {message ? <p className="mt-2 text-xs font-semibold text-accent">{message}</p> : null}
          </SettingsCard>
        </div>
      </section>

      <SettingsCard title="Settings Diagnostics" subtitle="Proves settings are wired to CSS variables and route clamping.">
        <div className="grid gap-2 text-xs sm:grid-cols-2 lg:grid-cols-4">
          {[
            ["Effective Mode", effectiveMode],
            ["Theme", pack?.name || settings.themePack],
            ["Variant", settings.themeVariant],
            ["Accent", settings.accent],
            ["Density", settings.density],
            ["UI Scale", `${settings.uiScale}%`],
            ["Motion", settings.motion],
            ["Reduced Motion", document.documentElement.dataset.motion === "reduced" ? "Yes" : "No"],
            ["Glass Raw", settings.glassOpacity.toFixed(2)],
            ["Glass Effective", operationalGlass.toFixed(2)],
            ["Blur Raw", `${settings.glassBlur.toFixed(0)}px`],
            ["Blur Effective", `${Math.min(14, settings.glassBlur).toFixed(0)}px`],
            ["Background Raw", settings.backgroundIntensity.toFixed(2)],
            ["Background Effective", operationalIntensity.toFixed(2)],
            ["Depth Raw", settings.backgroundDepth.toFixed(2)],
            ["Depth Effective", operationalBackground.toFixed(2)],
            ["Data Raw", settings.dataSurfaceOpacity.toFixed(2)],
            ["Data Effective", operationalData.toFixed(2)],
            ["Pointer Raw", settings.pointerLightStrength.toFixed(2)],
            ["Pointer Effective", operationalPointer.toFixed(2)],
            ["Calibration", settings.calibrationMode],
            ["Route Clamp", document.querySelector("[data-route-kind]")?.getAttribute("data-route-kind") || "none"],
            ["LocalStorage", "Available"],
            ["Schema", "v1"],
          ].map(([label, value]) => (
            <div key={label} className="rounded border border-line bg-surface-2 p-2">
              <div className="font-semibold text-muted">{label}</div>
              <div className="mt-0.5 font-mono text-ink">{value}</div>
            </div>
          ))}
        </div>
      </SettingsCard>

      <ThemePreviewSurface currentPack={settings.themePack} currentVariant={settings.themeVariant} />
    </section>
  );
}

function SettingsCard({ title, subtitle, children }: { title: string; subtitle: string; children: React.ReactNode }) {
  return (
    <section className="ema-card p-5">
      <h3 className="text-base font-semibold text-ink">{title}</h3>
      <p className="mt-1 text-sm text-muted">{subtitle}</p>
      <div className="mt-4">{children}</div>
    </section>
  );
}

function SelectControl({ label, value, options, onChange }: { label: string; value: string; options: readonly string[]; onChange: (value: string) => void }) {
  return (
    <label className="block">
      <span className="ema-field-label">{label}</span>
      <select className="ema-select mt-1.5 w-full px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)}>
        {options.map((option) => <option key={option} value={option}>{formatOption(option)}</option>)}
      </select>
    </label>
  );
}

function ThemePackSelect({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <label className="block">
      <span className="ema-field-label">Theme Pack</span>
      <select className="ema-select mt-1.5 w-full px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)}>
        {THEME_PACK_KEYS.map((key) => {
          const pack = THEME_PACKS[key];
          return <option key={key} value={key}>{pack?.name || key} ({pack?.mode || "Adaptive"})</option>;
        })}
      </select>
    </label>
  );
}

function SegmentedControl<T extends string>({ label, value, options, onChange }: { label: string; value: T; options: Array<[T, string]>; onChange: (value: T) => void }) {
  return (
    <div>
      <span className="ema-field-label">{label}</span>
      <div className="ema-segmented mt-1.5 p-1">
        {options.map(([optionValue, labelText]) => (
          <button key={optionValue} type="button" className={`ema-segmented-option ${value === optionValue ? "is-active" : ""}`} onClick={() => onChange(optionValue)}>
            {labelText}
          </button>
        ))}
      </div>
    </div>
  );
}

function ToggleControl({ label, help, checked, onChange }: { label: string; help?: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-line bg-surface-2 p-3">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} className="ema-checkbox mt-0.5 h-4 w-4" />
      <span>
        <span className="block text-sm font-semibold text-ink">{label}</span>
        {help ? <span className="mt-0.5 block text-xs text-muted">{help}</span> : null}
      </span>
    </label>
  );
}

function TextFieldControl({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="block">
      <span className="ema-field-label">{label}</span>
      <input type="text" className="ema-input mt-1.5 w-full px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function SliderControl({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return (
    <label className="block">
      <span className="flex items-center justify-between gap-3">
        <span className="ema-field-label">{label}</span>
        <span className="ema-field-help">{value}%</span>
      </span>
      <input type="range" min="0" max="100" value={value} onChange={(event) => onChange(Number(event.target.value))} className="ema-slider mt-2 w-full" />
    </label>
  );
}

function CalibrationSlider({
  label,
  value,
  min,
  max,
  step,
  valueLabel,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step: number;
  valueLabel: string;
  onChange: (value: number) => void;
}) {
  return (
    <label className="ema-calibration-control">
      <span className="flex items-center justify-between gap-3">
        <span className="ema-field-label">{label}</span>
        <span className="ema-field-help font-mono">{valueLabel}</span>
      </span>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        className="ema-slider mt-2 w-full"
      />
    </label>
  );
}

function formatOption(value: string): string {
  if (value === "extreme") {
    return "Extreme QA";
  }
  return value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
    .replace(/\b\w/g, (char) => char.toUpperCase());
}
