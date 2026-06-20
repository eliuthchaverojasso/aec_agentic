import { X } from "lucide-react";
import type { ReactNode } from "react";

type DetailDrawerProps = {
  title: string;
  subtitle?: string;
  isOpen: boolean;
  onClose: () => void;
  children: ReactNode;
};

export function DetailDrawer({ title, subtitle, isOpen, onClose, children }: DetailDrawerProps) {
  if (!isOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/20">
      <button
        type="button"
        className="absolute inset-0 cursor-default"
        aria-label="Close detail drawer"
        onClick={onClose}
      />
      <aside className="ema-glass-panel relative flex h-full w-full max-w-xl flex-col border-l border-line">
        <header className="ema-page-header border-b border-line px-5 py-4">
          <div>
            <h2 className="text-base font-semibold text-ink">{title}</h2>
            {subtitle && <p className="mt-1 text-sm text-muted">{subtitle}</p>}
          </div>
          <button
            type="button"
            className="rounded-md border border-line p-2 text-muted hover:bg-surface-2 hover:text-ink"
            aria-label="Close"
            onClick={onClose}
          >
            <X className="h-4 w-4" aria-hidden />
          </button>
        </header>
        <div className="flex-1 overflow-y-auto px-5 py-5">{children}</div>
      </aside>
    </div>
  );
}

export function DetailGrid({ children }: { children: ReactNode }) {
  return <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">{children}</dl>;
}

export function DetailItem({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="ema-data-card rounded-lg border border-line px-3 py-2">
      <dt className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</dt>
      <dd className="mt-1 text-sm font-medium text-ink">{value || "-"}</dd>
    </div>
  );
}
