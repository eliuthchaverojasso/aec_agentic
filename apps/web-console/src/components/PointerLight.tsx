import { useEffect, useRef } from "react";

const POINTER_X = "--ema-pointer-x";
const POINTER_Y = "--ema-pointer-y";
const POINTER_INTENSITY = "--ema-pointer-intensity";
const POINTER_ACTIVE = "--ema-pointer-active";
const LIQUID_VARIANTS = new Set(["liquid_glass_light", "liquid_glass_dark"]);

function isGlassCapable(root: HTMLElement): boolean {
  const visualTheme = root.dataset.visualTheme ?? "";
  const themeVariant = root.dataset.themeVariant ?? "";
  const glassMode = root.dataset.glass ?? "";
  const motion = root.dataset.motion ?? "";
  if (visualTheme === "high-contrast" || glassMode === "none" || motion === "reduced") {
    return false;
  }
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    return false;
  }
  if (window.matchMedia("(forced-colors: active)").matches) {
    return false;
  }
  if (window.matchMedia("(prefers-contrast: more)").matches) {
    return false;
  }
  return visualTheme === "liquid-glass" || LIQUID_VARIANTS.has(themeVariant);
}

export function PointerLight() {
  const rafId = useRef<number>(0);
  const active = useRef(false);
  const enabled = useRef(false);
  const latestX = useRef(50);
  const latestY = useRef(50);

  useEffect(() => {
    const root = document.documentElement;

    const applyIdleState = () => {
      root.style.setProperty(POINTER_INTENSITY, "0");
      root.style.setProperty(POINTER_ACTIVE, "0");
      active.current = false;
    };

    const applyCenter = () => {
      root.style.setProperty(POINTER_X, "50%");
      root.style.setProperty(POINTER_Y, "50%");
    };

    const refreshEnabled = () => {
      enabled.current = isGlassCapable(root);
      if (!enabled.current) {
        applyIdleState();
      }
      return enabled.current;
    };

    applyCenter();
    applyIdleState();
    refreshEnabled();

    const onMove = (event: PointerEvent) => {
      if (event.pointerType !== "mouse" || !enabled.current) return;
      latestX.current = (event.clientX / window.innerWidth) * 100;
      latestY.current = (event.clientY / window.innerHeight) * 100;
      if (rafId.current) return;
      rafId.current = window.requestAnimationFrame(() => {
        rafId.current = 0;
        if (!enabled.current) return;
        root.style.setProperty(POINTER_X, `${latestX.current.toFixed(1)}%`);
        root.style.setProperty(POINTER_Y, `${latestY.current.toFixed(1)}%`);
        root.style.setProperty(POINTER_INTENSITY, "1");
        if (!active.current) {
          active.current = true;
          root.style.setProperty(POINTER_ACTIVE, "1");
        }
      });
    };

    const onLeave = () => {
      if (rafId.current) {
        cancelAnimationFrame(rafId.current);
        rafId.current = 0;
      }
      applyIdleState();
    };

    const onVisibilityChange = () => {
      if (document.hidden) {
        onLeave();
      }
    };

    const onMediaChange = () => {
      refreshEnabled();
    };

    const mediaQueries = [
      window.matchMedia("(prefers-reduced-motion: reduce)"),
      window.matchMedia("(prefers-contrast: more)"),
      window.matchMedia("(forced-colors: active)"),
    ];

    const observers = new MutationObserver(() => {
      refreshEnabled();
    });
    observers.observe(root, {
      attributes: true,
      attributeFilter: ["data-visual-theme", "data-theme-variant", "data-glass", "data-motion"],
    });

    for (const query of mediaQueries) {
      query.addEventListener("change", onMediaChange);
    }

    window.addEventListener("pointermove", onMove, { passive: true });
    window.addEventListener("blur", onLeave, { passive: true });
    document.addEventListener("visibilitychange", onVisibilityChange);

    return () => {
      if (rafId.current) {
        cancelAnimationFrame(rafId.current);
      }
      for (const query of mediaQueries) {
        query.removeEventListener("change", onMediaChange);
      }
      observers.disconnect();
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("blur", onLeave);
      document.removeEventListener("visibilitychange", onVisibilityChange);
      root.style.removeProperty(POINTER_X);
      root.style.removeProperty(POINTER_Y);
      root.style.removeProperty(POINTER_INTENSITY);
      root.style.removeProperty(POINTER_ACTIVE);
    };
  }, []);

  return <div className="ema-pointer-light" aria-hidden />;
}
