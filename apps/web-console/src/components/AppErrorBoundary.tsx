import React from "react";
import { APPEARANCE_STORAGE_KEY, resetAppearanceSettings } from "../lib/appearance";

const DEMO_SESSION_KEY = "ema-ai-demo-session";

type Props = {
  children: React.ReactNode;
};

type State = {
  error: Error | null;
};

export class AppErrorBoundary extends React.Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error) {
    console.error("EMA AI frontend render recovery boundary caught an error.", error);
  }

  render() {
    if (!this.state.error) {
      return this.props.children;
    }

    return (
      <main className="ema-recovery-page">
        <section className="ema-recovery-card">
          <p className="ema-pill">Local Demo</p>
          <h1>EMA AI frontend recovered from a render error.</h1>
          <p>
            The app shell is still available. Use the recovery actions below to clear local UI state and return to
            Executive Overview.
          </p>
          <pre>{this.state.error.message || "Unknown frontend render error."}</pre>
          <div className="flex flex-wrap gap-2">
            <button type="button" className="ema-btn-secondary" onClick={resetAppearanceAndReload}>
              Reset Appearance Settings
            </button>
            <button type="button" className="ema-btn-secondary" onClick={resetDemoAndReload}>
              Reset Local Demo Session
            </button>
            <button type="button" className="ema-btn-primary" onClick={goHome}>
              Go to Executive Overview
            </button>
          </div>
          <p className="mt-3 text-xs text-muted">
            Recovery does not change backend readiness, source data, evidence status, or official compliance.
          </p>
        </section>
      </main>
    );
  }
}

function safeRemove(key: string) {
  try {
    window.localStorage.removeItem(key);
  } catch {
    // Local demo recovery must not throw if storage is unavailable.
  }
}

function resetAppearanceAndReload() {
  resetAppearanceSettings();
  window.location.reload();
}

function resetDemoAndReload() {
  safeRemove(DEMO_SESSION_KEY);
  window.history.pushState({}, "", "/login");
  window.location.reload();
}

function goHome() {
  safeRemove(APPEARANCE_STORAGE_KEY);
  window.history.pushState({}, "", "/");
  window.location.reload();
}
