import type { ExportRecord, Issue, ProjectReadiness, ProjectSummary, Requirement } from "../types";
export { demoMilestone } from "./demoFallback";

export function projectDisplayName(project?: ProjectSummary) {
  if (!project) {
    return "No project selected";
  }
  return project.project_name || project.project_title;
}

export function latestExportForProject(exports: ExportRecord[], projectId?: number) {
  return exports
    .filter((item) => item.project_id === projectId)
    .sort((a, b) => new Date(b.completed_at || b.started_at).getTime() - new Date(a.completed_at || a.started_at).getTime())[0];
}

export function ownerRequirementGaps(requirements: Requirement[], readiness?: ProjectReadiness | null) {
  if (!readiness) {
    return requirements.length;
  }
  const readinessGaps = readinessGapCount(readiness);
  if (readinessGaps > 0) {
    return readinessGaps;
  }
  const total = readiness.trade_readiness.reduce((sum, row) => sum + row.requirements_total, 0);
  const evaluated = readiness.trade_readiness.reduce((sum, row) => sum + row.requirements_evaluated, 0);
  return Math.max(0, total - evaluated || requirements.length);
}

export function atRiskCount(projects: ProjectSummary[], readiness?: ProjectReadiness | null) {
  return projects.filter((project) => {
    if (readiness?.project_id === project.id) {
      return readiness.overall_readiness < 75;
    }
    return (project.model_health_score ?? 100) < 75 || project.high_issues > 0 || project.critical_issues > 0;
  }).length;
}

export function averageReadiness(projects: ProjectSummary[], readiness?: ProjectReadiness | null) {
  if (projects.length === 0) {
    return 0;
  }
  const total = projects.reduce((sum, project) => {
    if (readiness?.project_id === project.id) {
      return sum + readiness.overall_readiness;
    }
    return sum + (project.model_health_score ?? 100);
  }, 0);
  return Math.round(total / projects.length);
}

export function criticalGapCount(issues: Issue[], readiness?: ProjectReadiness | null) {
  if (readiness?.gap_summary) {
    return (readiness.gap_summary.critical ?? 0) + (readiness.gap_summary.high ?? 0);
  }
  if (readiness) {
    return readiness.open_issues.critical + readiness.open_issues.high;
  }
  return issues.filter((issue) => issue.severity === "critical" || issue.severity === "high").length;
}

export function readinessGapCount(readiness?: ProjectReadiness | null) {
  if (!readiness?.gap_summary) {
    return 0;
  }
  return Object.values(readiness.gap_summary).reduce((sum, value) => sum + (value ?? 0), 0);
}

export function disciplineScore(readiness: ProjectReadiness | null | undefined, discipline: string, fallback: number) {
  const row = readiness?.trade_readiness.find((item) => item.discipline === discipline.toUpperCase());
  return row?.readiness ?? fallback;
}
