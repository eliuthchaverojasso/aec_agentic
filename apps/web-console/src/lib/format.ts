export function formatPercent(value?: number | null, digits = 0) {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return "-";
  }
  return `${value.toFixed(digits)}%`;
}

export function formatNumber(value?: number | null) {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return "-";
  }
  return new Intl.NumberFormat("en-US").format(value);
}

export function parseDateValue(value?: string | null) {
  if (!value) {
    return null;
  }
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

export function formatDateTime(value?: string | null) {
  const parsed = parseDateValue(value);
  if (!value) {
    return "No sync";
  }
  if (!parsed) {
    return "—";
  }
  return parsed.toLocaleString([], {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

export function formatDate(value?: string | null) {
  const parsed = parseDateValue(value);
  if (!value) {
    return "—";
  }
  if (!parsed) {
    return "—";
  }
  return parsed.toLocaleDateString([], {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

export function timeAgo(value?: string | null) {
  const parsed = parseDateValue(value);
  if (!value) {
    return "No sync";
  }
  if (!parsed) {
    return "—";
  }
  const diffMs = Date.now() - parsed.getTime();
  const diffMin = Math.max(0, Math.round(diffMs / 60000));
  if (diffMin < 60) {
    return `${diffMin || 1}m ago`;
  }
  const diffHours = Math.round(diffMin / 60);
  if (diffHours < 48) {
    return `${diffHours}h ago`;
  }
  return `${Math.round(diffHours / 24)}d ago`;
}

export function shortProjectName(title: string) {
  return title.replace(/^\d+\s+\d{3}\s+\d{4}\s+\d{3}\s+/, "").replace(/\s+R\d{2,4}$/, "");
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}
