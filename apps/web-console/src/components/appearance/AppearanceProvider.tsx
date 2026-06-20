import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import {
  APPEARANCE_STORAGE_KEY,
  APPEARANCE_LEGACY_STORAGE_KEYS,
  defaultAppearance,
  type AppearanceSettings,
  applyAppearanceSettings,
  initializeAppearance,
  resolveColorScheme,
  sanitizeAppearanceInput,
  type ResolvedColorScheme,
  writeAppearanceSettings,
} from "../../lib/appearance";

type AppearanceContextValue = {
  settings: AppearanceSettings;
  resolvedColorScheme: ResolvedColorScheme;
  updateAppearanceSetting: <K extends keyof AppearanceSettings>(key: K, value: AppearanceSettings[K]) => void;
  setAppearanceSettings: (next: AppearanceSettings) => void;
  updateWhiteLabel: <K extends keyof AppearanceSettings["whiteLabel"]>(
    key: K,
    value: AppearanceSettings["whiteLabel"][K],
  ) => void;
  resetAppearanceSettings: () => void;
  exportAppearanceJson: () => string;
  importAppearanceJson: (json: string) => { ok: boolean; error?: string };
  storageKey: string;
};

const AppearanceContext = createContext<AppearanceContextValue | null>(null);

export function AppearanceProvider({ children }: { children: ReactNode }) {
  const initialSettings = useMemo(() => initializeAppearance(), []);
  const [settings, setSettings] = useState<AppearanceSettings>(initialSettings);
  const [resolvedColorScheme, setResolvedColorScheme] = useState<ResolvedColorScheme>(() =>
    resolveColorScheme(initialSettings.colorScheme),
  );

  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => {
      if (settings.colorScheme === "system") {
        setResolvedColorScheme(applyAppearanceSettings(settings));
      }
    };
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, [settings]);

  useEffect(() => {
    const onStorage = (event: StorageEvent) => {
      if (event.storageArea !== window.localStorage) {
        return;
      }
      const storageKey = event.key ?? "";
      const isKnownKey =
        storageKey === APPEARANCE_STORAGE_KEY ||
        APPEARANCE_LEGACY_STORAGE_KEYS.some((legacyKey) => legacyKey === storageKey);
      if (!isKnownKey) {
        return;
      }

      if (!event.newValue) {
        setSettings(defaultAppearance);
        setResolvedColorScheme(applyAppearanceSettings(defaultAppearance));
        return;
      }

      try {
        const next = sanitizeAppearanceInput(JSON.parse(event.newValue));
        setSettings(next);
        setResolvedColorScheme(applyAppearanceSettings(next));
      } catch {
        setSettings(defaultAppearance);
        setResolvedColorScheme(applyAppearanceSettings(defaultAppearance));
      }
    };

    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const setAppearanceSettings = useCallback((next: AppearanceSettings) => {
    const sanitized = sanitizeAppearanceInput(next);
    writeAppearanceSettings(sanitized);
    setResolvedColorScheme(applyAppearanceSettings(sanitized));
    setSettings(sanitized);
  }, []);

  const updateAppearanceSetting = useCallback(
    <K extends keyof AppearanceSettings>(key: K, value: AppearanceSettings[K]) => {
      setAppearanceSettings({ ...settings, [key]: value });
    },
    [settings, setAppearanceSettings],
  );

  const updateWhiteLabel = useCallback(
    <K extends keyof AppearanceSettings["whiteLabel"]>(key: K, value: AppearanceSettings["whiteLabel"][K]) => {
      setAppearanceSettings({
        ...settings,
        whiteLabel: { ...settings.whiteLabel, [key]: value },
      });
    },
    [settings, setAppearanceSettings],
  );

  const resetAppearanceSettings = useCallback(() => {
    setAppearanceSettings(defaultAppearance);
  }, [setAppearanceSettings]);

  const exportAppearanceJson = useCallback(
    () =>
      JSON.stringify(
        {
          schemaVersion: 1,
          exportedAt: new Date().toISOString(),
          product: "EMA AI",
          settings,
        },
        null,
        2,
      ),
    [settings],
  );

  const importAppearanceJson = useCallback(
    (json: string): { ok: boolean; error?: string } => {
      try {
        const parsed = JSON.parse(json) as Record<string, unknown>;
        const payload = parsed.schemaVersion ? parsed.settings || parsed : parsed;
        setAppearanceSettings(sanitizeAppearanceInput(payload));
        return { ok: true };
      } catch (error) {
        return { ok: false, error: error instanceof Error ? error.message : "Invalid JSON." };
      }
    },
    [setAppearanceSettings],
  );

  const value = useMemo<AppearanceContextValue>(
    () => ({
      settings,
      resolvedColorScheme,
      updateAppearanceSetting,
      setAppearanceSettings,
      updateWhiteLabel,
      resetAppearanceSettings,
      exportAppearanceJson,
      importAppearanceJson,
      storageKey: APPEARANCE_STORAGE_KEY,
    }),
    [
      exportAppearanceJson,
      importAppearanceJson,
      resetAppearanceSettings,
      resolvedColorScheme,
      setAppearanceSettings,
      settings,
      updateAppearanceSetting,
      updateWhiteLabel,
    ],
  );

  return <AppearanceContext.Provider value={value}>{children}</AppearanceContext.Provider>;
}

export function useAppearance() {
  const context = useContext(AppearanceContext);
  if (!context) {
    throw new Error("useAppearance must be used within an AppearanceProvider.");
  }
  return context;
}

export type { AppearanceContextValue };
