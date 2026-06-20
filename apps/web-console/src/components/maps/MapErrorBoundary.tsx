import React from "react";

type Props = {
  children: React.ReactNode;
  title?: string;
};

type State = {
  error: Error | null;
};

export class MapErrorBoundary extends React.Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  render() {
    if (!this.state.error) {
      return this.props.children;
    }

    return (
      <section className="ema-liquid-map-shell p-4">
        <h3 className="font-semibold text-ink">{this.props.title || "USA Project Map"}</h3>
        <p className="mt-2 text-sm text-muted">
          USA Project Map unavailable for this session. Continue with table-based project actions below.
        </p>
        <div className="mt-3 rounded-lg border border-warning bg-warning-soft p-3 text-xs text-warning">
          Local demo fallback active. Map rendering failed without affecting the rest of the dashboard.
        </div>
      </section>
    );
  }
}

