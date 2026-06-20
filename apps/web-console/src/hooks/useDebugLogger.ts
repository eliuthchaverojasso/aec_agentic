import { useCallback } from "react";
import { api } from "../api/client";

type DebugLogInput = {
  action: string;
  route: string;
  project_id?: number;
  project_name?: string;
  endpoint?: string;
  method?: string;
  status?: string;
  severity?: "debug" | "info" | "warning" | "error" | "critical";
  duration_ms?: number;
  message?: string;
  error?: string;
  request_id?: string;
  run_id?: string;
};

export function useDebugLogger() {
  return useCallback(async (entry: DebugLogInput) => {
    const payload = {
      ...entry,
      message: entry.message || entry.error,
      timestamp: new Date().toISOString(),
    };
    try {
      await api.postFrontendLog(payload);
    } catch {
      // Intentionally swallow debug logging failures.
    }
  }, []);
}
