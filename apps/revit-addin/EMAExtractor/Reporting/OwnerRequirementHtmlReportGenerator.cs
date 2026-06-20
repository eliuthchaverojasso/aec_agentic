using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using EMAExtractor.Requirements;
using EMAExtractor.Requirements.Audit;

namespace EMAExtractor.Reporting
{
    public static class OwnerRequirementHtmlReportGenerator
    {
        private static readonly string[] DisciplineOrder =
        {
            "Electrical",
            "Lighting",
            "Mechanical",
            "Plumbing",
            "Technology",
            "Unknown / Needs Classification"
        };

        private static readonly string[] StatusOrder =
        {
            "All",
            "Met",
            "Not Met",
            "Needs Human Review",
            "Insufficient Model Data",
            "Not Applicable"
        };

        private static readonly string[] UrgencyOrder =
        {
            "All",
            "Critical",
            "High",
            "Medium",
            "Low",
            "Needs Review"
        };

        public static string Generate(RequirementCheckReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (string.IsNullOrWhiteSpace(report.OutputFolder))
            {
                throw new ArgumentException("A report output folder is required.", nameof(report));
            }

            Directory.CreateDirectory(report.OutputFolder);

            string safeProject = SanitizeFileName(string.IsNullOrWhiteSpace(report.ProjectName) ? "Project" : report.ProjectName);
            string safeDiscipline = SanitizeFileName(GetDisciplineLabel(report.Discipline));
            string timestamp = report.GeneratedAt == default(DateTime)
                ? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                : report.GeneratedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            string fileName = string.Format(
                CultureInfo.InvariantCulture,
                "EMA_AI_Requirement_Check_{0}_{1}_{2}.html",
                safeProject,
                safeDiscipline,
                timestamp);

            string outputPath = Path.Combine(report.OutputFolder, fileName);
            report.ReportPath = outputPath;

            File.WriteAllText(outputPath, BuildHtml(report), Encoding.UTF8);
            return outputPath;
        }

        private static string BuildHtml(RequirementCheckReport report)
        {
            ReportViewState state = BuildViewState(report);

            StringBuilder html = new StringBuilder();
            html.AppendLine("<!doctype html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\" />");
            html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
            html.AppendLine("<title>" + Encode(state.DocumentTitle) + "</title>");
            html.AppendLine("<style>");
            html.AppendLine(BuildStyles());
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<div class=\"console-shell\">");

            html.AppendLine(RenderConsoleHeader(state));
            html.AppendLine(RenderTabNav(state));

            // Summary tab
            html.AppendLine("<div class=\"tab-panel active\" id=\"tab-summary\">");
            html.AppendLine("<div class=\"page\">");
            html.AppendLine(RenderHeader(state));
            html.AppendLine(RenderFilterBar(state));
            html.AppendLine(RenderExecutiveSummary(state));
            html.AppendLine(RenderDisciplineAllocation(state));
            html.AppendLine(RenderStatusLegend());
            html.AppendLine(RenderKeyIssues(state));
            html.AppendLine(RenderIssuesByUrgency(state));
            html.AppendLine(RenderCoherenceAudit(state));
            html.AppendLine(RenderReportNotes(state));
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            // Requirements tab
            html.AppendLine("<div class=\"tab-panel\" id=\"tab-requirements\">");
            html.AppendLine(RenderRequirementsTab(state));
            html.AppendLine("</div>");

            // Evidence tab
            html.AppendLine("<div class=\"tab-panel\" id=\"tab-evidence\">");
            html.AppendLine(RenderEvidenceTab(state));
            html.AppendLine("</div>");

            // Elements tab
            html.AppendLine("<div class=\"tab-panel\" id=\"tab-elements\">");
            html.AppendLine(RenderElementsTab(state));
            html.AppendLine("</div>");

            // Rules tab
            html.AppendLine("<div class=\"tab-panel\" id=\"tab-rules\">");
            html.AppendLine(RenderRulesTab(state));
            html.AppendLine("</div>");

            // Exports tab
            html.AppendLine("<div class=\"tab-panel\" id=\"tab-exports\">");
            html.AppendLine(RenderExportsTab(state));
            html.AppendLine("</div>");

            // Ask EMA AI tab
            html.AppendLine("<div class=\"tab-panel\" id=\"tab-ask\">");
            html.AppendLine(RenderAskEmaAiTab(state));
            html.AppendLine("</div>");

            html.AppendLine(RenderMachineReadableContext(state));
            html.AppendLine("</div>");
            html.AppendLine("<script>");
            html.AppendLine(BuildScript(state));
            html.AppendLine("</script>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private static ReportViewState BuildViewState(RequirementCheckReport report)
        {
            List<RequirementCheckResult> allResults = report.Results ?? new List<RequirementCheckResult>();
            ReportFilterContext filterContext = report.FilterContext ?? new ReportFilterContext();

            string activeDiscipline = string.IsNullOrWhiteSpace(filterContext.ActiveDiscipline)
                ? GetDisciplineLabel(report.Discipline)
                : filterContext.ActiveDiscipline;
            string activeStatus = string.IsNullOrWhiteSpace(filterContext.ActiveStatus) ? "All" : filterContext.ActiveStatus;
            string activeUrgency = string.IsNullOrWhiteSpace(filterContext.ActiveUrgency) ? "All" : filterContext.ActiveUrgency;

            List<RequirementCheckResult> visibleResults = FilterResults(allResults, activeDiscipline, activeStatus, activeUrgency);
            List<KeyIssue> visibleIssues = FilterIssues(report.KeyIssues ?? new List<KeyIssue>(), activeDiscipline, activeStatus, activeUrgency);

            RequirementCheckSummary visibleSummary = RequirementCheckSummary.FromResults(visibleResults);
            double overallScore = ScoreCalculator.CalculateOverallScore(visibleResults);
            double readinessScore = ScoreCalculator.CalculateReadiness(visibleResults, report.LastModelSyncTime).OverallScore;
            RequirementDiscipline parsedDiscipline = ParseDiscipline(activeDiscipline);
            double disciplineScore = activeDiscipline == "All Disciplines"
                ? overallScore
                : ScoreCalculator.CalculateDisciplineScore(visibleResults, parsedDiscipline);

            string title = activeDiscipline == "All Disciplines"
                ? "Master Owner Requirements Review"
                : activeDiscipline + " Owner Requirements Review";

            string subtitle = activeDiscipline == "All Disciplines"
                ? "Executive summary first. Requirement-by-requirement detail remains below for every discipline."
                : "Focused discipline view. Other requirements remain in the report and are hidden by the active filter.";

            List<string> topActions = BuildTopActions(visibleResults, 5);
            List<string> questions = filterContext.SuggestedQuestions != null && filterContext.SuggestedQuestions.Count > 0
                ? filterContext.SuggestedQuestions
                : BuildSuggestedQuestions(activeDiscipline);

            string exportStem = BuildPdfStem(activeDiscipline, report.GeneratedAt);
            int disciplineImpactCount = (report.DisciplineSummaries ?? new List<DisciplineSummary>()).Count(item => item != null && item.Total > 0);

            // Coherence is audited across the full requirement set (and per requirement
            // type), independent of the active discipline filter, so the report always
            // exposes duplicate/conflict findings for every requirement and type.
            RequirementCoherenceReport coherence = RequirementCoherenceEngine.Analyze(allResults);

            return new ReportViewState
            {
                Report = report,
                AllResults = allResults,
                VisibleResults = visibleResults,
                VisibleIssues = visibleIssues,
                VisibleSummary = visibleSummary,
                VisibleOverallScore = overallScore,
                VisibleReadinessScore = readinessScore,
                VisibleDisciplineScore = disciplineScore,
                ActiveDiscipline = activeDiscipline,
                ActiveStatus = activeStatus,
                ActiveUrgency = activeUrgency,
                Title = title,
                Subtitle = subtitle,
                DocumentTitle = "EMA AI | " + title,
                ExportStem = exportStem,
                SuggestedQuestions = questions,
                TopNextActions = topActions,
                DisciplineSummaries = report.DisciplineSummaries ?? new List<DisciplineSummary>(),
                DisciplinesImpacted = disciplineImpactCount,
                ExcludedCount = Math.Max(0, allResults.Count - visibleResults.Count),
                Coherence = coherence
            };
        }

        private static string BuildStyles()
        {
            return @":root{
  --page-bg:#EEF3F8;
  --card-bg:#FFFFFF;
  --text-primary:#061633;
  --text-secondary:#475569;
  --text-muted:#64748B;
  --border:#D6E0EC;
  --border-strong:#B7C6D8;
  --shadow-sm:0 1px 2px rgba(15,23,42,.06);
  --shadow-md:0 8px 24px rgba(15,23,42,.08);
  --radius-sm:8px;
  --radius-md:14px;
  --radius-lg:20px;
  --navy:#0B1F3A;
  --blue:#2563EB;
  --bg:#edf2f7;
  --surface:#ffffff;
  --surface-2:#f8fafc;
  --text:#061633;
  --muted:#64748B;
  --accent:#2563EB;
  --accent-soft:rgba(37,99,235,.10);
  --good:#059669;
  --good-bg:#ECFDF5;
  --good-border:#047857;
  --good-text:#064E3B;
  --bad:#DC2626;
  --bad-bg:#FEF2F2;
  --bad-border:#B91C1C;
  --bad-text:#7F1D1D;
  --review:#D97706;
  --review-bg:#FFFBEB;
  --review-border:#B45309;
  --review-text:#78350F;
  --insufficient:#475569;
  --insufficient-bg:#F1F5F9;
  --insufficient-border:#334155;
  --insufficient-text:#1E293B;
  --na:#6B7280;
  --na-bg:#F9FAFB;
  --na-border:#4B5563;
  --na-text:#374151;
  --critical:#DC2626;
  --high:#D97706;
  --medium:#F59E0B;
  --low:#475569;
  --needs-review:#7c3aed;
}
*{box-sizing:border-box}
html,body{margin:0;padding:0;background:var(--page-bg);color:var(--text-primary);font-family:'Segoe UI',Inter,system-ui,-apple-system,BlinkMacSystemFont,Arial,sans-serif;font-size:15px;line-height:1.5}
body{-webkit-print-color-adjust:exact;print-color-adjust:exact}
.report-shell{max-width:1440px;margin:0 auto;padding:24px}
.page{max-width:1440px;margin:0 auto;padding:24px}
.report-section{background:var(--card-bg);border:1px solid var(--border);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm);padding:24px;margin:18px 0}
.card{background:var(--card-bg);border:1px solid var(--border);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm);margin-bottom:18px}
.hero{padding:28px 28px 24px}
.eyebrow{display:inline-flex;align-items:center;gap:8px;padding:7px 14px;border-radius:999px;background:var(--accent-soft);color:#1D4ED8;font-size:11px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}
.hero h1{margin:14px 0 8px;font-size:32px;line-height:1.12;letter-spacing:-.02em;color:var(--text-primary)}
.hero p{margin:0;color:var(--text-muted);max-width:1100px;font-size:15px}
.identity-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px;margin-top:18px}
.identity-card{background:#fff;border:1px solid var(--border);border-radius:var(--radius-md);padding:14px 16px}
.identity-card .label{display:block;color:var(--text-muted);font-size:11px;text-transform:uppercase;letter-spacing:.07em;margin-bottom:4px;font-weight:600}
.identity-card .value{font-size:15px;font-weight:700;word-break:break-word;color:var(--text-primary)}
.meta-grid,.summary-grid,.mini-grid,.question-grid,.traceability-grid{display:grid;gap:12px}
.meta-grid{grid-template-columns:repeat(auto-fit,minmax(220px,1fr));margin-top:18px}
.metric-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(185px,1fr));gap:14px;margin-top:16px}
.summary-grid{grid-template-columns:repeat(auto-fit,minmax(185px,1fr));margin-top:16px}
.mini-grid{grid-template-columns:repeat(auto-fit,minmax(130px,1fr));margin-top:12px}
.issue-grid{grid-template-columns:repeat(auto-fit,minmax(440px,1fr));margin-top:14px;display:grid;gap:16px}
.question-grid{grid-template-columns:repeat(auto-fit,minmax(260px,1fr))}
.traceability-grid{grid-template-columns:repeat(auto-fit,minmax(240px,1fr))}
.chip{background:var(--card-bg);border:1px solid var(--border);border-radius:var(--radius-md);padding:14px 16px}
.chip .label,.metric .label,.field-label{display:block;color:var(--text-muted);font-size:11px;text-transform:uppercase;letter-spacing:.07em;margin-bottom:4px;font-weight:600}
.chip .value,.metric .value{font-size:15px;font-weight:700;word-break:break-word}
.metric,.section,.filter-bar,.filter-panel,.focus-card,.table-wrap,.status-legend,.note-card,.issue-card,.result-card,.compact-row,.question-card{background:var(--card-bg);border:1px solid var(--border);border-radius:var(--radius-md)}
.metric{padding:18px 18px 16px;min-height:128px;border-left:5px solid var(--border-strong);position:relative;overflow:hidden}
.metric .value{font-size:28px;line-height:1;margin-top:6px;font-weight:800}
.metric .detail{margin-top:8px;color:var(--text-muted);font-size:13px;line-height:1.4}
.metric.status-met{border-left-color:var(--good);background:linear-gradient(135deg,#fff 0%,var(--good-bg) 100%);color:var(--good-text)}
.metric.status-not-met{border-left-color:var(--bad);background:linear-gradient(135deg,#fff 0%,var(--bad-bg) 100%);color:var(--bad-text)}
.metric.status-needs-review{border-left-color:var(--review);background:linear-gradient(135deg,#fff 0%,var(--review-bg) 100%);color:var(--review-text)}
.metric.status-insufficient-data{border-left-color:var(--insufficient);background:linear-gradient(135deg,#fff 0%,var(--insufficient-bg) 100%);color:var(--insufficient-text)}
.metric.status-not-applicable{border-left-color:var(--na);background:linear-gradient(135deg,#fff 0%,var(--na-bg) 100%);color:var(--na-text)}
.section{padding:24px}
.section h2{margin:0 0 8px;font-size:22px;line-height:1.2;color:var(--text-primary)}
.section .section-copy{margin:0 0 16px;color:var(--text-muted);font-size:14px}
.filter-bar,.filter-panel{padding:20px 22px}
.filter-group{display:flex;flex-wrap:wrap;gap:8px;margin-top:10px}
.filter-label{font-size:11px;font-weight:800;color:var(--text-muted);text-transform:uppercase;letter-spacing:.08em}
.filter-chip,.action-chip{appearance:none;-webkit-appearance:none;border:1px solid var(--border-strong);background:#fff;border-radius:999px;padding:8px 14px;font-weight:700;font-size:13px;cursor:pointer;color:var(--navy);box-shadow:none;transition:all .15s ease;line-height:1.3}
.filter-chip:hover,.action-chip:hover{border-color:#94A3B8;background:#F8FAFC}
.filter-chip:focus-visible,.action-chip:focus-visible{outline:2px solid var(--blue);outline-offset:2px}
.filter-chip.active{background:var(--navy);color:#fff;border-color:var(--navy);box-shadow:0 4px 12px rgba(11,31,58,.18)}
.action-chip{background:var(--surface-2);border-color:var(--border-strong)}
.action-chip:hover{background:#EFF6FF;border-color:var(--blue);color:var(--blue)}
.next-actions-callout{border-left:5px solid var(--blue);background:linear-gradient(90deg,#EFF6FF,#FFFFFF);border-radius:var(--radius-md);padding:16px 18px;margin-top:14px}
.next-actions-callout strong{color:var(--navy)}
.next-actions-callout ul{margin:8px 0 0 18px;color:var(--text-primary)}
.next-actions-callout li{margin-bottom:4px}
.callout{border-left:4px solid var(--blue);background:linear-gradient(90deg,rgba(37,99,235,.06),rgba(37,99,235,.02));padding:16px 18px;border-radius:var(--radius-md);color:var(--text-primary)}
.focus-card{padding:18px 20px;margin-bottom:14px}
.focus-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:10px;margin-top:12px}
.small-card{background:#f8fbff;border:1px solid var(--border);border-left:5px solid var(--border-strong);border-radius:var(--radius-md);padding:12px 14px;position:relative;overflow:hidden}
.small-card .k{display:block;color:var(--text-muted);font-size:11px;text-transform:uppercase;letter-spacing:.06em;font-weight:600}
.small-card .v{font-weight:800;font-size:15px;margin-top:2px}
.small-card.status-met{border-left-color:var(--good);background:linear-gradient(135deg,#fff,var(--good-bg));color:var(--good-text)}
.small-card.status-not-met{border-left-color:var(--bad);background:linear-gradient(135deg,#fff,var(--bad-bg));color:var(--bad-text)}
.small-card.status-needs-review{border-left-color:var(--review);background:linear-gradient(135deg,#fff,var(--review-bg));color:var(--review-text)}
.small-card.status-insufficient-data{border-left-color:var(--insufficient);background:linear-gradient(135deg,#fff,var(--insufficient-bg));color:var(--insufficient-text)}
.small-card.status-not-applicable{border-left-color:var(--na);background:linear-gradient(135deg,#fff,var(--na-bg));color:var(--na-text)}
.status-legend{padding:20px 22px}
.status-legend-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:12px}
.urgency-legend-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:12px}
.legend-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:12px}
.legend-item{padding:14px 16px;border:1px solid var(--border);border-left:5px solid var(--border-strong);border-radius:var(--radius-md);background:#fff;position:relative;overflow:hidden}
.legend-item.status-met,.legend-item.status-legend-card.status-met{background:var(--good-bg);border-left-color:var(--good)}
.legend-item.status-not-met,.legend-item.status-legend-card.status-not-met{background:var(--bad-bg);border-left-color:var(--bad)}
.legend-item.status-needs-review,.legend-item.status-legend-card.status-needs-review{background:var(--review-bg);border-left-color:var(--review)}
.legend-item.status-insufficient-data,.legend-item.status-legend-card.status-insufficient-data{background:var(--insufficient-bg);border-left-color:var(--insufficient)}
.legend-item.status-not-applicable,.legend-item.status-legend-card.status-not-applicable{background:var(--na-bg);border-left-color:var(--na)}
.legend-item.urgency-legend-card.urgency-critical{background:#FEF2F2;border-left-color:var(--critical)}
.legend-item.urgency-legend-card.urgency-high{background:#FFFBEB;border-left-color:var(--high)}
.legend-item.urgency-legend-card.urgency-medium{background:#FEF3C7;border-left-color:var(--medium)}
.legend-item.urgency-legend-card.urgency-low{background:var(--insufficient-bg);border-left-color:var(--low)}
.legend-item.urgency-legend-card.urgency-needs-review{background:#F5F3FF;border-left-color:var(--needs-review)}
.legend-pill{display:inline-flex;align-items:center;gap:8px;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.05em;margin-bottom:8px}
.legend-pill .dot{width:13px;height:13px;border-radius:999px;display:inline-block;border:2px solid rgba(15,23,42,.10)}
.dot.met{background:var(--good)}
.dot.notmet{background:var(--bad)}
.dot.review{background:var(--review)}
.dot.bad{background:var(--insufficient)}
.dot.na{background:var(--na)}
.result-list{display:grid;gap:14px}
.result-card,.issue-card{padding:18px;position:relative;overflow:hidden;border-left:6px solid var(--border-strong);background:#fff;border-radius:16px}
.discipline-section{background:#fff;border:1px solid var(--border);border-left:6px solid var(--blue);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm);margin-top:18px;padding:20px}
.discipline-section + .discipline-section{margin-top:18px}
.result-head,.issue-head{display:flex;justify-content:space-between;gap:12px;align-items:flex-start;flex-wrap:wrap}
.result-title,.issue-title{font-size:17px;font-weight:800;margin:0;color:var(--text-primary)}
.result-meta,.issue-meta{color:var(--text-muted);font-size:12px;margin-top:4px}
.pill{display:inline-flex;align-items:center;justify-content:center;padding:5px 11px;border-radius:999px;font-size:11px;font-weight:800;border:1px solid transparent;white-space:nowrap;letter-spacing:.02em}
.pill.status-met{background:var(--good);color:#fff;border-color:var(--good-border)}
.pill.status-not-met{background:var(--bad);color:#fff;border-color:var(--bad-border)}
.pill.status-needs-review{background:var(--review);color:#fff;border-color:var(--review-border)}
.pill.status-insufficient-data{background:var(--insufficient);color:#fff;border-color:var(--insufficient-border)}
.pill.status-not-applicable{background:var(--na);color:#fff;border-color:var(--na-border)}
.pill.urgency-critical{background:var(--critical);color:#fff;border-color:#B91C1C}
.pill.urgency-high{background:var(--high);color:#fff;border-color:#B45309}
.pill.urgency-medium{background:var(--medium);color:#1F2937;border-color:#D97706}
.pill.urgency-low{background:var(--low);color:#fff;border-color:#334155}
.pill.urgency-needs-review{background:var(--needs-review);color:#fff;border-color:#6D28D9}
.discipline-pill{display:inline-flex;align-items:center;justify-content:center;padding:5px 11px;border-radius:999px;font-size:11px;font-weight:800;border:1px solid transparent;white-space:nowrap;letter-spacing:.02em}
.discipline-badge-electrical,.discipline-pill.discipline-electrical{background:#2563EB;color:#fff;border-color:#1D4ED8}
.discipline-badge-lighting,.discipline-pill.discipline-lighting{background:#D97706;color:#fff;border-color:#F59E0B}
.discipline-badge-mechanical,.discipline-pill.discipline-mechanical{background:#7C3AED;color:#fff;border-color:#6D28D9}
.discipline-badge-plumbing,.discipline-pill.discipline-plumbing{background:#0891B2;color:#fff;border-color:#0E7490}
.discipline-badge-technology,.discipline-pill.discipline-technology{background:#4F46E5;color:#fff;border-color:#4338CA}
.discipline-badge-unknown,.discipline-pill.discipline-unknown{background:#64748B;color:#fff;border-color:#475569}
.discipline-pill.discipline-general{background:#0F766E;color:#fff;border-color:#0D9488}
.detail-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px;margin-top:12px}
.decision-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px;margin-top:12px}
.snapshot-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px;margin-top:12px}
.requirement-text-block{background:#fff;border:1px solid var(--border);border-left:5px solid var(--blue);border-radius:var(--radius-sm);padding:14px 16px;margin-top:12px;color:var(--text-primary);font-size:14px}
.parameter-summary{margin-top:12px;background:#FBFDFF;border:1px solid var(--border);border-radius:var(--radius-md);padding:14px}
.parameter-list{display:grid;gap:8px;margin-top:8px}
.parameter-row{display:grid;grid-template-columns:minmax(140px,220px) minmax(120px,1fr) minmax(160px,2fr) auto;gap:10px;align-items:start;border:1px solid var(--border);border-radius:8px;background:#fff;padding:10px;overflow-wrap:anywhere}
.parameter-name{font-weight:800;color:var(--navy)}
.parameter-value{font-family:Consolas,'Courier New',monospace;font-size:12px;color:var(--text-primary);background:#F8FAFC;border:1px solid var(--border);border-radius:6px;padding:4px 6px}
.parameter-reason{font-size:13px;color:var(--text-secondary)}
.result-chip{display:inline-flex;align-items:center;justify-content:center;border-radius:999px;padding:4px 9px;font-size:11px;font-weight:800;white-space:nowrap;border:1px solid var(--border-strong)}
.result-chip.pass{background:var(--good-bg);color:var(--good-text);border-color:var(--good-border)}
.result-chip.missing,.result-chip.empty,.result-chip.fail{background:var(--bad-bg);color:var(--bad-text);border-color:var(--bad-border)}
.result-chip.unavailable{background:var(--insufficient-bg);color:var(--insufficient-text);border-color:var(--insufficient-border)}
.result-chip.needs-review{background:#F5F3FF;color:#4C1D95;border-color:#7C3AED}
.details-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:12px;margin-top:12px}
.evidence-block,.reasoning-block,.next-action-block,.traceability-block,.missing-evidence-block{background:#F8FAFC;border:1px solid var(--border);border-radius:var(--radius-sm);padding:12px 14px;margin-top:8px}
.evidence-block .field-label,.reasoning-block .field-label,.next-action-block .field-label,.traceability-block .field-label,.missing-evidence-block .field-label{font-size:11px;text-transform:uppercase;letter-spacing:.06em;color:var(--text-muted);font-weight:700;margin-bottom:6px;display:block}
.field{background:#F8FAFC;border:1px solid var(--border);border-radius:var(--radius-sm);padding:12px 14px}
.field .value{font-size:13px;color:var(--text-primary);line-height:1.5}
.element-id-box{background:#F8FAFC;border:1px dashed #94A3B8;border-radius:12px;padding:14px;font-family:Consolas,'Courier New',monospace;margin-top:10px}
.copyable-ids{display:inline-flex;align-items:center;gap:8px;max-width:100%;padding:10px 12px;border:1px solid var(--border);border-radius:12px;background:#F8FAFC;color:var(--navy);font-weight:700;word-break:break-all;overflow-wrap:anywhere;font-family:Consolas,'Courier New',monospace;font-size:13px}
.copy-ids{appearance:none;-webkit-appearance:none;border:1px solid var(--navy);background:var(--navy);color:#fff;border-radius:999px;padding:8px 16px;font-weight:700;font-size:13px;cursor:pointer;transition:all .15s ease}
.copy-ids:hover{background:#1E3A5F;box-shadow:0 4px 12px rgba(11,31,58,.18)}
.copy-ids:focus-visible{outline:2px solid var(--blue);outline-offset:2px}
.compact-row{padding:14px 16px}
.compact-row + .compact-row{margin-top:10px}
.compact-row .title{font-weight:800;color:var(--text-primary)}
.compact-row .subtitle{color:var(--text-muted);font-size:12px;margin-top:3px}
.group-title{display:flex;justify-content:space-between;gap:12px;align-items:center;margin:10px 0 8px;flex-wrap:wrap}
.group-title h3{margin:0;font-size:18px;color:var(--text-primary)}
.group-title .meta{color:var(--text-muted);font-size:13px}
table{width:100%;border-collapse:collapse}
th,td{padding:11px 10px;border-bottom:1px solid var(--border);vertical-align:top}
th{font-size:11px;text-transform:uppercase;letter-spacing:.07em;color:var(--text-muted);text-align:left;font-weight:700}
td.numeric,th.numeric{text-align:right}
.empty-state{padding:18px;border:1px dashed #CBD5E1;border-radius:var(--radius-md);color:var(--text-muted);background:#FBFDFF;font-size:14px}
.note-card{padding:16px 18px;color:var(--text-secondary);font-size:14px}
.report-path{word-break:break-word}
.no-print{}
.discipline-link{display:inline-flex;align-items:center;gap:6px;color:var(--navy);text-decoration:none;font-weight:700;font-size:13px;transition:color .1s}
.discipline-link:hover{text-decoration:underline;color:var(--blue)}
.discipline-link:focus-visible{outline:2px solid var(--blue);outline-offset:2px}
details.traceability summary a{color:var(--navy);text-decoration:none}
details.traceability summary a:hover{text-decoration:underline}
.result-card.status-met,.issue-card.status-met,.discipline-section.status-met,.legend-item.status-met{border-left-color:var(--good)}
.result-card.status-not-met,.issue-card.status-not-met,.discipline-section.status-not-met,.legend-item.status-not-met{border-left-color:var(--bad)}
.result-card.status-needs-review,.issue-card.status-needs-review,.discipline-section.status-needs-review,.legend-item.status-needs-review{border-left-color:var(--review)}
.result-card.status-insufficient-data,.issue-card.status-insufficient-data,.discipline-section.status-insufficient-data,.legend-item.status-insufficient-data{border-left-color:var(--insufficient)}
.result-card.status-not-applicable,.issue-card.status-not-applicable,.discipline-section.status-not-applicable,.legend-item.status-not-applicable{border-left-color:var(--na)}
.urgency-group{border:1px solid var(--border);border-radius:16px;overflow:hidden;margin-bottom:18px;background:#fff}
.urgency-group-header{display:flex;justify-content:space-between;align-items:center;padding:14px 16px;flex-wrap:wrap;gap:8px}
.urgency-group.urgency-critical{border-left:6px solid var(--critical)}
.urgency-group.urgency-critical .urgency-group-header{background:#FEF2F2}
.urgency-group.urgency-high{border-left:6px solid var(--high)}
.urgency-group.urgency-high .urgency-group-header{background:#FFFBEB}
.urgency-group.urgency-medium{border-left:6px solid var(--medium)}
.urgency-group.urgency-medium .urgency-group-header{background:#FEF3C7}
.urgency-group.urgency-low{border-left:6px solid var(--low)}
.urgency-group.urgency-low .urgency-group-header{background:var(--insufficient-bg)}
.urgency-group.urgency-needs-review{border-left:6px solid var(--needs-review)}
.urgency-group.urgency-needs-review .urgency-group-header{background:#F5F3FF}
.result-card.urgency-critical,.issue-card.urgency-critical{border-top:4px solid var(--critical)}
.result-card.urgency-high,.issue-card.urgency-high{border-top:4px solid var(--high)}
.result-card.urgency-medium,.issue-card.urgency-medium{border-top:4px solid var(--medium)}
.result-card.urgency-low,.issue-card.urgency-low{border-top:4px solid var(--low)}
.result-card.urgency-needs-review,.issue-card.urgency-needs-review{border-top:4px solid var(--needs-review)}
.discipline-electrical{border-color:rgba(37,99,235,.24)}
.discipline-lighting{border-color:rgba(217,119,6,.24)}
.discipline-mechanical{border-color:rgba(124,58,237,.24)}
.discipline-plumbing{border-color:rgba(8,145,178,.24)}
.discipline-technology{border-color:rgba(79,70,229,.24)}
.discipline-unknown{border-color:rgba(100,116,139,.24)}
.discipline-general{border-color:rgba(15,118,110,.24)}
.discipline-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(285px,1fr));gap:16px;margin-top:16px}
.discipline-card{background:#fff;border:1px solid var(--border);border-left:6px solid var(--border-strong);border-radius:18px;padding:18px 20px 20px;position:relative;overflow:hidden;box-shadow:var(--shadow-sm);transition:box-shadow .15s,transform .15s,border-color .15s;min-height:190px}
.discipline-card::before{content:"";position:absolute;left:0;top:0;right:0;height:8px;background:var(--discipline-accent,var(--blue))}
.discipline-card:hover{box-shadow:var(--shadow-md);transform:translateY(-1px)}
.discipline-card-header{display:flex;justify-content:space-between;gap:16px;align-items:flex-start;flex-wrap:wrap}
.discipline-card-identity{display:flex;gap:12px;align-items:flex-start;min-width:0;flex:1 1 280px}
.discipline-card-swatch{width:16px;height:16px;border-radius:999px;flex:none;border:2px solid rgba(15,23,42,.12);margin-top:7px;box-shadow:0 0 0 4px rgba(255,255,255,.8)}
.discipline-card-title-wrap{min-width:0}
.discipline-card-kicker{display:block;font-size:11px;font-weight:800;color:var(--text-muted);text-transform:uppercase;letter-spacing:.08em;margin-bottom:4px}
.discipline-card-name{margin:0;font-size:24px;line-height:1.1;font-weight:900;letter-spacing:-.03em;color:var(--text-primary);word-break:break-word}
.discipline-card-role{margin-top:6px;font-size:13px;line-height:1.35;color:var(--text-secondary)}
.discipline-card-score{flex:none;min-width:112px;text-align:right;padding:10px 12px;border:1px solid var(--border);border-radius:14px;background:#fff;box-shadow:0 1px 2px rgba(15,23,42,.04)}
.discipline-card-score-value{font-size:30px;line-height:1;font-weight:900;color:var(--discipline-accent,var(--navy))}
.discipline-card-score-label{margin-top:4px;font-size:11px;font-weight:700;color:var(--text-muted);text-transform:uppercase;letter-spacing:.06em}
.discipline-card-counts{display:flex;flex-wrap:wrap;gap:8px;margin-top:14px}
.discipline-card .pill{font-size:12px;padding:6px 12px;min-height:28px;line-height:1.2;box-shadow:0 1px 1px rgba(15,23,42,.04)}
.discipline-card-electrical,.discipline-section-electrical{border-color:rgba(37,99,235,.24)}
.discipline-card-lighting,.discipline-section-lighting{border-color:rgba(217,119,6,.24)}
.discipline-card-mechanical,.discipline-section-mechanical{border-color:rgba(124,58,237,.24)}
.discipline-card-plumbing,.discipline-section-plumbing{border-color:rgba(8,145,178,.24)}
.discipline-card-technology,.discipline-section-technology{border-color:rgba(79,70,229,.24)}
.discipline-card-unknown,.discipline-section-unknown{border-color:rgba(100,116,139,.24)}
.discipline-card-general,.discipline-section-general{border-color:rgba(15,118,110,.24)}
.filter-chip.discipline-electrical{border-color:#2563EB;color:#1E3A8A;background:#EFF6FF}
.filter-chip.discipline-electrical.active{background:#2563EB;color:#fff;border-color:#1D4ED8}
.filter-chip.discipline-lighting{border-color:#D97706;color:#92400E;background:#FFFBEB}
.filter-chip.discipline-lighting.active{background:#D97706;color:#fff;border-color:#F59E0B}
.filter-chip.discipline-mechanical{border-color:#7C3AED;color:#4C1D95;background:#F5F3FF}
.filter-chip.discipline-mechanical.active{background:#7C3AED;color:#fff;border-color:#6D28D9}
.filter-chip.discipline-plumbing{border-color:#0891B2;color:#164E63;background:#ECFEFF}
.filter-chip.discipline-plumbing.active{background:#0891B2;color:#fff;border-color:#0E7490}
.filter-chip.discipline-technology{border-color:#4F46E5;color:#312E81;background:#EEF2FF}
.filter-chip.discipline-technology.active{background:#4F46E5;color:#fff;border-color:#4338CA}
.filter-chip.discipline-unknown{border-color:#64748B;color:#334155;background:#F8FAFC}
.filter-chip.discipline-unknown.active{background:#64748B;color:#fff;border-color:#475569}
.filter-chip.discipline-general{border-color:#0F766E;color:#134E4A;background:#F0FDFA}
.filter-chip.discipline-general.active{background:#0F766E;color:#fff;border-color:#0D9488}
.ask-ema-section{background:linear-gradient(135deg,#EFF6FF 0%,#fff 100%);border:1px solid rgba(37,99,235,.18);border-radius:var(--radius-lg);padding:24px}
.ask-ema-section h2{color:var(--navy);margin:0 0 4px}
.ask-ema-section .section-copy{color:var(--text-muted);margin:0 0 16px}
.question-card{background:#fff;border:1px solid var(--border);border-radius:var(--radius-md);padding:14px 16px;transition:border-color .15s,box-shadow .15s}
.question-card:hover{border-color:var(--blue);box-shadow:0 4px 12px rgba(37,99,235,.08)}
details.traceability{margin-top:12px}
details.traceability>summary{list-style:none;border:1px solid var(--border);border-radius:10px;background:#F8FAFC;padding:10px 12px}
details.traceability>summary::-webkit-details-marker{display:none}
details.traceability[open]>summary{background:#EFF6FF;border-color:#BFDBFE}
summary{cursor:pointer;font-weight:700;color:var(--navy);font-size:14px}
summary:focus-visible{outline:2px solid var(--blue);outline-offset:2px}
a:focus-visible{outline:2px solid var(--blue);outline-offset:2px}
.filter-context-banner{background:linear-gradient(90deg,#EFF6FF,#fff);border:1px solid rgba(37,99,235,.18);border-left:5px solid var(--blue);border-radius:var(--radius-md);padding:14px 18px;margin-bottom:14px}
.filter-context-banner .banner-title{font-weight:800;font-size:14px;color:var(--navy);margin:0 0 4px}
.filter-context-banner .banner-detail{color:var(--text-muted);font-size:13px;margin:0;line-height:1.5}
.filter-context-banner .banner-caption{color:var(--text-muted);font-size:12px;font-style:italic;margin:6px 0 0}
.traceability-compact{display:flex;flex-wrap:wrap;gap:10px;align-items:center;margin-top:8px}
.traceability-compact .id-preview{font-family:Consolas,'Courier New',monospace;font-size:13px;color:var(--navy);max-width:420px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;background:#F8FAFC;border:1px solid var(--border);border-radius:8px;padding:6px 10px}
.element-id-scroll{max-height:200px;overflow-y:auto;overflow-x:hidden;word-break:break-all;font-family:Consolas,'Courier New',monospace;font-size:13px;padding:8px;background:#F8FAFC;border:1px solid var(--border);border-radius:8px;margin-top:8px}
.scope-note{margin-top:8px;padding:8px 12px;background:#F1F5F9;border:1px solid #94A3B8;border-left:4px solid #64748B;border-radius:8px;color:#334155;font-size:12px}
.matched-elements-scroll{max-height:400px;overflow:auto}
.empty-filter-state{padding:24px;border:1px dashed #CBD5E1;border-radius:var(--radius-md);color:var(--text-muted);background:#FBFDFF;font-size:14px;text-align:center}
@media print{
  body{background:#fff}
  .page,.report-shell{padding:0;max-width:none}
  .no-print,.filter-bar,.filter-panel,.filter-context-banner{display:none !important}
  *{-webkit-print-color-adjust:exact !important;print-color-adjust:exact !important}
  .card,.report-section,.section,.focus-card,.status-legend,.note-card,.issue-card,.result-card,.compact-row,.metric,.chip,.discipline-section,.legend-item,.discipline-card,.urgency-group,.status-legend-card,.urgency-legend-card,.metric-card,.requirement-card{box-shadow:none !important;break-inside:avoid;page-break-inside:avoid}
  .result-card.urgency-critical,.issue-card.urgency-critical{border-top:4px solid var(--critical)}
  .result-card.urgency-high,.issue-card.urgency-high{border-top:4px solid var(--high)}
  .result-card.urgency-medium,.issue-card.urgency-medium{border-top:4px solid var(--medium)}
  .result-card.urgency-low,.issue-card.urgency-low{border-top:4px solid var(--low)}
  .result-card.urgency-needs-review,.issue-card.urgency-needs-review{border-top:4px solid var(--needs-review)}
  script,#ema-ai-report-context{display:none !important}
}" + BuildConsoleStyles();
        }

        private static string BuildConsoleStyles()
        {
            return @"
/* === Console Shell === */
.console-shell{display:flex;flex-direction:column;min-height:100vh;background:var(--page-bg)}
.console-header{background:var(--navy);color:#fff;padding:10px 18px;flex-shrink:0}
.console-header-inner{display:flex;align-items:center;justify-content:space-between;gap:14px;flex-wrap:wrap}
.console-header-identity{display:flex;flex-direction:column;gap:4px}
.console-identity-chips{display:flex;flex-wrap:wrap;gap:8px;margin-top:8px}
.console-eyebrow{font-size:10px;text-transform:uppercase;letter-spacing:.08em;color:rgba(255,255,255,.55);font-weight:700}
.identity-chip{display:inline-flex;align-items:baseline;gap:6px;background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.14);border-radius:8px;padding:6px 10px;margin:0;font-size:12px;line-height:1.3}
.ch-label{color:rgba(255,255,255,.55);font-size:10px;font-weight:700;letter-spacing:.05em}
.ch-value{color:#fff;font-weight:600}
.console-toolbar{display:flex;align-items:center;gap:6px;flex-wrap:wrap}
.toolbar-btn{background:rgba(255,255,255,.10);border:1px solid rgba(255,255,255,.18);color:#fff;border-radius:6px;padding:5px 11px;font-size:12px;font-weight:600;cursor:pointer;white-space:nowrap;transition:background .15s}
.toolbar-btn:hover:not(:disabled){background:rgba(255,255,255,.20)}
.toolbar-btn-disabled{opacity:.38;cursor:not-allowed}
.toolbar-divider{width:1px;height:20px;background:rgba(255,255,255,.18);margin:0 2px}
/* === Tab Nav === */
.console-tab-nav{display:flex;align-items:center;background:#fff;border-bottom:2px solid var(--border);padding:0 16px;gap:0;position:sticky;top:0;z-index:100;flex-shrink:0}
.tab-btn{background:none;border:none;border-bottom:3px solid transparent;padding:10px 14px;font-size:13px;font-weight:600;color:var(--text-muted);cursor:pointer;white-space:nowrap;margin-bottom:-2px;transition:color .15s,border-color .15s}
.tab-btn:hover{color:var(--navy)}
.tab-btn.active{color:var(--blue);border-bottom-color:var(--blue)}
.tab-btn-ai{color:var(--blue);opacity:.85}
.tab-btn-ai.active{opacity:1}
.tab-spacer{flex:1}
.tab-info{font-size:11px;color:var(--text-muted);padding:0 8px}
/* === Tab Panels === */
.tab-panel{display:none}
.tab-panel.active{display:block}
.tab-panel-body{padding:0}
/* === Schedule Console === */
.schedule-console{display:flex;flex-direction:column;height:calc(100vh - 90px)}
.schedule-top{display:flex;align-items:center;gap:8px;padding:10px 14px;background:#fff;border-bottom:1px solid var(--border);flex-wrap:wrap;flex-shrink:0}
.schedule-search-input{flex:1;min-width:200px;max-width:340px;border:1px solid var(--border);border-radius:6px;padding:5px 10px;font-size:13px;color:var(--text-primary);background:#fff;outline:none}
.schedule-search-input:focus{border-color:var(--blue);box-shadow:0 0 0 2px rgba(37,99,235,.12)}
.schedule-select{border:1px solid var(--border);border-radius:6px;padding:5px 8px;font-size:12px;color:var(--text-primary);background:#fff;cursor:pointer}
.schedule-clear-btn{background:none;border:1px solid var(--border);border-radius:6px;padding:5px 10px;font-size:12px;color:var(--text-muted);cursor:pointer}
.schedule-clear-btn:hover{border-color:var(--blue);color:var(--blue)}
.schedule-count{font-size:12px;color:var(--text-muted);white-space:nowrap}
.schedule-grid-wrap{overflow:auto;flex:1}
.schedule-grid{width:100%;border-collapse:collapse;font-size:12px;table-layout:auto}
.schedule-grid thead tr{background:#F8FAFC;position:sticky;top:0;z-index:10}
.schedule-grid th{padding:7px 9px;text-align:left;border-bottom:2px solid var(--border);font-weight:700;font-size:11px;text-transform:uppercase;letter-spacing:.04em;color:var(--text-muted);white-space:nowrap}
.schedule-grid td{padding:6px 9px;border-bottom:1px solid #F1F5F9;vertical-align:middle;color:var(--text-primary)}
.schedule-grid tr:hover td{background:#F8FAFC}
.schedule-grid tr.sched-selected td{background:#EFF6FF}
.schedule-grid tr.sched-expanded td{background:#F0F7FF}
.sortable{cursor:pointer;user-select:none}
.sortable:hover{color:var(--navy)}
.sort-icon{opacity:.45;font-size:10px}
.sched-expand-btn{background:none;border:none;cursor:pointer;color:var(--text-muted);font-size:14px;padding:0 4px;line-height:1}
.sched-select-cb{cursor:pointer}
.sched-status-pill{display:inline-block;border-radius:4px;padding:2px 6px;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.03em}
.sched-met{background:var(--good-bg);color:var(--good-text)}
.sched-notmet{background:var(--bad-bg);color:var(--bad-text)}
.sched-review{background:var(--review-bg);color:var(--review-text)}
.sched-insufficient{background:var(--insufficient-bg);color:var(--insufficient-text)}
.sched-na{background:var(--na-bg);color:var(--na-text)}
.sched-urg-critical{color:var(--critical);font-weight:700}
.sched-urg-high{color:var(--high);font-weight:700}
.sched-urg-medium{color:var(--medium)}
.sched-urg-low{color:var(--text-muted)}
.sched-ai-btn{background:none;border:1px solid rgba(37,99,235,.3);border-radius:4px;color:var(--blue);font-size:10px;font-weight:700;padding:2px 6px;cursor:pointer;white-space:nowrap}
.sched-ai-btn:hover{background:rgba(37,99,235,.06)}
/* Expanded row detail */
.sched-detail-row td{padding:0 !important;background:#F0F7FF}
.sched-detail-inner{padding:12px 16px;font-size:12px;display:grid;grid-template-columns:1fr 1fr;gap:14px}
.sched-detail-section{display:flex;flex-direction:column;gap:4px}
.sched-detail-label{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:var(--text-muted)}
.sched-detail-value{color:var(--text-primary);line-height:1.5}
.sched-evidence-list{list-style:none;margin:0;padding:0}
.sched-evidence-list li{padding:2px 0;border-bottom:1px solid #E2E8F0}
.sched-evidence-list li:last-child{border:none}
/* Schedule pagination */
.schedule-pagination{display:flex;align-items:center;justify-content:center;gap:6px;padding:8px 14px;background:#fff;border-top:1px solid var(--border);flex-shrink:0;flex-wrap:wrap}
.page-btn{background:none;border:1px solid var(--border);border-radius:5px;padding:4px 10px;font-size:12px;cursor:pointer;color:var(--text-primary)}
.page-btn:hover{border-color:var(--blue);color:var(--blue)}
.page-btn.active-page{background:var(--blue);color:#fff;border-color:var(--blue)}
.page-info{font-size:12px;color:var(--text-muted)}
/* === Schedule detail panel === */
.schedule-detail-panel{border-top:2px solid var(--blue);background:#F8FAFC;padding:16px;font-size:13px;flex-shrink:0;max-height:320px;overflow:auto}
/* Legacy details */
.schedule-legacy-details{margin:16px;border:1px solid var(--border);border-radius:10px;background:#fff}
.schedule-legacy-details>summary{padding:10px 14px;font-size:13px;font-weight:700;color:var(--text-muted);cursor:pointer}
/* === Evidence Browser === */
.evidence-view-nav{display:flex;gap:6px;flex-wrap:wrap;margin-bottom:4px}
.evidence-view-btn{background:#fff;border:1px solid var(--border);border-radius:6px;padding:5px 12px;font-size:12px;font-weight:600;cursor:pointer;color:var(--text-muted)}
.evidence-view-btn.active{background:var(--blue);color:#fff;border-color:var(--blue)}
/* === Export Grid === */
.export-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:14px;margin-top:18px}
.export-card{background:#fff;border:1px solid var(--border);border-radius:var(--radius-sm);padding:16px;display:flex;flex-direction:column;gap:10px}
.export-card-title{font-weight:700;font-size:13px;color:var(--navy)}
.export-card-desc{font-size:12px;color:var(--text-muted);line-height:1.5}
.export-card-btn{background:var(--blue);color:#fff;border:none;border-radius:6px;padding:8px 14px;font-size:12px;font-weight:700;cursor:pointer;margin-top:auto}
.export-card-btn:hover{background:#1d4ed8}
/* === Ask EMA AI Panel === */
.ask-panel{display:flex;flex-direction:column;gap:14px;background:#fff;border:1px solid var(--border);border-radius:var(--radius-md);padding:18px;margin-bottom:14px}
.ask-model-row,.ask-context-row{display:flex;align-items:center;gap:10px;flex-wrap:wrap}
.ask-row-label{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--text-muted);min-width:52px}
.ask-model-select,.ask-context-scope{flex:1;min-width:220px;border:1px solid var(--border);border-radius:6px;padding:6px 10px;font-size:13px;color:var(--text-primary);background:#fff}
.provider-badge{font-size:10px;font-weight:800;text-transform:uppercase;letter-spacing:.06em;padding:3px 7px;border-radius:4px;white-space:nowrap}
.provider-local{background:#ECFDF5;color:#065F46}
.provider-cloud{background:#FEF3C7;color:#92400E}
.provider-deterministic{background:#F1F5F9;color:#334155}
.ask-provider-disclosure{font-size:11px;color:var(--text-muted);font-style:italic}
.ask-selected-context{font-size:12px;color:var(--text-muted);padding:6px 10px;background:#F8FAFC;border-radius:6px;border:1px solid var(--border)}
.ask-input-row{display:flex;gap:10px;align-items:flex-start}
.ask-input{flex:1;border:1px solid var(--border);border-radius:8px;padding:10px 12px;font-size:13px;font-family:inherit;color:var(--text-primary);resize:vertical;min-height:70px;outline:none}
.ask-input:focus{border-color:var(--blue);box-shadow:0 0 0 2px rgba(37,99,235,.12)}
.ask-btn{background:var(--blue);color:#fff;border:none;border-radius:8px;padding:10px 18px;font-size:14px;font-weight:700;cursor:pointer;white-space:nowrap}
.ask-btn:hover{background:#1d4ed8}
.ask-btn:disabled{opacity:.55;cursor:not-allowed}
.ask-answer-area{background:#F8FAFC;border:1px solid var(--border);border-radius:8px;padding:14px;white-space:pre-wrap;font-size:13px;line-height:1.6;color:var(--text-primary)}
.ask-answer-text strong{font-weight:700}
.ask-references{margin-top:10px;padding-top:10px;border-top:1px solid var(--border)}
.ask-references-label{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--text-muted);margin-bottom:6px}
.ask-actions-row{display:flex;gap:8px;flex-wrap:wrap}
.ask-action-btn{background:#fff;border:1px solid var(--border);border-radius:6px;padding:6px 12px;font-size:12px;font-weight:600;cursor:pointer;color:var(--text-primary)}
.ask-action-btn:hover{border-color:var(--blue);color:var(--blue)}
.ask-suggested-section{margin-top:4px}
.ask-suggested-label{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--text-muted);margin-bottom:8px}
.ask-suggested{display:flex;flex-wrap:wrap;gap:7px}
.ask-suggested-btn{background:#fff;border:1px solid var(--border-strong);border-radius:16px;padding:5px 12px;font-size:12px;font-weight:600;cursor:pointer;color:var(--text-primary);transition:border-color .15s}
.ask-suggested-btn:hover{border-color:var(--blue);color:var(--blue);background:#EFF6FF}
@media print{.console-header,.console-tab-nav,.schedule-top,.schedule-pagination,.ask-panel,.ask-actions-row,.ask-suggested-section,.export-grid{display:none !important}}";
        }

        private static string RenderHeader(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"card hero\">");
            html.AppendLine("<div class=\"eyebrow\">Revit-first owner requirements report</div>");
            html.AppendLine("<h1 id=\"report-title\">" + Encode(state.Title) + "</h1>");
            html.AppendLine("<p id=\"report-subtitle\">" + Encode(state.Subtitle) + "</p>");
            html.AppendLine("<div class=\"identity-grid\">");
            AddIdentityCard(html, "Project", SafeText(state.Report.ProjectName));
            AddIdentityCard(html, "Model", SafeText(state.Report.ModelName));
            AddIdentityCard(html, "Requirements File", FormatSourceFileName(SafeText(state.Report.RequirementsFileName)));
            AddIdentityCard(html, "Generated", FormatTimestamp(state.Report.GeneratedAt));
            AddIdentityCard(html, "Scope", GetScopeLabel(state.Report.Scope));
            AddIdentityCard(html, "Active View", state.ActiveDiscipline);
            AddIdentityCard(html, "Requirements Shown", state.VisibleResults.Count.ToString(CultureInfo.InvariantCulture) + " of " + state.AllResults.Count.ToString(CultureInfo.InvariantCulture));
            AddIdentityCard(html, "Model Elements Reviewed", state.Report.ModelElementCount.ToString(CultureInfo.InvariantCulture));
            html.AppendLine("</div>");
            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderFilterBar(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section filter-panel no-print\">");
            html.AppendLine("<div class=\"filter-label\">View</div>");
            html.AppendLine("<div class=\"filter-group filter-chip-row\" data-filter-group=\"discipline\">");
            html.AppendLine(RenderFilterButton("discipline", "View All", state.ActiveDiscipline, "general", "All Disciplines"));
            foreach (string discipline in DisciplineOrder)
            {
                html.AppendLine(RenderFilterButton("discipline", discipline, state.ActiveDiscipline, DisciplineColorKey(discipline)));
            }
            html.AppendLine("</div>");

            html.AppendLine("<div class=\"filter-label\" style=\"margin-top:14px;\">Status</div>");
            html.AppendLine("<div class=\"filter-group filter-chip-row\" data-filter-group=\"status\">");
            foreach (string status in StatusOrder)
            {
                html.AppendLine(RenderFilterButton("status", status, state.ActiveStatus));
            }
            html.AppendLine("</div>");

            html.AppendLine("<div class=\"filter-label\" style=\"margin-top:14px;\">Urgency</div>");
            html.AppendLine("<div class=\"filter-group filter-chip-row\" data-filter-group=\"urgency\">");
            foreach (string urgency in UrgencyOrder)
            {
                html.AppendLine(RenderFilterButton("urgency", urgency, state.ActiveUrgency));
            }
            html.AppendLine("</div>");

            html.AppendLine("<div class=\"filter-label\" style=\"margin-top:14px;\">Actions</div>");
            html.AppendLine("<div class=\"filter-group\">");
            html.AppendLine("<button type=\"button\" class=\"action-chip\" data-action=\"export\">Export Current View to PDF</button>");
            html.AppendLine("<button type=\"button\" class=\"action-chip\" data-action=\"copy\">Copy Current Summary</button>");
            html.AppendLine("<button type=\"button\" class=\"action-chip\" data-action=\"ask\">Ask EMA AI About This View</button>");
            html.AppendLine("</div>");
            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderFilterButton(string kind, string label, string activeValue, string classKey = null, string filterValue = null)
        {
            string value = string.IsNullOrWhiteSpace(filterValue) ? label : filterValue;
            bool active = string.Equals(value, activeValue, StringComparison.OrdinalIgnoreCase);
            string safeLabel = Encode(label);
            string safeValue = Encode(value);
            string cssClass = "filter-chip";
            if (string.Equals(kind, "discipline", StringComparison.OrdinalIgnoreCase))
            {
                cssClass += " discipline-" + Encode(string.IsNullOrWhiteSpace(classKey) ? DisciplineColorKey(value) : classKey);
            }
            if (active)
            {
                cssClass += " active";
            }
            return "<button type=\"button\" class=\"" + cssClass + "\" data-filter-kind=\"" + Encode(kind) + "\" data-filter-value=\"" + safeValue + "\">" + safeLabel + "</button>";
        }

        private static string RenderExecutiveSummary(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Executive Summary</h2>");
            html.AppendLine("<div class=\"filter-context-banner no-print\" id=\"filter-context-banner\">");
            if (state.IsMaster)
            {
                html.AppendLine("<div class=\"banner-title\">Master View</div>");
                html.AppendLine("<div class=\"banner-detail\">Showing all " + state.AllResults.Count.ToString(CultureInfo.InvariantCulture) + " requirements across all disciplines and statuses.</div>");
            }
            else
            {
                html.AppendLine("<div class=\"banner-title\">Active Filtered View</div>");
                html.AppendLine("<div class=\"banner-detail\">Discipline: " + Encode(state.ActiveDiscipline) + " | Status: " + Encode(state.ActiveStatus) + " | Urgency: " + Encode(state.ActiveUrgency) + "</div>");
                html.AppendLine("<div class=\"banner-detail\">Showing " + state.VisibleResults.Count.ToString(CultureInfo.InvariantCulture) + " of " + state.AllResults.Count.ToString(CultureInfo.InvariantCulture) + " requirements.</div>");
                html.AppendLine("<div class=\"banner-caption\">Counts and scores below reflect this filtered view.</div>");
            }
            html.AppendLine("</div>");
            html.AppendLine("<p class=\"section-copy\">This report is a first-pass evidence review of Owner Requirements using the current Revit model export and available requirement data. It does not certify compliance.</p>");

            html.AppendLine("<div class=\"metric-grid summary-grid\" id=\"summary-grid\">");
            if (state.IsMaster)
            {
                AddMetric(html, "Evidence Review Score", state.VisibleOverallScore.ToString("0.0", CultureInfo.InvariantCulture) + "%", "Summary of how much current evidence supports the reviewed Owner Requirements. This score is not a compliance certification and does not mean the project is fully ready. It is a first-pass evidence review based on the current Revit model export and available requirement data.", "na");
                AddMetric(html, "Total Requirements", state.AllResults.Count.ToString(CultureInfo.InvariantCulture), "Total rows in the source workbook.", "na");
                AddMetric(html, "Met", state.VisibleSummary.MetCount.ToString(CultureInfo.InvariantCulture), "Evidence appears sufficient based on the current model data.", "met");
                AddMetric(html, "Not Met", state.VisibleSummary.NotMetCount.ToString(CultureInfo.InvariantCulture), "Evidence exists, but required values or conditions are missing.", "notmet");
                AddMetric(html, "Needs Human Review", state.VisibleSummary.NeedsHumanReviewCount.ToString(CultureInfo.InvariantCulture), "Requires drawings, specifications, or judgment.", "review");
                AddMetric(html, "Insufficient Model Data", state.VisibleSummary.InsufficientModelDataCount.ToString(CultureInfo.InvariantCulture), "The model snapshot does not contain enough evidence.", "bad");
                AddMetric(html, "Key Issues", state.VisibleIssues.Count.ToString(CultureInfo.InvariantCulture), "Prioritized findings most likely to affect the next deliverable.", "bad");
                AddMetric(html, "Disciplines Impacted", state.DisciplinesImpacted.ToString(CultureInfo.InvariantCulture), "Disciplines with requirements currently in scope.", "na");
            }
            else
            {
                AddMetric(html, "Discipline Score", state.VisibleDisciplineScore.ToString("0.0", CultureInfo.InvariantCulture) + "%", "Deterministic summary for the active discipline view.", "na");
                AddMetric(html, "Discipline Requirements", state.VisibleResults.Count.ToString(CultureInfo.InvariantCulture), "Requirements shown in the current view.", "na");
                AddMetric(html, "Met", state.VisibleSummary.MetCount.ToString(CultureInfo.InvariantCulture), "Evidence appears sufficient based on the current model data.", "met");
                AddMetric(html, "Not Met", state.VisibleSummary.NotMetCount.ToString(CultureInfo.InvariantCulture), "Evidence exists, but required values or conditions are missing.", "notmet");
                AddMetric(html, "Needs Human Review", state.VisibleSummary.NeedsHumanReviewCount.ToString(CultureInfo.InvariantCulture), "Requires drawings, specifications, or judgment.", "review");
                AddMetric(html, "Insufficient Model Data", state.VisibleSummary.InsufficientModelDataCount.ToString(CultureInfo.InvariantCulture), "The model snapshot does not contain enough evidence.", "bad");
                AddMetric(html, "Key Issues", state.VisibleIssues.Count.ToString(CultureInfo.InvariantCulture), "Prioritized findings most likely to affect the active discipline.", "bad");
                AddMetric(html, "Top Next Actions", Math.Min(5, state.TopNextActions.Count).ToString(CultureInfo.InvariantCulture), "Most actionable next steps for this filtered view.", "na");
            }
            html.AppendLine("</div>");

            html.AppendLine("<div class=\"next-actions-callout\"" + (state.TopNextActions.Count == 0 ? " style=\"display:none;\"" : string.Empty) + ">");
            html.AppendLine("<strong>Top Next Actions</strong>");
            html.AppendLine("<ul id=\"top-next-actions\">");
            foreach (string action in state.TopNextActions.Take(5))
            {
                html.AppendLine("<li>" + Encode(action) + "</li>");
            }
            html.AppendLine("</ul>");
            html.AppendLine("</div>");

            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderDisciplineAllocation(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Discipline Allocation</h2>");
            if (state.IsMaster)
            {
                html.AppendLine("<p class=\"section-copy\">Master view by discipline. Use the filter bar to focus the report on one trade without deleting any other requirements.</p>");
                html.AppendLine("<div class=\"discipline-grid\" id=\"discipline-grid\">");
                foreach (DisciplineSummary summary in state.DisciplineSummaries)
                {
                    html.AppendLine(BuildDisciplineCard(summary));
                }
                html.AppendLine("</div>");
            }
            else
            {
                DisciplineSummary focused = state.DisciplineSummaries.FirstOrDefault(item => string.Equals(item.Discipline, state.ActiveDiscipline, StringComparison.OrdinalIgnoreCase));
                html.AppendLine("<p class=\"section-copy\">Focused discipline: " + Encode(state.ActiveDiscipline) + ". Other requirements are not deleted; they are outside the active filter.</p>");
                html.AppendLine("<div class=\"focus-card\">");
                html.AppendLine("<div class=\"group-title\"><h3>" + Encode(state.ActiveDiscipline) + "</h3><div class=\"meta\">Requirements shown: " + state.VisibleResults.Count.ToString(CultureInfo.InvariantCulture) + " | Excluded from this view: " + state.ExcludedCount.ToString(CultureInfo.InvariantCulture) + "</div></div>");
                html.AppendLine("<div class=\"focus-grid\">");
                AddSmallCard(html, "Discipline Score", state.VisibleDisciplineScore.ToString("0.0", CultureInfo.InvariantCulture) + "%");
                AddSmallCard(html, "Key Issues", state.VisibleIssues.Count.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Met", state.VisibleSummary.MetCount.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Not Met", state.VisibleSummary.NotMetCount.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Needs Human Review", state.VisibleSummary.NeedsHumanReviewCount.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Insufficient Model Data", state.VisibleSummary.InsufficientModelDataCount.ToString(CultureInfo.InvariantCulture));
                html.AppendLine("</div>");
                if (focused != null && focused.TopNextActions.Count > 0)
                {
                    html.AppendLine("<div class=\"callout\" style=\"margin-top:12px;\">");
                    html.AppendLine("<strong>Top next actions:</strong><ul style=\"margin:8px 0 0 18px;\">");
                    foreach (string action in focused.TopNextActions.Take(5))
                    {
                        html.AppendLine("<li>" + Encode(action) + "</li>");
                    }
                    html.AppendLine("</ul></div>");
                }
                html.AppendLine("</div>");
                html.AppendLine("<details class=\"traceability no-print\" style=\"margin-top:14px;\">");
                html.AppendLine("<summary>Show all discipline cards</summary>");
                html.AppendLine("<div class=\"discipline-grid\" style=\"margin-top:12px;\">");
                foreach (DisciplineSummary summary in state.DisciplineSummaries)
                {
                    html.AppendLine(BuildDisciplineCard(summary));
                }
                html.AppendLine("</div></details>");
            }
            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string BuildDisciplineCard(DisciplineSummary summary)
        {
            if (summary == null)
            {
                return string.Empty;
            }

            string anchor = BuildDisciplineAnchor(summary.Discipline);
            string disciplineCardClass = DisciplineCardClass(summary.Discipline);
            string primaryColor = DisciplinePrimaryColor(summary.Discipline);
            string bgColor = DisciplineBackgroundColor(summary.Discipline);
            string roleLabel = string.IsNullOrWhiteSpace(summary.ResponsibleRole) ? summary.Discipline : summary.ResponsibleRole;

            StringBuilder html = new StringBuilder();
            html.AppendLine("<article class=\"discipline-card " + disciplineCardClass + "\" data-discipline-row=\"" + Encode(summary.Discipline) + "\" style=\"--discipline-accent:" + primaryColor + ";--discipline-soft:" + bgColor + ";border-left-color:" + primaryColor + ";background:linear-gradient(180deg,#fff 0%,#fff 72%," + bgColor + " 100%);\">");
            html.AppendLine("<div class=\"discipline-card-header\">");
            html.AppendLine("<div class=\"discipline-card-identity\">");
            html.AppendLine("<span class=\"discipline-card-swatch\" aria-hidden=\"true\" style=\"background:" + primaryColor + ";\"></span>");
            html.AppendLine("<div class=\"discipline-card-title-wrap\">");
            html.AppendLine("<span class=\"discipline-card-kicker\">Discipline</span>");
            html.AppendLine("<h3 class=\"discipline-card-name\">" + Encode(summary.Discipline) + "</h3>");
            html.AppendLine("<div class=\"discipline-card-role\">" + Encode(roleLabel) + "</div>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"discipline-card-score\">");
            html.AppendLine("<div class=\"discipline-card-score-value\">" + summary.DisciplineScore.ToString("0.0", CultureInfo.InvariantCulture) + "%</div>");
            html.AppendLine("<div class=\"discipline-card-score-label\">Score</div>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"discipline-card-counts\">");
            html.AppendLine("<span class=\"pill status-met\" style=\"font-size:12px;padding:6px 12px;\">Met " + summary.Met.ToString(CultureInfo.InvariantCulture) + "</span>");
            html.AppendLine("<span class=\"pill status-not-met\" style=\"font-size:12px;padding:6px 12px;\">Not Met " + summary.NotMet.ToString(CultureInfo.InvariantCulture) + "</span>");
            html.AppendLine("<span class=\"pill status-needs-review\" style=\"font-size:12px;padding:6px 12px;\">Review " + summary.NeedsHumanReview.ToString(CultureInfo.InvariantCulture) + "</span>");
            html.AppendLine("<span class=\"pill status-insufficient-data\" style=\"font-size:12px;padding:6px 12px;\">Insufficient " + summary.InsufficientModelData.ToString(CultureInfo.InvariantCulture) + "</span>");
            if (summary.NotApplicable > 0)
            {
                html.AppendLine("<span class=\"pill status-not-applicable\" style=\"font-size:12px;padding:6px 12px;\">N/A " + summary.NotApplicable.ToString(CultureInfo.InvariantCulture) + "</span>");
            }
            html.AppendLine("</div>");
            html.AppendLine("<div style=\"display:flex;justify-content:space-between;align-items:center;margin-top:12px;padding-top:10px;border-top:1px solid var(--border);\">");
            html.AppendLine("<div style=\"font-size:13px;color:var(--muted);\">" + summary.Total.ToString(CultureInfo.InvariantCulture) + " requirements | " + summary.KeyIssueCount.ToString(CultureInfo.InvariantCulture) + " key issues</div>");
            html.AppendLine("<a class=\"discipline-link\" href=\"#" + Encode(anchor) + "\" style=\"font-size:13px;\">View Section &#8594;</a>");
            html.AppendLine("</div>");
            html.AppendLine("</article>");
            return html.ToString();
        }

        private static string RenderStatusLegend()
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section status-legend\">");
            html.AppendLine("<h2>Status and Urgency Legend</h2>");
            html.AppendLine("<p class=\"section-copy\">Status shows what the evidence review found. Urgency shows how soon the team should act.</p>");
            html.AppendLine("<div class=\"legend-grid status-legend-grid\">");
            AddLegendItem(html, "Met", "Evidence appears sufficient for this first-pass review.", "met", "status-legend-card");
            AddLegendItem(html, "Not Met", "Relevant evidence exists, but required values or conditions are missing.", "notmet", "status-legend-card");
            AddLegendItem(html, "Needs Human Review", "Model data alone cannot close this item; drawings, specs, field input, or engineering review are needed.", "review", "status-legend-card");
            AddLegendItem(html, "Insufficient Model Data", "The item appears model-checkable, but the current model/export lacks enough evidence.", "bad", "status-legend-card");
            AddLegendItem(html, "Not Applicable", "Outside the selected discipline, scope, or active filter.", "na", "status-legend-card");
            html.AppendLine("</div>");
            html.AppendLine("<h3 style=\"margin:20px 0 8px;font-size:18px;\">Urgency Legend</h3>");
            html.AppendLine("<div class=\"legend-grid urgency-legend-grid\">");
            AddUrgencyLegendItem(html, "Critical", "Potential deliverable blocker or immediate coordination risk.", "urgency-critical");
            AddUrgencyLegendItem(html, "High", "Likely to affect the next review or deliverable.", "urgency-high");
            AddUrgencyLegendItem(html, "Medium", "Needs action, but is not the first blocker.", "urgency-medium");
            AddUrgencyLegendItem(html, "Low", "Track through normal QA/QC.", "urgency-low");
            AddUrgencyLegendItem(html, "Needs Review", "Priority depends on human review of drawings, specs, or project context.", "urgency-needs-review");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"note-card\" style=\"margin-top:12px;\">Status is the evidence result. Urgency is action priority. This report does not certify compliance.</div>");
            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderKeyIssues(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Key Issues &amp; Recommended Actions</h2>");
            html.AppendLine("<p class=\"section-copy\">Key Issues are elevated findings most likely to affect the next deliverable or require immediate coordination.</p>");

            List<KeyIssue> allIssues = state.Report.KeyIssues ?? new List<KeyIssue>();
            if (allIssues.Count == 0)
            {
                html.AppendLine("<div class=\"empty-state\">No key issues are visible for the active filter.</div>");
            }
            else
            {
                html.AppendLine("<div class=\"issue-grid\" id=\"key-issues-grid\">");
                foreach (KeyIssue issue in allIssues)
                {
                    html.AppendLine(BuildKeyIssueCard(issue, MatchesIssueFilter(issue, state.ActiveDiscipline, state.ActiveStatus, state.ActiveUrgency)));
                }
                html.AppendLine("</div>");
            }

            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string BuildKeyIssueCard(KeyIssue issue, bool initiallyVisible)
        {
            if (issue == null)
            {
                return string.Empty;
            }

            StringBuilder html = new StringBuilder();
            string disciplineClass = DisciplineCssClass(issue.Discipline);
            string disciplineBadgeClass = DisciplineBadgeClass(issue.Discipline);
            string disciplineCardClass = DisciplineCardClass(issue.Discipline);
            string normalizedUrgency = NormalizeUrgency(issue.SeverityLabel);
            string urgencyClass = UrgencyCssClass(normalizedUrgency);
            html.AppendLine("<article class=\"issue-card " + disciplineClass + " " + disciplineCardClass + " " + StatusCssClass(issue.Status) + " " + urgencyClass + "\" data-report-card=\"issue\" data-issue-rank-card=\"true\" data-discipline=\"" + Encode(issue.Discipline) + "\" data-status=\"" + Encode(StatusLabel(issue.Status)) + "\" data-urgency=\"" + Encode(normalizedUrgency) + "\" data-keyissue=\"true\" data-issue-title=\"" + Encode(issue.IssueTitle) + "\" data-confidence=\"" + issue.Confidence.ToString("0.00", CultureInfo.InvariantCulture) + "\"" + (initiallyVisible ? string.Empty : " style=\"display:none\"") + ">");
            html.AppendLine("<div class=\"issue-head\">");
            html.AppendLine("<div>");
            html.AppendLine("<div class=\"issue-title\">#" + issue.Rank.ToString(CultureInfo.InvariantCulture) + " " + Encode(SafeText(issue.IssueTitle)) + "</div>");
            html.AppendLine("<div class=\"issue-meta\">" + Encode(issue.RequirementId) + " | " + Encode(issue.Discipline) + " | " + Encode(issue.SourceWorksheet) + " | Row " + issue.SourceRow.ToString(CultureInfo.InvariantCulture) + "</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div style=\"display:flex;gap:8px;flex-wrap:wrap;align-items:center;justify-content:flex-end\">");
            html.AppendLine("<span class=\"discipline-pill " + disciplineBadgeClass + "\">" + Encode(issue.Discipline) + "</span>");
            html.AppendLine("<span class=\"pill " + IssuePillClass(issue.Status) + "\">" + Encode(StatusLabel(issue.Status)) + "</span>");
            html.AppendLine("<span class=\"pill " + urgencyClass + "\">" + Encode(normalizedUrgency) + "</span>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"detail-grid\">");
            AddField(html, "Why this is urgent", SafeText(issue.UrgencyReason));
            AddField(html, "Evidence gap", SafeText(issue.EvidenceGap));
            AddField(html, "Affected scoped elements", issue.AffectedScopedElements.ToString(CultureInfo.InvariantCulture));
            AddField(html, "Next Best Action", SafeText(issue.NextBestAction));
            AddField(html, "Key Issue Score", Math.Round(issue.KeyIssueScore * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%");
            html.AppendLine("</div>");
            html.AppendLine(BuildKeyIssueDetails(issue));
            html.AppendLine("</article>");
            return html.ToString();
        }

        private static string RenderIssuesByUrgency(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Issues by Urgency</h2>");
            html.AppendLine("<p class=\"section-copy\">Grouped by urgency so the team can see what needs immediate action first.</p>");

            List<KeyIssue> allIssues = state.Report.KeyIssues ?? new List<KeyIssue>();
            foreach (string urgency in new[] { "Critical", "High", "Medium", "Low", "Needs Review" })
            {
                List<KeyIssue> group = allIssues.Where(item => string.Equals(NormalizeUrgency(item.SeverityLabel), urgency, StringComparison.OrdinalIgnoreCase)).OrderByDescending(item => item.KeyIssueScore).ToList();
                string urgencyClass = UrgencyCssClass(urgency);
                string theme = urgency == "Critical"
                    ? "Potential deliverable blocker or immediate coordination risk."
                    : urgency == "High"
                        ? "Likely to affect the next review, deliverable, or discipline handoff."
                        : urgency == "Medium"
                            ? "Needs action, but is not the first blocker."
                            : urgency == "Low"
                                ? "Track through normal QA/QC."
                                : "Priority depends on human review of drawings, specifications, or project context.";

                List<KeyIssue> visibleGroup = group.Where(item => MatchesIssueFilter(item, state.ActiveDiscipline, state.ActiveStatus, state.ActiveUrgency)).ToList();
                List<string> disciplines = visibleGroup.Select(item => SafeText(item.Discipline)).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                html.AppendLine("<div class=\"urgency-group " + urgencyClass + "\" data-urgency-group=\"" + Encode(urgency) + "\" style=\"" + (visibleGroup.Count == 0 ? "display:none;" : string.Empty) + "\">");
                html.AppendLine("<div class=\"urgency-group-header\"><h3 data-group-title style=\"margin:0;font-size:17px;\"><span class=\"pill " + urgencyClass + "\" style=\"margin-right:8px;\">" + Encode(urgency) + "</span>" + visibleGroup.Count.ToString(CultureInfo.InvariantCulture) + " issue(s)</h3><div class=\"meta\" data-group-meta style=\"color:var(--text-muted);font-size:13px;\">Affected: " + Encode(disciplines.Count == 0 ? "None" : string.Join(", ", disciplines)) + " | " + Encode(theme) + "</div></div>");
                html.AppendLine("<div style=\"padding:16px;\">");
                if (visibleGroup.Count == 0)
                {
                    html.AppendLine("<div class=\"empty-state\">No " + Encode(urgency.ToLowerInvariant()) + " urgency issues are visible for the active filter.</div>");
                }
                foreach (KeyIssue issue in group)
                {
                    html.AppendLine(BuildIssueRow(issue, MatchesIssueFilter(issue, state.ActiveDiscipline, state.ActiveStatus, state.ActiveUrgency)));
                }
                html.AppendLine("</div>");
                html.AppendLine("</div>");
            }

            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderRequirementReview(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Discipline Sections</h2>");
            html.AppendLine("<p class=\"section-copy\">Each discipline jumps to its own evidence-backed review. The first page stays executive, while the requirement-by-requirement detail remains below.</p>");

            foreach (string discipline in DisciplineOrder)
            {
                List<RequirementCheckResult> disciplineResults = state.AllResults
                    .Where(result => string.Equals(GetResultDiscipline(result), discipline, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(ResultPriority)
                    .ThenByDescending(item => item != null ? item.Confidence : 0.0)
                    .ThenBy(item => BuildRequirementReference(item))
                    .ToList();

                DisciplineSummary summary = state.DisciplineSummaries.FirstOrDefault(item => string.Equals(item.Discipline, discipline, StringComparison.OrdinalIgnoreCase));
                RequirementCheckSummary disciplineCounts = summary != null
                    ? new RequirementCheckSummary
                    {
                        TotalRequirements = summary.Total,
                        MetCount = summary.Met,
                        NotMetCount = summary.NotMet,
                        NeedsHumanReviewCount = summary.NeedsHumanReview,
                        InsufficientModelDataCount = summary.InsufficientModelData,
                        NotApplicableCount = summary.NotApplicable
                    }
                    : RequirementCheckSummary.FromResults(disciplineResults);

                string disciplineAnchor = BuildDisciplineAnchor(discipline);
                string disciplineSectionClass = DisciplineSectionClass(discipline);
                List<KeyIssue> disciplineIssues = (state.Report.KeyIssues ?? new List<KeyIssue>())
                    .Where(item => string.Equals(SafeText(item.Discipline), discipline, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.KeyIssueScore)
                    .Take(3)
                    .ToList();

                html.AppendLine("<section class=\"discipline-section " + disciplineSectionClass + " " + DisciplineCssClass(discipline) + "\" id=\"" + Encode(disciplineAnchor) + "\" data-discipline-section=\"" + Encode(discipline) + "\">");
                html.AppendLine("<div class=\"group-title\"><h3>" + Encode(discipline) + " Owner Requirements Review</h3><div class=\"meta\"><a class=\"discipline-link\" href=\"#report-title\">Back to Executive Summary</a></div></div>");
                html.AppendLine("<div class=\"focus-grid\">");
                AddSmallCard(html, "Total Requirements", disciplineCounts.TotalRequirements.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Met", disciplineCounts.MetCount.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Not Met", disciplineCounts.NotMetCount.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Needs Human Review", disciplineCounts.NeedsHumanReviewCount.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Insufficient Model Data", disciplineCounts.InsufficientModelDataCount.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Not Applicable", disciplineCounts.NotApplicableCount.ToString(CultureInfo.InvariantCulture));
                AddSmallCard(html, "Discipline Score", summary != null ? summary.DisciplineScore.ToString("0.0", CultureInfo.InvariantCulture) + "%" : ScoreCalculator.CalculateDisciplineScore(disciplineResults, ParseDiscipline(discipline)).ToString("0.0", CultureInfo.InvariantCulture) + "%");
                AddSmallCard(html, "Key Issues", disciplineIssues.Count.ToString(CultureInfo.InvariantCulture));
                html.AppendLine("</div>");

                if (disciplineIssues.Count > 0)
                {
                    html.AppendLine("<div class=\"section-copy\" style=\"margin-top:12px;margin-bottom:8px;font-weight:800;color:var(--navy);\">Top discipline issues</div>");
                    foreach (KeyIssue issue in disciplineIssues)
                    {
                        html.AppendLine(BuildIssueRow(issue, MatchesIssueFilter(issue, state.ActiveDiscipline, state.ActiveStatus, state.ActiveUrgency)));
                    }
                }
                else
                {
                    html.AppendLine("<div class=\"empty-state\" style=\"margin-top:12px;\">No top discipline issues are visible for this section.</div>");
                }

                html.AppendLine("<div class=\"section-copy\" style=\"margin-top:14px;margin-bottom:8px;font-weight:800;color:var(--navy);\">Requirement-by-Requirement Detail</div>");
                if (disciplineResults.Count == 0)
                {
                    html.AppendLine("<div class=\"empty-state\">No requirements found for this discipline.</div>");
                }
                else
                {
                    html.AppendLine("<div class=\"result-list\">");
                    foreach (RequirementCheckResult result in disciplineResults)
                    {
                        bool visible = MatchesFilter(result, state.ActiveDiscipline, state.ActiveStatus, state.ActiveUrgency);
                        html.AppendLine(BuildResultCard(result, visible));
                    }
                    html.AppendLine("</div>");
                }

                html.AppendLine("</section>");
            }

            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderEvidenceAndTraceability(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Evidence &amp; Traceability</h2>");
            html.AppendLine("<p class=\"section-copy\">Traceability is compact enough for management review and detailed enough for follow-up. Full file paths live here instead of the main header.</p>");
            html.AppendLine("<div class=\"traceability-grid\">");
            AddTraceabilityChip(html, "Source Worksheet", state.VisibleResults.Count > 0 ? SafeText(state.VisibleResults[0].SourceWorksheet) : "(not set)");
            AddTraceabilityChip(html, "Source Row", state.VisibleResults.Count > 0 ? state.VisibleResults[0].SourceRow.ToString(CultureInfo.InvariantCulture) : "(not set)");
            AddTraceabilityChip(html, "Requirement Rows Shown", state.VisibleResults.Count.ToString(CultureInfo.InvariantCulture));
            AddTraceabilityChip(html, "Source Files", state.Report.Results != null && state.Report.Results.Count > 0 ? CountDistinctSourceFiles(state.Report.Results).ToString(CultureInfo.InvariantCulture) : "0");
            html.AppendLine("</div>");

            if (!string.IsNullOrWhiteSpace(state.Report.RequirementsFilePath) || !string.IsNullOrWhiteSpace(state.Report.ReportPath))
            {
                html.AppendLine("<details class=\"traceability no-print\" style=\"margin-top:14px;\">");
                html.AppendLine("<summary>Show traceability paths and report location</summary>");
                html.AppendLine("<div class=\"callout\" style=\"margin-top:10px;\">");
                if (!string.IsNullOrWhiteSpace(state.Report.RequirementsFilePath))
                {
                    html.AppendLine("<div class=\"report-path\"><strong>Requirements file:</strong> " + Encode(state.Report.RequirementsFilePath) + "</div>");
                }
                if (!string.IsNullOrWhiteSpace(state.Report.ReportPath))
                {
                    html.AppendLine("<div class=\"report-path\"><strong>Report file:</strong> " + Encode(state.Report.ReportPath) + "</div>");
                }
                html.AppendLine("</div>");
                html.AppendLine("</details>");
            }

            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderAskEmaAi(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"ask-ema-section report-section\" id=\"ask-ema-ai\">");
            html.AppendLine("<h2>Ask EMA AI</h2>");
            html.AppendLine("<p class=\"section-copy\">Ask questions about this report. The assistant explains and summarizes the report, but it does not certify compliance or change official results.</p>");
            html.AppendLine("<div class=\"question-grid\" id=\"ask-questions\" style=\"gap:12px;\">");
            foreach (string question in state.SuggestedQuestions)
            {
                html.AppendLine("<div class=\"question-card\"><span class=\"label\" style=\"display:block;color:var(--text-muted);font-size:10px;text-transform:uppercase;letter-spacing:.06em;margin-bottom:4px;font-weight:600;\">Suggested question</span><div style=\"font-size:14px;font-weight:600;color:var(--text-primary);\">" + Encode(question) + "</div></div>");
            }
            html.AppendLine("</div>");
            html.AppendLine("<div style=\"margin-top:14px;padding:12px 16px;background:rgba(37,99,235,.05);border-radius:var(--radius-sm);color:var(--text-muted);font-size:13px;\">Ask EMA AI explains the report. It does not certify compliance or change official statuses.</div>");
            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderConsoleHeader(ReportViewState state)
        {
            string projectName = Encode(SafeText(state.Report.ProjectName));
            string generatedAt = Encode(FormatTimestamp(state.Report.GeneratedAt));
            string rowCount = state.AllResults.Count.ToString(CultureInfo.InvariantCulture);
            string elementCount = state.Report.ModelElementCount.ToString(CultureInfo.InvariantCulture);

            StringBuilder html = new StringBuilder();
            html.AppendLine("<header class=\"console-header no-print\">");
            html.AppendLine("<div class=\"console-header-inner\">");
            html.AppendLine("<div class=\"console-header-identity\">");
            html.AppendLine("<div class=\"console-eyebrow\">EMA AI &mdash; Owner Requirements Schedule</div>");
            html.AppendLine("<div class=\"console-identity-chips\">");
            html.AppendLine("<span class=\"identity-chip\"><span class=\"ch-label\">Project:</span><span class=\"ch-value\">" + projectName + "</span></span>");
            html.AppendLine("<span class=\"identity-chip\"><span class=\"ch-label\">Generated:</span><span class=\"ch-value\">" + generatedAt + "</span></span>");
            html.AppendLine("<span class=\"identity-chip\"><span class=\"ch-label\">Requirements:</span><span class=\"ch-value\">" + rowCount + " rows</span></span>");
            html.AppendLine("<span class=\"identity-chip\"><span class=\"ch-label\">Elements:</span><span class=\"ch-value\">" + elementCount + "</span></span>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"console-toolbar\">");
            html.AppendLine("<button type=\"button\" class=\"toolbar-btn\" data-action=\"export\" title=\"Print or save as PDF\">Print / PDF</button>");
            html.AppendLine("<button type=\"button\" class=\"toolbar-btn\" data-action=\"copy\" title=\"Copy executive summary to clipboard\">Copy Summary</button>");
            html.AppendLine("<button type=\"button\" class=\"toolbar-btn\" data-export=\"csv-requirements\" title=\"Export current view to CSV\">Export CSV</button>");
            html.AppendLine("<span class=\"toolbar-divider\"></span>");
            html.AppendLine("<button type=\"button\" class=\"toolbar-btn toolbar-btn-disabled\" disabled title=\"Select Revit elements &mdash; bridge not yet active\">Select Elements</button>");
            html.AppendLine("<button type=\"button\" class=\"toolbar-btn toolbar-btn-disabled\" disabled title=\"Isolate in Revit &mdash; bridge not yet active\">Isolate</button>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("</header>");
            return html.ToString();
        }

        private static string RenderTabNav(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<nav class=\"console-tab-nav no-print\" role=\"tablist\" aria-label=\"Report sections\">");
            html.AppendLine("<button type=\"button\" class=\"tab-btn active\" data-tab=\"summary\" role=\"tab\" aria-selected=\"true\">Summary</button>");
            html.AppendLine("<button type=\"button\" class=\"tab-btn\" data-tab=\"requirements\" role=\"tab\">Requirements</button>");
            html.AppendLine("<button type=\"button\" class=\"tab-btn\" data-tab=\"evidence\" role=\"tab\">Evidence</button>");
            html.AppendLine("<button type=\"button\" class=\"tab-btn\" data-tab=\"elements\" role=\"tab\">Elements</button>");
            html.AppendLine("<button type=\"button\" class=\"tab-btn\" data-tab=\"rules\" role=\"tab\">Rules</button>");
            html.AppendLine("<button type=\"button\" class=\"tab-btn\" data-tab=\"exports\" role=\"tab\">Exports</button>");
            html.AppendLine("<button type=\"button\" class=\"tab-btn tab-btn-ai\" data-tab=\"ask\" role=\"tab\">Ask EMA AI</button>");
            html.AppendLine("<span class=\"tab-spacer\"></span>");
            html.AppendLine("<span class=\"tab-info\" id=\"tab-schedule-count\"></span>");
            html.AppendLine("</nav>");
            return html.ToString();
        }

        private static string RenderRequirementsTab(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"schedule-console\">");

            // Schedule filter bar
            html.AppendLine("<div class=\"schedule-top\">");
            html.AppendLine("<input type=\"text\" id=\"schedule-search\" class=\"schedule-search-input\" placeholder=\"Search rows, types, disciplines, evidence...\" autocomplete=\"off\" />");
            html.AppendLine("<select id=\"schedule-discipline\" class=\"schedule-select\">");
            html.AppendLine("<option value=\"\">All Disciplines</option>");
            foreach (string d in DisciplineOrder)
            {
                html.AppendLine("<option value=\"" + Encode(d) + "\">" + Encode(d) + "</option>");
            }
            html.AppendLine("</select>");
            html.AppendLine("<select id=\"schedule-status\" class=\"schedule-select\">");
            html.AppendLine("<option value=\"\">All Statuses</option>");
            foreach (string s in new[] { "Met", "Not Met", "Needs Human Review", "Insufficient Model Data", "Not Applicable" })
            {
                html.AppendLine("<option value=\"" + Encode(s) + "\">" + Encode(s) + "</option>");
            }
            html.AppendLine("</select>");
            html.AppendLine("<select id=\"schedule-urgency\" class=\"schedule-select\">");
            html.AppendLine("<option value=\"\">All Urgency</option>");
            foreach (string u in new[] { "Critical", "High", "Medium", "Low", "Needs Review" })
            {
                html.AppendLine("<option value=\"" + Encode(u) + "\">" + Encode(u) + "</option>");
            }
            html.AppendLine("</select>");
            html.AppendLine("<select id=\"schedule-group-by\" class=\"schedule-select\">");
            html.AppendLine("<option value=\"\">No Grouping</option>");
            html.AppendLine("<option value=\"discipline\">By Discipline</option>");
            html.AppendLine("<option value=\"status\">By Status</option>");
            html.AppendLine("<option value=\"urgency\">By Urgency</option>");
            html.AppendLine("<option value=\"requirement_type\">By Type</option>");
            html.AppendLine("<option value=\"evidence_alignment\">By Evidence</option>");
            html.AppendLine("<option value=\"source_worksheet\">By Worksheet</option>");
            html.AppendLine("</select>");
            html.AppendLine("<button type=\"button\" class=\"schedule-clear-btn\" id=\"schedule-clear\">Clear</button>");
            html.AppendLine("<span class=\"schedule-count\" id=\"schedule-row-count\">Loading...</span>");
            html.AppendLine("</div>");

            // Schedule grid
            html.AppendLine("<div class=\"schedule-grid-wrap\">");
            html.AppendLine("<table class=\"schedule-grid\" id=\"requirements-schedule-table\">");
            html.AppendLine("<thead>");
            html.AppendLine("<tr>");
            html.AppendLine("<th style=\"width:28px\"></th>"); // expand
            html.AppendLine("<th style=\"width:24px\"></th>"); // select
            html.AppendLine("<th class=\"sortable\" data-sort=\"source_row\">Row <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"discipline\">Discipline <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"status\">Status <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"urgency\">Urgency <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"requirement_type\">Type <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"evidence_alignment\">Evidence <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"confidence\" title=\"Confidence\">Conf. <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"_directCount\" title=\"Direct Evidence Count\">Direct <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"_missingCount\" title=\"Missing Evidence Count\">Missing <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th class=\"sortable\" data-sort=\"matched_element_count\">Elements <span class=\"sort-icon\">&#8597;</span></th>");
            html.AppendLine("<th>Next Action</th>");
            html.AppendLine("<th title=\"Ask EMA AI about this requirement\">AI</th>");
            html.AppendLine("</tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody id=\"schedule-tbody\"><tr><td colspan=\"14\" style=\"text-align:center;padding:28px;color:#64748B\">Loading schedule from report data...</td></tr></tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            // Pagination
            html.AppendLine("<div id=\"schedule-pagination\" class=\"schedule-pagination\"></div>");

            // Row detail panel
            html.AppendLine("<div id=\"schedule-detail-panel\" class=\"schedule-detail-panel\" style=\"display:none\"></div>");

            // Legacy card view (collapsed)
            html.AppendLine("<details class=\"schedule-legacy-details\">");
            html.AppendLine("<summary>Show Full Discipline Card View (Legacy)</summary>");
            html.AppendLine("<div style=\"margin-top:14px;\">");
            html.AppendLine(RenderRequirementReview(state));
            html.AppendLine("</div>");
            html.AppendLine("</details>");

            html.AppendLine("</div>"); // schedule-console
            return html.ToString();
        }

        private static string RenderEvidenceTab(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"tab-panel-body\">");
            html.AppendLine("<div class=\"page\">");
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Evidence Browser</h2>");
            html.AppendLine("<p class=\"section-copy\">Browse captured evidence by parameter, category, or rule type. Switch views to find evidence patterns across the report.</p>");
            html.AppendLine("<div class=\"evidence-view-nav\">");
            html.AppendLine("<button type=\"button\" class=\"evidence-view-btn active\" data-evidence-view=\"parameter\">By Parameter</button>");
            html.AppendLine("<button type=\"button\" class=\"evidence-view-btn\" data-evidence-view=\"rule\">By Rule Type</button>");
            html.AppendLine("<button type=\"button\" class=\"evidence-view-btn\" data-evidence-view=\"category\">By Category</button>");
            html.AppendLine("<button type=\"button\" class=\"evidence-view-btn\" data-evidence-view=\"row\">By Source Row</button>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"schedule-grid-wrap\" style=\"margin-top:12px;\">");
            html.AppendLine("<table class=\"schedule-grid\" id=\"evidence-table\">");
            html.AppendLine("<thead><tr><th>Parameter / Type</th><th>Requirements</th><th>Direct Evidence</th><th>Missing</th><th>Disciplines</th></tr></thead>");
            html.AppendLine("<tbody id=\"evidence-tbody\"><tr><td colspan=\"5\" style=\"text-align:center;padding:18px;color:#64748B\">Open Evidence tab to load.</td></tr></tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
            html.AppendLine("</section>");
            html.AppendLine(RenderEvidenceAndTraceability(state));
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static string RenderElementsTab(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"tab-panel-body\">");
            html.AppendLine("<div class=\"page\">");
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Elements Traceability</h2>");
            html.AppendLine("<p class=\"section-copy\">All Revit model elements matched to requirements. Copy IDs for coordination. Select / Isolate / Zoom will be enabled in the next bridge pass.</p>");
            html.AppendLine("<div style=\"display:flex;gap:10px;align-items:center;margin-bottom:12px;flex-wrap:wrap\">");
            html.AppendLine("<input type=\"text\" id=\"elements-search\" class=\"schedule-search-input\" style=\"max-width:340px\" placeholder=\"Search element ID, category, family, type...\" autocomplete=\"off\" />");
            html.AppendLine("<span class=\"schedule-count\" id=\"elements-count\">Loading...</span>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"schedule-grid-wrap\">");
            html.AppendLine("<table class=\"schedule-grid\" id=\"elements-table\">");
            html.AppendLine("<thead><tr><th>Element ID</th><th>Category</th><th>Family</th><th>Type</th><th>Level</th><th>Requirements</th><th>Evidence Role</th><th>Missing Parameters</th><th>Actions</th></tr></thead>");
            html.AppendLine("<tbody id=\"elements-tbody\"><tr><td colspan=\"9\" style=\"text-align:center;padding:18px;color:#64748B\">Open Elements tab to load.</td></tr></tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
            html.AppendLine("<div id=\"elements-pagination\" class=\"schedule-pagination\"></div>");
            html.AppendLine("<div class=\"note-card\" style=\"margin-top:14px;\">Select / Isolate / Zoom to Revit elements will be enabled in the next bridge pass. Copy actions work now.</div>");
            html.AppendLine("</section>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static string RenderRulesTab(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"tab-panel-body\">");
            html.AppendLine("<div class=\"page\">");
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Rule Logic / Taxonomy</h2>");
            html.AppendLine("<p class=\"section-copy\">Why each requirement type can or cannot be closed from model evidence alone. Use this to understand evidence guardrails and false-positive risk.</p>");
            html.AppendLine("<div class=\"schedule-grid-wrap\">");
            html.AppendLine("<table class=\"schedule-grid\" id=\"rules-table\">");
            html.AppendLine("<thead><tr><th>Requirement Type</th><th>Validation Type</th><th>Direct Evidence Required</th><th>Expected Categories</th><th>Expected Parameters</th><th>False Positive Risk</th><th>Why Model May Not Close</th><th>Example Rows</th></tr></thead>");
            html.AppendLine("<tbody id=\"rules-tbody\"><tr><td colspan=\"8\" style=\"text-align:center;padding:18px;color:#64748B\">Open Rules tab to load.</td></tr></tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
            html.AppendLine("</section>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static string RenderExportsTab(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"tab-panel-body\">");
            html.AppendLine("<div class=\"page\">");
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Export Center</h2>");
            html.AppendLine("<p class=\"section-copy\">Export filtered requirements, action lists, missing evidence, or element traceability. CSV is preferred. PDF is for sharing snapshots.</p>");
            html.AppendLine("<div class=\"export-grid\">");

            AppendExportCard(html, "Current Filtered Requirements", "All visible requirements with status, urgency, type, and next action.", "Export Requirements CSV", "csv-requirements");
            AppendExportCard(html, "Not Met Action List", "Requirements with Not Met status. Includes discipline, row, next action, and missing evidence.", "Export Not Met CSV", "csv-notmet");
            AppendExportCard(html, "Needs Human Review", "Items that require drawings, specs, or engineering judgment. Cannot be closed from model data alone.", "Export Review CSV", "csv-review");
            AppendExportCard(html, "Missing Evidence", "Requirements with missing direct evidence including parameter names and next actions.", "Export Missing CSV", "csv-missing");
            AppendExportCard(html, "Element Traceability", "Matched Revit elements with category, family, type, and linked requirements.", "Export Elements CSV", "csv-elements");

            html.AppendLine("<div class=\"export-card\">");
            html.AppendLine("<div class=\"export-card-title\">Copy Summary</div>");
            html.AppendLine("<div class=\"export-card-desc\">Copies the executive summary to clipboard for email or meeting notes.</div>");
            html.AppendLine("<button type=\"button\" class=\"export-card-btn\" data-action=\"copy\">Copy to Clipboard</button>");
            html.AppendLine("</div>");

            html.AppendLine("<div class=\"export-card\">");
            html.AppendLine("<div class=\"export-card-title\">Print / PDF</div>");
            html.AppendLine("<div class=\"export-card-desc\">Opens the browser print dialog. Save as PDF for sharing.</div>");
            html.AppendLine("<button type=\"button\" class=\"export-card-btn\" data-action=\"export\">Print / Save PDF</button>");
            html.AppendLine("</div>");

            html.AppendLine("</div>"); // export-grid
            html.AppendLine("</section>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static void AppendExportCard(StringBuilder html, string title, string desc, string btnLabel, string exportKey)
        {
            html.AppendLine("<div class=\"export-card\">");
            html.AppendLine("<div class=\"export-card-title\">" + Encode(title) + "</div>");
            html.AppendLine("<div class=\"export-card-desc\">" + Encode(desc) + "</div>");
            html.AppendLine("<button type=\"button\" class=\"export-card-btn\" data-export=\"" + Encode(exportKey) + "\">" + Encode(btnLabel) + "</button>");
            html.AppendLine("</div>");
        }

        private static string RenderAskEmaAiTab(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"tab-panel-body\">");
            html.AppendLine("<div class=\"page\">");
            html.AppendLine("<section class=\"ask-ema-section report-section\" id=\"ask-ema-ai\">");
            html.AppendLine("<h2>Ask EMA AI</h2>");
            html.AppendLine("<p class=\"section-copy\">Ask questions grounded in this Owner Requirements report. Answers come from the report data, not general AI knowledge. This assistant does not certify compliance or change official results.</p>");
            html.AppendLine("<div class=\"ask-panel\">");

            // Model selector row
            html.AppendLine("<div class=\"ask-model-row\">");
            html.AppendLine("<label class=\"ask-row-label\" for=\"ask-model-select\">Model</label>");
            html.AppendLine("<select id=\"ask-model-select\" class=\"ask-model-select\">");
            html.AppendLine("<option value=\"deterministic\" selected>Deterministic Report Summary (No AI &mdash; stays local)</option>");
            html.AppendLine("<option value=\"ollama/qwen3.6:35b\">Local Ollama &mdash; Qwen 3.6 35B</option>");
            html.AppendLine("<option value=\"ollama/granite4.1:30b\">Local Ollama &mdash; Granite 4.1 30B</option>");
            html.AppendLine("<option value=\"ollama/gemma4:26b\">Local Ollama &mdash; Gemma 4 26B</option>");
            html.AppendLine("<option value=\"ollama/gemma4:31b\">Local Ollama &mdash; Gemma 4 31B</option>");
            html.AppendLine("<option value=\"ollama/qwen3-coder:30b\">Local Ollama &mdash; Qwen3 Coder 30B</option>");
            html.AppendLine("<option value=\"ollama/deepseek-r1:32b\">Local Ollama &mdash; DeepSeek R1 32B</option>");
            html.AppendLine("<option value=\"openrouter/anthropic/claude-sonnet-4\">OpenRouter &mdash; Claude Sonnet 4 (cloud)</option>");
            html.AppendLine("<option value=\"openrouter/openai/gpt-4.1\">OpenRouter &mdash; GPT-4.1 (cloud)</option>");
            html.AppendLine("</select>");
            html.AppendLine("<span id=\"ask-provider-badge\" class=\"provider-badge provider-local\">Local</span>");
            html.AppendLine("<span id=\"ask-provider-disclosure\" class=\"ask-provider-disclosure\">Report data stays local. No external calls.</span>");
            html.AppendLine("</div>");

            // Context scope row
            html.AppendLine("<div class=\"ask-context-row\">");
            html.AppendLine("<label class=\"ask-row-label\" for=\"ask-context-scope\">Context</label>");
            html.AppendLine("<select id=\"ask-context-scope\" class=\"ask-context-scope\">");
            html.AppendLine("<option value=\"selected\">Selected Requirement</option>");
            html.AppendLine("<option value=\"filtered\" selected>Current Filtered View</option>");
            html.AppendLine("<option value=\"discipline\">Current Discipline</option>");
            html.AppendLine("<option value=\"key_issues\">Key Issues</option>");
            html.AppendLine("<option value=\"summary\">Whole Report Summary</option>");
            html.AppendLine("</select>");
            html.AppendLine("<div id=\"ask-selected-context\" class=\"ask-selected-context\">No requirement selected. Using report summary as context.</div>");
            html.AppendLine("</div>");

            // Input row
            html.AppendLine("<div class=\"ask-input-row\">");
            html.AppendLine("<textarea id=\"ask-input\" class=\"ask-input\" rows=\"3\" placeholder=\"Ask about this Owner Requirements report...\nExamples: Why is Row 478 Not Met? | Summarize Mechanical Not Met | What evidence is missing?\"></textarea>");
            html.AppendLine("<button type=\"button\" id=\"ask-btn\" class=\"ask-btn\">Ask</button>");
            html.AppendLine("</div>");

            // Answer area
            html.AppendLine("<div class=\"ask-answer-area\" id=\"ask-answer-area\" style=\"display:none\">");
            html.AppendLine("<div id=\"ask-answer-text\" class=\"ask-answer-text\"></div>");
            html.AppendLine("<div id=\"ask-references\" class=\"ask-references\" style=\"display:none\">");
            html.AppendLine("<div class=\"ask-references-label\">References</div>");
            html.AppendLine("<div id=\"ask-references-list\"></div>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            // Action row
            html.AppendLine("<div class=\"ask-actions-row\" id=\"ask-actions-row\" style=\"display:none\">");
            html.AppendLine("<button type=\"button\" class=\"ask-action-btn\" id=\"ask-copy-answer\">Copy Answer</button>");
            html.AppendLine("<button type=\"button\" class=\"ask-action-btn\" id=\"ask-copy-context\">Copy Context</button>");
            html.AppendLine("<button type=\"button\" class=\"ask-action-btn\" id=\"ask-clear\">Clear</button>");
            html.AppendLine("</div>");

            // Suggested questions
            html.AppendLine("<div class=\"ask-suggested-section\">");
            html.AppendLine("<div class=\"ask-suggested-label\">Suggested Questions</div>");
            html.AppendLine("<div class=\"ask-suggested\" id=\"ask-questions\">");
            foreach (string question in state.SuggestedQuestions)
            {
                html.AppendLine("<button type=\"button\" class=\"ask-suggested-btn\" data-question=\"" + Encode(question) + "\">" + Encode(question) + "</button>");
            }
            html.AppendLine("<button type=\"button\" class=\"ask-suggested-btn\" data-question=\"Why is Row 478 Not Met?\">Why is Row 478 Not Met?</button>");
            html.AppendLine("<button type=\"button\" class=\"ask-suggested-btn\" data-question=\"Summarize Mechanical Not Met requirements\">Summarize Mechanical Not Met requirements</button>");
            html.AppendLine("<button type=\"button\" class=\"ask-suggested-btn\" data-question=\"What evidence is missing for this requirement?\">What evidence is missing for this requirement?</button>");
            html.AppendLine("<button type=\"button\" class=\"ask-suggested-btn\" data-question=\"List top critical issues\">List top critical issues</button>");
            html.AppendLine("<button type=\"button\" class=\"ask-suggested-btn\" data-question=\"Can this be closed from model data?\">Can this be closed from model data?</button>");
            html.AppendLine("<button type=\"button\" class=\"ask-suggested-btn\" data-question=\"Show missing manufacturer evidence\">Show missing manufacturer evidence</button>");
            html.AppendLine("<button type=\"button\" class=\"ask-suggested-btn\" data-question=\"Draft action items for tomorrow\">Draft action items for tomorrow</button>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            html.AppendLine("</div>"); // ask-panel
            html.AppendLine("<div class=\"note-card\" style=\"margin-top:14px;\">Ask EMA AI explains the report. It does not certify compliance or change official statuses. Deterministic mode answers from report data only.</div>");
            html.AppendLine("</section>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            return html.ToString();
        }

        private const int MaxCoherenceFindingsInHtml = 60;

        private static string RenderCoherenceAudit(ReportViewState state)
        {
            RequirementCoherenceReport coherence = state.Coherence ?? new RequirementCoherenceReport();

            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section\" id=\"coherence-audit\">");
            html.AppendLine("<h2>Requirement Coherence Audit</h2>");
            html.AppendLine("<p class=\"section-copy\">Every requirement is cross-checked against every other requirement, and rolled up per requirement type, to surface duplicates and conflicting obligations. Coherence findings never change a requirement&#39;s Met / Not Met status &mdash; they flag obligations that disagree and need human resolution.</p>");

            string gradeColor = CoherenceGradeColor(coherence.CoherenceGrade);
            html.AppendLine("<div class=\"note-card\" style=\"display:flex;flex-wrap:wrap;gap:18px;align-items:center;\">");
            html.AppendLine("<span style=\"font-weight:700;color:" + gradeColor + ";\">Coherence: " + Encode(coherence.CoherenceGrade ?? "Coherent") + "</span>");
            html.AppendLine("<span>Requirements analyzed: <strong>" + coherence.RequirementsAnalyzed + "</strong></span>");
            html.AppendLine("<span>Requirement types: <strong>" + coherence.RequirementTypesAnalyzed + "</strong></span>");
            html.AppendLine("<span>Exact duplicates: <strong>" + coherence.ExactDuplicateCount + "</strong></span>");
            html.AppendLine("<span>Semantic duplicates: <strong>" + coherence.SemanticDuplicateCount + "</strong></span>");
            html.AppendLine("<span>Numeric conflicts: <strong>" + coherence.NumericConflictCount + "</strong></span>");
            html.AppendLine("<span>Quantity conflicts: <strong>" + coherence.QuantityConflictCount + "</strong></span>");
            html.AppendLine("<span>Manufacturer conflicts: <strong>" + coherence.ManufacturerConflictCount + "</strong></span>");
            html.AppendLine("</div>");

            // Per-requirement-type coherence table.
            List<RequirementTypeCoherenceSummary> typeSummaries = coherence.TypeSummaries ?? new List<RequirementTypeCoherenceSummary>();
            if (typeSummaries.Count > 0)
            {
                html.AppendLine("<h3 style=\"margin-top:16px;\">Coherence by requirement type</h3>");
                html.AppendLine("<table style=\"width:100%;border-collapse:collapse;font-size:13px;\">");
                html.AppendLine("<thead><tr>" +
                    "<th style=\"text-align:left;padding:6px 8px;border-bottom:1px solid var(--border);\">Requirement type</th>" +
                    "<th style=\"text-align:right;padding:6px 8px;border-bottom:1px solid var(--border);\">Requirements</th>" +
                    "<th style=\"text-align:right;padding:6px 8px;border-bottom:1px solid var(--border);\">Findings</th>" +
                    "<th style=\"text-align:right;padding:6px 8px;border-bottom:1px solid var(--border);\">Duplicates</th>" +
                    "<th style=\"text-align:right;padding:6px 8px;border-bottom:1px solid var(--border);\">Conflicts</th>" +
                    "<th style=\"text-align:left;padding:6px 8px;border-bottom:1px solid var(--border);\">Status</th>" +
                    "</tr></thead><tbody>");
                foreach (RequirementTypeCoherenceSummary summary in typeSummaries)
                {
                    string statusLabel = summary.IsCoherent
                        ? "<span style=\"color:#15803D;\">Coherent</span>"
                        : "<span style=\"color:" + CoherenceSeverityColor(summary.HighestSeverity) + ";\">" + Encode(SeverityName(summary.HighestSeverity)) + "</span>";
                    html.AppendLine("<tr>" +
                        "<td style=\"padding:6px 8px;border-bottom:1px solid var(--border);\">" + Encode(summary.RequirementType ?? "unclassified") + "</td>" +
                        "<td style=\"padding:6px 8px;border-bottom:1px solid var(--border);text-align:right;\">" + summary.RequirementCount + "</td>" +
                        "<td style=\"padding:6px 8px;border-bottom:1px solid var(--border);text-align:right;\">" + summary.FindingCount + "</td>" +
                        "<td style=\"padding:6px 8px;border-bottom:1px solid var(--border);text-align:right;\">" + summary.DuplicateCount + "</td>" +
                        "<td style=\"padding:6px 8px;border-bottom:1px solid var(--border);text-align:right;\">" + summary.ConflictCount + "</td>" +
                        "<td style=\"padding:6px 8px;border-bottom:1px solid var(--border);\">" + statusLabel + "</td>" +
                        "</tr>");
                }
                html.AppendLine("</tbody></table>");
            }

            // Individual findings (capped, with honest truncation note).
            List<CoherenceFinding> findings = coherence.Findings ?? new List<CoherenceFinding>();
            if (findings.Count == 0)
            {
                html.AppendLine("<div class=\"callout\" style=\"margin-top:12px;\">No duplicate or conflicting requirements were detected across the requirement set.</div>");
            }
            else
            {
                html.AppendLine("<details class=\"traceability\" style=\"margin-top:12px;\" open>");
                html.AppendLine("<summary>Coherence findings (" + findings.Count + ")</summary>");
                int shown = 0;
                foreach (CoherenceFinding finding in findings)
                {
                    if (shown >= MaxCoherenceFindingsInHtml)
                    {
                        break;
                    }
                    html.AppendLine(BuildCoherenceFindingCard(finding));
                    shown++;
                }
                if (findings.Count > MaxCoherenceFindingsInHtml)
                {
                    html.AppendLine("<div class=\"callout\" style=\"margin-top:10px;\">Showing " + MaxCoherenceFindingsInHtml + " of " + findings.Count + " findings. The full set is available in the evaluation bundle (coherence_findings.json).</div>");
                }
                html.AppendLine("</details>");
            }

            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string BuildCoherenceFindingCard(CoherenceFinding finding)
        {
            StringBuilder html = new StringBuilder();
            string color = CoherenceSeverityColor(finding.Severity);
            html.AppendLine("<div class=\"callout\" style=\"margin-top:10px;border-left:3px solid " + color + ";\">");
            html.AppendLine("<div><strong style=\"color:" + color + ";\">" + Encode(finding.FindingTypeLabel) + "</strong> &middot; " + Encode(finding.SeverityLabel) + " &middot; type: " + Encode(finding.RequirementType ?? "unclassified") + "</div>");
            html.AppendLine("<div style=\"margin-top:4px;\">" + Encode(finding.Rationale ?? string.Empty) + "</div>");
            html.AppendLine("<div style=\"margin-top:4px;font-size:12px;color:var(--text-muted);\">" + Encode(FormatRef(finding.Primary)));
            if (finding.Related != null)
            {
                html.Append(" &harr; " + Encode(FormatRef(finding.Related)));
            }
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static string FormatRef(RequirementRef reference)
        {
            if (reference == null)
            {
                return "(unknown requirement)";
            }
            string id = string.IsNullOrWhiteSpace(reference.RequirementId) ? "(no id)" : reference.RequirementId;
            string sheet = string.IsNullOrWhiteSpace(reference.SourceWorksheet) ? string.Empty : reference.SourceWorksheet + " ";
            string row = reference.SourceRow > 0 ? "row " + reference.SourceRow.ToString(CultureInfo.InvariantCulture) : string.Empty;
            string locator = (sheet + row).Trim();
            return string.IsNullOrEmpty(locator) ? id : id + " (" + locator + ")";
        }

        private static string CoherenceGradeColor(string grade)
        {
            if (string.Equals(grade, "Conflicts Found", StringComparison.OrdinalIgnoreCase))
            {
                return "#B91C1C";
            }
            if (string.Equals(grade, "Minor Issues", StringComparison.OrdinalIgnoreCase))
            {
                return "#B45309";
            }
            return "#15803D";
        }

        private static string CoherenceSeverityColor(CoherenceSeverity severity)
        {
            switch (severity)
            {
                case CoherenceSeverity.Critical: return "#7F1D1D";
                case CoherenceSeverity.High: return "#B91C1C";
                case CoherenceSeverity.Medium: return "#B45309";
                case CoherenceSeverity.Low: return "#2563EB";
                default: return "#64748B";
            }
        }

        private static string SeverityName(CoherenceSeverity severity)
        {
            switch (severity)
            {
                case CoherenceSeverity.Critical: return "Critical";
                case CoherenceSeverity.High: return "High";
                case CoherenceSeverity.Medium: return "Medium";
                case CoherenceSeverity.Low: return "Low";
                default: return "Info";
            }
        }

        private static string RenderReportNotes(ReportViewState state)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<section class=\"report-section\">");
            html.AppendLine("<h2>Report Notes / No-Overclaim Boundary</h2>");
            html.AppendLine("<p class=\"section-copy\">This report is an AI-assisted first-pass model evidence review. Final validation remains subject to engineering review, drawings, specifications, and owner acceptance.</p>");
            html.AppendLine("<div class=\"note-card\">");
            html.AppendLine("No final outcome promise is made here. The report is intended to support deterministic review and human coordination.");
            html.AppendLine("</div>");

            if (state.Report.Warnings != null && state.Report.Warnings.Count > 0)
            {
                html.AppendLine("<details class=\"traceability no-print\" style=\"margin-top:12px;\">");
                html.AppendLine("<summary>Show warnings</summary>");
                foreach (string warning in state.Report.Warnings)
                {
                    html.AppendLine("<div class=\"callout\" style=\"margin-top:10px;\">" + Encode(warning) + "</div>");
                }
                html.AppendLine("</details>");
            }

            html.AppendLine("</section>");
            return html.ToString();
        }

        private static string RenderMachineReadableContext(ReportViewState state)
        {
            string json = BuildMachineReadableContextJson(state);
            return "<script type=\"application/json\" id=\"ema-ai-report-context\">" + json + "</script>";
        }

        private static string BuildMachineReadableContextJson(ReportViewState state)
        {
            object payload = new
            {
                schema_version = "1.0",
                report_metadata = new
                {
                    project_name = SafeText(state.Report.ProjectName),
                    model_name = SafeText(state.Report.ModelName),
                    requirements_file = SafeText(state.Report.RequirementsFileName),
                    generated_at = state.Report.GeneratedAt == default(DateTime)
                        ? string.Empty
                        : state.Report.GeneratedAt.ToString("O", CultureInfo.InvariantCulture),
                    scope = GetScopeLabel(state.Report.Scope),
                    model_elements_reviewed = state.Report.ModelElementCount,
                    total_requirements = state.AllResults.Count
                },
                summary_counts = new
                {
                    met = state.Report.Summary?.MetCount ?? 0,
                    not_met = state.Report.Summary?.NotMetCount ?? 0,
                    needs_human_review = state.Report.Summary?.NeedsHumanReviewCount ?? 0,
                    insufficient_model_data = state.Report.Summary?.InsufficientModelDataCount ?? 0,
                    not_applicable = state.Report.Summary?.NotApplicableCount ?? 0
                },
                filter_context = new
                {
                    active_discipline = state.ActiveDiscipline,
                    active_status = state.ActiveStatus,
                    active_urgency = state.ActiveUrgency,
                    visible_requirements = state.VisibleResults.Count,
                    visible_key_issues = state.VisibleIssues.Count,
                    overall_score = Math.Round(state.VisibleOverallScore, 1),
                    readiness_score = Math.Round(state.VisibleReadinessScore, 1),
                    discipline_score = Math.Round(state.VisibleDisciplineScore, 1),
                    excluded_requirements = state.ExcludedCount,
                    suggested_questions = state.SuggestedQuestions
                },
                discipline_summaries = (state.DisciplineSummaries ?? new List<DisciplineSummary>())
                    .Select(summary => new
                    {
                        discipline = SafeText(summary.Discipline),
                        total = summary.Total,
                        met = summary.Met,
                        not_met = summary.NotMet,
                        needs_human_review = summary.NeedsHumanReview,
                        insufficient_model_data = summary.InsufficientModelData,
                        not_applicable = summary.NotApplicable,
                        discipline_score = Math.Round(summary.DisciplineScore, 1),
                        key_issue_count = summary.KeyIssueCount,
                        top_next_actions = summary.TopNextActions ?? new List<string>(),
                        section_anchor = BuildDisciplineAnchor(summary.Discipline),
                        display = new
                        {
                            primary_color = DisciplinePrimaryColor(summary.Discipline),
                            background_color = DisciplineBackgroundColor(summary.Discipline),
                            border_color = DisciplineBorderColor(summary.Discipline),
                            text_color = DisciplineTextColor(summary.Discipline)
                        }
                    })
                    .ToList(),
                coherence_audit = BuildCoherenceAuditJson(state.Coherence),
                key_issues = (state.Report.KeyIssues ?? new List<KeyIssue>())
                    .Select(issue => new
                    {
                        rank = issue.Rank,
                        requirement_id = SafeText(issue.RequirementId),
                        source_row = issue.SourceRow,
                        discipline = SafeText(issue.Discipline),
                        urgency = SafeText(issue.SeverityLabel),
                        status = StatusLabel(issue.Status),
                        issue_title = SafeText(issue.IssueTitle),
                        evidence_summary = SafeText(issue.EvidenceSummary),
                        reasoning = SafeText(issue.Reasoning),
                        next_best_action = SafeText(issue.NextBestAction),
                        confidence = Math.Round(issue.Confidence, 2),
                        key_issue_score = Math.Round(issue.KeyIssueScore, 2),
                        urgency_reason = SafeText(issue.UrgencyReason),
                        score_factors = new
                        {
                            status_severity_score = Math.Round(issue.StatusSeverityScore, 2),
                            deliverable_impact_score = Math.Round(issue.DeliverableImpactScore, 2),
                            actionability_score = Math.Round(issue.ActionabilityScore, 2),
                            evidence_gap_score = Math.Round(issue.EvidenceGapScore, 2),
                            requirement_type_risk_score = Math.Round(issue.RequirementTypeRiskScore, 2),
                            impact_scale_score = Math.Round(issue.ImpactScaleScore, 2)
                        },
                        status_severity_score = Math.Round(issue.StatusSeverityScore, 2),
                        deliverable_impact_score = Math.Round(issue.DeliverableImpactScore, 2),
                        actionability_score = Math.Round(issue.ActionabilityScore, 2),
                        evidence_gap_score = Math.Round(issue.EvidenceGapScore, 2),
                        requirement_type_risk_score = Math.Round(issue.RequirementTypeRiskScore, 2),
                        impact_scale_score = Math.Round(issue.ImpactScaleScore, 2),
                        candidate_scope_valid = issue.CandidateScopeValid,
                        full_model_fallback_used = issue.FullModelFallbackUsed,
                        requirement_type = SafeText(issue.RequirementType),
                        key_issue_score_reason = SafeText(issue.KeyIssueScoreReason),
                        section_anchor = BuildRequirementAnchor(new RequirementCheckResult
                        {
                            RequirementId = issue.RequirementId,
                            SourceRow = issue.SourceRow
                        }),
                        discipline_color_key = DisciplineColorKey(issue.Discipline),
                        discipline_anchor = BuildDisciplineAnchor(issue.Discipline)
                    })
                    .ToList(),
                requirement_results = (state.AllResults ?? new List<RequirementCheckResult>())
                    .Select(result => new
                    {
                        requirement_id = SafeText(result.RequirementId ?? result.Requirement?.RequirementId),
                        source_file = SafeText(result.SourceFile ?? result.Requirement?.SourceFile),
                        source_worksheet = SafeText(result.SourceWorksheet ?? result.Requirement?.SourceSheet),
                        source_row = result.SourceRow > 0 ? result.SourceRow : result.Requirement?.RowNumber ?? 0,
                        discipline = SafeText(GetResultDiscipline(result)),
                        responsible_role = SafeText(result.ResponsibleRole ?? GetResultDiscipline(result)),
                        requirement_type = SafeText(result.RequirementType),
                        requirement_type_reason = SafeText(result.RequirementTypeReason),
                        validation_type = result.ValidationType.ToString(),
                        status = StatusLabel(result.Status),
                        confidence = Math.Round(result.Confidence, 2),
                        requirement_text = SafeText(result.RequirementText ?? result.Requirement?.RequirementText),
                        evidence_summary = SafeText(result.EvidenceSummary),
                        matched_categories = result.MatchedCategories ?? new List<string>(),
                        matched_families = result.MatchedFamilies ?? new List<string>(),
                        matched_types = result.MatchedTypes ?? new List<string>(),
                        matched_keywords = BuildMatchedKeywords(result),
                        matched_parameters = result.MatchedParameters ?? new List<string>(),
                        missing_parameters = result.MissingEvidence ?? new List<string>(),
                        matched_element_count = result.MatchedModelElementCount,
                        matched_element_ids = (result.MatchedElementIds ?? new List<long>()).Take(EvidenceEmbedLimits.MaxElementIdsInJson).ToList(),
                        matched_element_id_total = result.MatchedElementIds?.Count ?? 0,
                        matched_element_ids_truncated = (result.MatchedElementIds?.Count ?? 0) > EvidenceEmbedLimits.MaxElementIdsInJson,
                        matched_unique_ids = (result.MatchedUniqueIds ?? new List<string>()).Take(EvidenceEmbedLimits.MaxUniqueIdsInJson).ToList(),
                        matched_unique_id_total = result.MatchedUniqueIds?.Count ?? 0,
                        matched_unique_ids_truncated = (result.MatchedUniqueIds?.Count ?? 0) > EvidenceEmbedLimits.MaxUniqueIdsInJson,
                        element_id_copy_text = SafeText(BuildJsonElementIdCopyText(result)),
                        broad_category_match = IsBroadCategoryMatch(result),
                        filter_trace = new
                        {
                            requirement_type = SafeText(result.FilterTrace?.RequirementType),
                            requirement_type_reason = SafeText(result.FilterTrace?.RequirementTypeReason),
                            discipline_filter = SafeText(result.FilterTrace?.DisciplineFilter),
                            scope_filter = SafeText(result.FilterTrace?.ScopeFilter),
                            status_filter = SafeText(result.FilterTrace?.StatusFilter),
                            requirement_intent = SafeText(result.FilterTrace?.RequirementIntent),
                            validation_type = SafeText(result.FilterTrace?.ValidationType),
                            validation_type_reason = SafeText(result.FilterTrace?.ValidationTypeReason),
                            rule_applied = SafeText(result.FilterTrace?.RuleApplied),
                            rule_family = SafeText(result.FilterTrace?.RuleFamily),
                            trigger_keywords = result.FilterTrace?.TriggerKeywords ?? new List<string>(),
                            expected_evidence_sources = result.FilterTrace?.ExpectedEvidenceSources ?? new List<string>(),
                            expected_categories = result.FilterTrace?.ExpectedCategories ?? new List<string>(),
                            expected_family_type_hints = result.FilterTrace?.ExpectedFamilyTypeHints ?? new List<string>(),
                            expected_parameters = result.FilterTrace?.ExpectedParameters ?? new List<string>(),
                            allowed_categories = result.FilterTrace?.AllowedCategories ?? new List<string>(),
                            excluded_categories = result.FilterTrace?.ExcludedCategories ?? new List<string>(),
                            direct_closing_evidence = result.FilterTrace?.DirectClosingEvidence ?? new List<string>(),
                            supporting_context = result.FilterTrace?.SupportingContext ?? new List<string>(),
                            missing_direct_evidence = result.FilterTrace?.MissingDirectEvidence ?? new List<string>(),
                            candidate_scope_reason = SafeText(result.FilterTrace?.CandidateScopeReason),
                            fallback_used = result.FilterTrace?.FallbackUsed ?? false,
                            fallback_allowed = result.FilterTrace?.FallbackAllowed ?? false,
                            candidate_scope_valid = result.FilterTrace?.CandidateScopeValid ?? true,
                            full_model_fallback_used = result.FilterTrace?.FullModelFallbackUsed ?? false,
                            model_evidence_sufficiency = SafeText(result.FilterTrace?.ModelEvidenceSufficiency),
                            why_not_model_closeable = SafeText(result.FilterTrace?.WhyNotModelCloseable),
                            candidate_stages = (result.FilterTrace?.CandidateStages ?? new List<FilterStageTrace>())
                                .Select(stage => new
                                {
                                    stage_name = SafeText(stage.StageName),
                                    description = SafeText(stage.Description),
                                    input_count = stage.InputCount,
                                    output_count = stage.OutputCount,
                                    criteria = SafeText(stage.Criteria),
                                    example_matched_values = stage.ExampleMatchedValues ?? new List<string>()
                                }).ToList()
                        },
                        matched_elements_total = result.MatchedElements?.Count ?? 0,
                        matched_elements_truncated = (result.MatchedElements?.Count ?? 0) > EvidenceEmbedLimits.MaxMatchedElementsInJson,
                        matched_elements = (result.MatchedElements ?? new List<MatchedElementEvidence>())
                            .Take(EvidenceEmbedLimits.MaxMatchedElementsInJson)
                            .Select(element => new
                            {
                                element_id = SafeText(element.ElementId),
                                unique_id = SafeText(element.UniqueId),
                                category = SafeText(element.Category),
                                family = SafeText(element.Family),
                                type = SafeText(element.Type),
                                level = SafeText(element.Level),
                                matched_parameters = element.MatchedParameters ?? new List<string>(),
                                missing_parameters = element.MissingParameters ?? new List<string>(),
                                parameter_value_examples = element.ParameterValueExamples ?? new List<string>(),
                                parameter_checks = (element.ParameterChecks ?? new List<ParameterCheckResult>())
                                    .Select(check => new
                                    {
                                        parameter_name = SafeText(check.ParameterName),
                                        expected_meaning = SafeText(check.ExpectedMeaning),
                                        expected_value_pattern = SafeText(check.ExpectedValuePattern),
                                        actual_value = SafeText(check.ActualValue),
                                        source = SafeText(check.Source),
                                        is_present = check.IsPresent,
                                        is_empty = check.IsEmpty,
                                        is_match = check.IsMatch,
                                        is_required = check.IsRequired,
                                        failure_reason = SafeText(check.FailureReason)
                                    }).ToList(),
                                parameter_values = element.ParameterValues ?? new Dictionary<string, string>(),
                                evidence_reason = SafeText(element.EvidenceReason)
                            })
                            .ToList(),
                        actual_matched_categories = result.ActualMatchedCategories ?? new List<string>(),
                        actual_matched_parameters = result.ActualMatchedParameters ?? new List<string>(),
                        actual_parameter_value_examples = result.ActualParameterValueExamples ?? new List<string>(),
                        missing_expected_parameters = result.MissingExpectedParameters ?? new List<string>(),
                        matched_family_type_summary = result.MatchedFamilyTypeSummary ?? new List<string>(),
                        evidence_strength = Math.Round(CalculateEvidenceStrength(result), 2),
                        evidence_alignment = result.EvidenceAlignmentLabel,
                        evidence_alignment_reason = SafeText(result.EvidenceAlignmentReason),
                        rule_applied = SafeText(result.RuleApplied),
                        rule_family = SafeText(result.RuleFamily),
                        rule_trigger_keywords = result.RuleTriggerKeywords ?? new List<string>(),
                        rule_expected_evidence = SafeText(result.RuleExpectedEvidence),
                        validation_type_reason = SafeText(result.ValidationTypeReason),
                        candidate_scope = SafeText(result.CandidateScopeReason),
                        allowed_categories = result.AllowedCategories ?? new List<string>(),
                        excluded_categories = result.ExcludedCategories ?? new List<string>(),
                        direct_closing_evidence = result.DirectClosingEvidence ?? new List<string>(),
                        supporting_context = result.SupportingContext ?? new List<string>(),
                        missing_direct_evidence = result.MissingDirectEvidence ?? new List<string>(),
                        candidate_scope_reason = SafeText(result.CandidateScopeReason),
                        fallback_used = result.FallbackUsed,
                        fallback_allowed = result.FallbackAllowed,
                        candidate_scope_valid = result.CandidateScopeValid,
                        full_model_fallback_used = result.FullModelFallbackUsed,
                        model_evidence_sufficiency = SafeText(result.ModelEvidenceSufficiency),
                        why_not_model_closeable = SafeText(result.WhyNotModelCloseable),
                        parameter_value_examples = result.ParameterValueExamples ?? new List<string>(),
                        missing_evidence_details = (result.MissingEvidenceDetails ?? new List<MissingEvidenceDetail>())
                            .Select(d => new
                            {
                                parameter_name = SafeText(d.ParameterName),
                                reason = d.Reason.ToString(),
                                reason_label = d.ReasonLabel
                            }).ToList(),
                        reasoning = SafeText(result.Reasoning),
                        next_best_action = SafeText(result.NextBestAction),
                        status_reason = SafeText(result.StatusReason),
                        confidence_reason = SafeText(result.ConfidenceReason),
                        model_evidence_limitations = SafeText(result.ModelEvidenceLimitations),
                        human_review_needed = result.HumanReviewNeeded,
                        issue_title = SafeText(result.IssueTitle),
                        urgency = NormalizeUrgency(string.IsNullOrWhiteSpace(result.Urgency) ? DefaultUrgency(result.Status) : result.Urgency),
                        key_issue_score = Math.Round(result.KeyIssueScore, 2),
                        urgency_reason = SafeText(result.UrgencyReason),
                        key_issue_score_reason = SafeText(result.KeyIssueScoreReason),
                        score_factors = new
                        {
                            status_severity_score = Math.Round(result.StatusSeverityScore, 2),
                            deliverable_impact_score = Math.Round(result.DeliverableImpactScore, 2),
                            actionability_score = Math.Round(result.ActionabilityScore, 2),
                            evidence_gap_score = Math.Round(result.EvidenceGapScore, 2),
                            requirement_type_risk_score = Math.Round(result.RequirementTypeRiskScore, 2),
                            impact_scale_score = Math.Round(result.ImpactScaleScore, 2)
                        },
                        is_key_issue = result.IsKeyIssue,
                        section_anchor = BuildRequirementAnchor(result),
                        discipline_color_key = DisciplineColorKey(result.Discipline ?? result.Requirement?.Discipline),
                        discipline_anchor = BuildDisciplineAnchor(GetResultDiscipline(result)),
                        ai_lookup_hints = new
                        {
                            search_terms = BuildSearchTerms(result),
                            evidence_location = BuildEvidenceLocation(result),
                            missing_evidence_terms = result.MissingEvidence ?? new List<string>(),
                            likely_owner = SafeText(result.ResponsibleRole ?? GetResultDiscipline(result)),
                            human_review_needed = result.Status == RequirementCheckStatus.NeedsHumanReview,
                            suggested_question = BuildSuggestedQuestionForResult(result),
                            revit_element_ids = (result.MatchedElementIds ?? new List<long>()).Take(EvidenceEmbedLimits.MaxElementIdsInJson).ToList(),
                            element_id_copy_text = SafeText(BuildJsonElementIdCopyText(result))
                        }
                    })
                    .ToList()
            };

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = false
            };

            return JsonSerializer.Serialize(payload, options);
        }

        private static object BuildCoherenceAuditJson(RequirementCoherenceReport coherence)
        {
            coherence = coherence ?? new RequirementCoherenceReport();
            return new
            {
                grade = SafeText(coherence.CoherenceGrade),
                requirements_analyzed = coherence.RequirementsAnalyzed,
                requirement_types_analyzed = coherence.RequirementTypesAnalyzed,
                exact_duplicates = coherence.ExactDuplicateCount,
                semantic_duplicates = coherence.SemanticDuplicateCount,
                numeric_conflicts = coherence.NumericConflictCount,
                quantity_conflicts = coherence.QuantityConflictCount,
                manufacturer_conflicts = coherence.ManufacturerConflictCount,
                high_severity_findings = coherence.HighSeverityCount,
                by_requirement_type = (coherence.TypeSummaries ?? new List<RequirementTypeCoherenceSummary>())
                    .Select(summary => new
                    {
                        requirement_type = SafeText(summary.RequirementType),
                        requirement_count = summary.RequirementCount,
                        finding_count = summary.FindingCount,
                        duplicate_count = summary.DuplicateCount,
                        conflict_count = summary.ConflictCount,
                        highest_severity = SeverityName(summary.HighestSeverity),
                        is_coherent = summary.IsCoherent
                    })
                    .ToList(),
                findings = (coherence.Findings ?? new List<CoherenceFinding>())
                    .Take(MaxCoherenceFindingsInHtml)
                    .Select(finding => new
                    {
                        id = SafeText(finding.Id),
                        finding_type = finding.FindingType.ToString(),
                        severity = SeverityName(finding.Severity),
                        requirement_type = SafeText(finding.RequirementType),
                        rationale = SafeText(finding.Rationale),
                        status = SafeText(finding.Status),
                        primary = CoherenceRefJson(finding.Primary),
                        related = CoherenceRefJson(finding.Related),
                        normalized_values = finding.NormalizedValues ?? new Dictionary<string, string>()
                    })
                    .ToList()
            };
        }

        private static object CoherenceRefJson(RequirementRef reference)
        {
            if (reference == null)
            {
                return null;
            }
            return new
            {
                requirement_id = SafeText(reference.RequirementId),
                source_worksheet = SafeText(reference.SourceWorksheet),
                source_row = reference.SourceRow,
                discipline = SafeText(reference.Discipline),
                requirement_type = SafeText(reference.RequirementType),
                short_text = SafeText(reference.ShortText)
            };
        }

        private static string BuildScript(ReportViewState state)
        {
            StringBuilder js = new StringBuilder();
            js.AppendLine("(function(){");
            js.AppendLine("  const initial = { discipline: " + JsString(state.ActiveDiscipline) + ", status: " + JsString(state.ActiveStatus) + ", urgency: " + JsString(state.ActiveUrgency) + ", title: " + JsString(state.Title) + ", exportStem: " + JsString(state.ExportStem) + " };");
            js.AppendLine("  const questionSets = " + BuildQuestionSetLiteral() + ";");
            js.AppendLine("  const disciplineButtons = Array.from(document.querySelectorAll('[data-filter-kind=\"discipline\"]'));");
            js.AppendLine("  const statusButtons = Array.from(document.querySelectorAll('[data-filter-kind=\"status\"]'));");
            js.AppendLine("  const urgencyButtons = Array.from(document.querySelectorAll('[data-filter-kind=\"urgency\"]'));");
            js.AppendLine("  const resultCards = Array.from(document.querySelectorAll('[data-report-card=\"result\"]'));");
            js.AppendLine("  const issueCards = Array.from(document.querySelectorAll('[data-report-card=\"issue\"]'));");
            js.AppendLine("  const disciplineRows = Array.from(document.querySelectorAll('[data-discipline-row]'));");
            js.AppendLine("  const disciplineSections = Array.from(document.querySelectorAll('[data-discipline-section]'));");
            js.AppendLine("  const titleNode = document.getElementById('report-title');");
            js.AppendLine("  const subtitleNode = document.getElementById('report-subtitle');");
            js.AppendLine("  const summaryGrid = document.getElementById('summary-grid');");
            js.AppendLine("  const questionsNode = document.getElementById('ask-questions');");
            js.AppendLine("  const disciplineGrid = document.getElementById('discipline-grid');");
            js.AppendLine("  const filterBanner = document.getElementById('filter-context-banner');");
            js.AppendLine("  const totalResultCount = Array.from(document.querySelectorAll('[data-report-card=\"result\"]')).length;");
            js.AppendLine("  let state = { discipline: initial.discipline, status: initial.status, urgency: initial.urgency };");
            js.AppendLine("  const documentTitle = document.title;");
            js.AppendLine("  function matches(value, filter){ return !filter || filter === 'All' || filter === 'All Disciplines' || value === filter; }");
            js.AppendLine("  function computeStats(cards){");
            js.AppendLine("    const stats = { total: cards.length, met: 0, notMet: 0, review: 0, bad: 0, na: 0, overall: 0, readiness: 0, disciplines: new Set(), actions: [], scoreCount: 0, scoreTotal: 0 };");
            js.AppendLine("    const values = { 'Met': 1.0, 'Needs Human Review': 0.55, 'Insufficient Model Data': 0.40, 'Not Met': 0.0, 'Not Applicable': 0.0 };");
            js.AppendLine("    cards.forEach(card => {");
            js.AppendLine("      const status = card.dataset.status || 'Not Applicable';");
            js.AppendLine("      const urgency = card.dataset.urgency || 'Low';");
            js.AppendLine("      const conf = parseFloat(card.dataset.confidence || '0');");
            js.AppendLine("      const weight = values[status] === undefined ? 0 : values[status];");
            js.AppendLine("      stats.scoreTotal += weight * conf; stats.scoreCount += 1;");
            js.AppendLine("      if (status === 'Met') stats.met += 1; else if (status === 'Not Met') stats.notMet += 1; else if (status === 'Needs Human Review') stats.review += 1; else if (status === 'Insufficient Model Data') stats.bad += 1; else stats.na += 1;");
            js.AppendLine("      const discipline = card.dataset.discipline || 'Unknown / Needs Classification'; stats.disciplines.add(discipline);");
            js.AppendLine("      const action = card.dataset.nextaction || ''; if (action && stats.actions.indexOf(action) === -1 && status !== 'Met' && status !== 'Not Applicable') stats.actions.push(action);");
            js.AppendLine("    });");
            js.AppendLine("    stats.overall = stats.scoreCount ? Math.max(0, Math.min(100, (stats.scoreTotal / stats.scoreCount) * 100)) : 0;");
            js.AppendLine("    // stats.readiness removed from visible report");
            js.AppendLine("    return stats;");
            js.AppendLine("  }");
            js.AppendLine("  function setMetric(label, value, detail, cssClass){");
            js.AppendLine("    const node = summaryGrid ? Array.from(summaryGrid.querySelectorAll('.metric')).find(el => el.dataset.metric === label) : null;");
            js.AppendLine("    if (!node) return;");
            js.AppendLine("    const valueNode = node.querySelector('.value'); if (valueNode) valueNode.textContent = value;");
            js.AppendLine("    const detailNode = node.querySelector('.detail'); if (detailNode && detail !== undefined) detailNode.textContent = detail;");
            js.AppendLine("    node.className = 'metric ' + (cssClass || node.dataset.css || 'na');");
            js.AppendLine("  }");
            js.AppendLine("  function setSummaryMetrics(cards){");
            js.AppendLine("    const stats = computeStats(cards);");
            js.AppendLine("    const total = Array.from(document.querySelectorAll('[data-report-card=\"result\"]')).length;");
            js.AppendLine("    const visibleCount = cards.length;");
            js.AppendLine("    const excluded = Math.max(0, total - visibleCount);");
            js.AppendLine("    // Count only ranked key-issue cards; compact urgency rows duplicate the same issues per requirement.");
            js.AppendLine("    const topIssues = issueCards.filter(card => card.dataset.issueRankCard === 'true' && matches(card.dataset.discipline || '', state.discipline) && matches(card.dataset.status || '', state.status) && matches(card.dataset.urgency || '', state.urgency));");
            js.AppendLine("    const issueCount = topIssues.length;");
            js.AppendLine("    const disciplineCount = stats.disciplines.size;");
            js.AppendLine("    document.title = 'EMA AI | ' + (state.discipline === 'All Disciplines' ? 'Master Owner Requirements Review' : state.discipline + ' Owner Requirements Review');");
            js.AppendLine("    if (titleNode) titleNode.textContent = state.discipline === 'All Disciplines' ? 'Master Owner Requirements Review' : state.discipline + ' Owner Requirements Review';");
            js.AppendLine("    if (subtitleNode) subtitleNode.textContent = state.discipline === 'All Disciplines' ? 'Executive summary first. Requirement-by-requirement detail remains below for every discipline.' : 'Focused discipline view. Other requirements remain in the report and are hidden by the active filter.';");
            js.AppendLine("    const metrics = Array.from(summaryGrid.querySelectorAll('.metric'));");
            js.AppendLine("    metrics.forEach(m => { m.style.display = ''; });");
            js.AppendLine("    const map = {};");
            js.AppendLine("    metrics.forEach(m => { map[m.dataset.metric] = m; });");
            js.AppendLine("    function put(label, value, detail, cls){ if (map[label]) { map[label].querySelector('.value').textContent = value; const d = map[label].querySelector('.detail'); if (d && detail !== undefined) d.textContent = detail; map[label].className = 'metric ' + cls; } }");
            js.AppendLine("    if (state.discipline === 'All Disciplines') {");
            js.AppendLine("      put('Evidence Review Score', stats.overall.toFixed(1) + '%', 'Summary of how much current evidence supports the reviewed Owner Requirements. This score is not a compliance certification and does not mean the project is fully ready. It is a first-pass evidence review based on the current Revit model export and available requirement data.', 'status-not-applicable');");
            js.AppendLine("      // Readiness Score removed from visible report");
            js.AppendLine("      put('Total Requirements', total.toString(), 'Total rows in the source workbook.', 'status-not-applicable');");
            js.AppendLine("      put('Applicable Requirements', visibleCount.toString(), 'Requirements considered in the active filtered view.', 'status-not-applicable');");
            js.AppendLine("      put('Met', stats.met.toString(), 'Evidence appears sufficient based on the current model data.', 'status-met');");
            js.AppendLine("      put('Not Met', stats.notMet.toString(), 'Evidence exists, but required values or conditions are missing.', 'status-not-met');");
            js.AppendLine("      put('Needs Human Review', stats.review.toString(), 'Requires drawings, specifications, or judgment.', 'status-needs-review');");
            js.AppendLine("      put('Insufficient Model Data', stats.bad.toString(), 'The model snapshot does not contain enough evidence.', 'status-insufficient-data');");
            js.AppendLine("      put('Not Applicable', stats.na.toString(), 'Outside the selected discipline or scope.', 'status-not-applicable');");
            js.AppendLine("      put('Key Issues', issueCount.toString(), 'Prioritized findings most likely to affect the next deliverable.', 'status-not-met');");
            js.AppendLine("      put('Disciplines Impacted', disciplineCount.toString(), 'Disciplines with requirements currently in scope.', 'status-not-applicable');");
            js.AppendLine("    } else {");
            js.AppendLine("      put('Discipline Score', stats.overall.toFixed(1) + '%', 'Deterministic summary for the active discipline view.', 'status-not-applicable');");
            js.AppendLine("      put('Discipline Requirements', visibleCount.toString(), 'Requirements shown in the current view.', 'status-not-applicable');");
            js.AppendLine("      // Master-grid tiles must also refresh when the master report is filtered to a discipline; put() is a no-op for tiles that do not exist in this report variant.");
            js.AppendLine("      put('Evidence Review Score', stats.overall.toFixed(1) + '%', 'Recomputed for the active filtered view. First-pass evidence review based on the current model data; not a compliance certification.', 'status-not-applicable');");
            js.AppendLine("      put('Disciplines Impacted', disciplineCount.toString(), 'Disciplines with requirements in the active filtered view.', 'status-not-applicable');");
            js.AppendLine("      put('Applicable Requirements', visibleCount.toString(), 'Requirements considered after applying the active filter.', 'status-not-applicable');");
            js.AppendLine("      put('Met', stats.met.toString(), 'Evidence appears sufficient based on the current model data.', 'status-met');");
            js.AppendLine("      put('Not Met', stats.notMet.toString(), 'Evidence exists, but required values or conditions are missing.', 'status-not-met');");
            js.AppendLine("      put('Needs Human Review', stats.review.toString(), 'Requires drawings, specifications, or judgment.', 'status-needs-review');");
            js.AppendLine("      put('Insufficient Model Data', stats.bad.toString(), 'The model snapshot does not contain enough evidence.', 'status-insufficient-data');");
            js.AppendLine("      put('Not Applicable', stats.na.toString(), 'Outside the selected discipline or scope.', 'status-not-applicable');");
            js.AppendLine("      put('Key Issues', issueCount.toString(), 'Prioritized findings most likely to affect the active discipline.', 'status-not-met');");
            js.AppendLine("      put('Top Next Actions', Math.min(5, stats.actions.length).toString(), 'Most actionable next steps for this filtered view.', 'status-not-applicable');");
            js.AppendLine("    }");
            js.AppendLine("    const focused = document.getElementById('discipline-focus'); if (focused) focused.textContent = state.discipline;");
            js.AppendLine("    const shown = document.getElementById('requirements-shown'); if (shown) shown.textContent = visibleCount.toString();");
            js.AppendLine("    const excludedNode = document.getElementById('requirements-excluded'); if (excludedNode) excludedNode.textContent = excluded.toString();");
            js.AppendLine("    const keyIssueCountNode = document.getElementById('key-issue-count'); if (keyIssueCountNode) keyIssueCountNode.textContent = issueCount.toString();");
            js.AppendLine("    updateFilterBanner(visibleCount, stats, issueCount);");
            js.AppendLine("    updateNextActions(stats, issueCount);");
            js.AppendLine("    updateQuestions();");
            js.AppendLine("    updateAllocation(cards, stats);");
            js.AppendLine("    updateGroupTitles();");
            js.AppendLine("    updateSections();");
            js.AppendLine("  }");
            js.AppendLine("  function updateFilterBanner(visibleCount, stats, issueCount){");
            js.AppendLine("    if (!filterBanner) return;");
            js.AppendLine("    const isMaster = state.discipline === 'All Disciplines' && state.status === 'All' && state.urgency === 'All';");
            js.AppendLine("    if (isMaster) {");
            js.AppendLine("      filterBanner.innerHTML = '<div class=\"banner-title\">Master View</div><div class=\"banner-detail\">Showing all ' + totalResultCount + ' requirements across all disciplines and statuses.</div>';");
            js.AppendLine("    } else {");
            js.AppendLine("      let parts = [];");
            js.AppendLine("      if (state.discipline !== 'All Disciplines') parts.push('Discipline: ' + state.discipline);");
            js.AppendLine("      if (state.status !== 'All') parts.push('Status: ' + state.status);");
            js.AppendLine("      if (state.urgency !== 'All') parts.push('Urgency: ' + state.urgency);");
            js.AppendLine("      let caption = '';");
            js.AppendLine("      if (state.status !== 'All') caption = 'Scores and counts reflect only the filtered ' + state.status + ' items, not the full project.';");
            js.AppendLine("      else caption = 'Counts and scores below reflect this filtered view.';");
            js.AppendLine("      filterBanner.innerHTML = '<div class=\"banner-title\">Active Filtered View</div><div class=\"banner-detail\">' + parts.join(' | ') + '</div><div class=\"banner-detail\">Showing ' + visibleCount + ' of ' + totalResultCount + ' requirements.</div><div class=\"banner-caption\">' + caption + '</div>';");
            js.AppendLine("    }");
            js.AppendLine("  }");
            js.AppendLine("  function updateNextActions(stats, issueCount){");
            js.AppendLine("    const nextActionsNode = document.getElementById('top-next-actions');");
            js.AppendLine("    const nextActionsCallout = nextActionsNode ? nextActionsNode.closest('.next-actions-callout') : null;");
            js.AppendLine("    if (!nextActionsNode) return;");
            js.AppendLine("    nextActionsNode.innerHTML = '';");
            js.AppendLine("    if (state.status === 'Met') {");
            js.AppendLine("      const li = document.createElement('li'); li.textContent = 'No immediate corrective actions in this filtered Met view.'; nextActionsNode.appendChild(li);");
            js.AppendLine("    } else if (state.status === 'Not Applicable') {");
            js.AppendLine("      const li = document.createElement('li'); li.textContent = 'These requirements are outside the selected scope. No action required.'; nextActionsNode.appendChild(li);");
            js.AppendLine("    } else if (stats.total === 0) {");
            js.AppendLine("      const li = document.createElement('li'); li.textContent = 'No requirements match the current filter.'; nextActionsNode.appendChild(li);");
            js.AppendLine("    } else if (state.status === 'Needs Human Review') {");
            js.AppendLine("      const actions = stats.actions.length > 0 ? stats.actions.slice(0,5) : ['Review these items against drawings, specifications, and owner standards.', 'Confirm owner interpretation for items that cannot be verified in the model.'];");
            js.AppendLine("      actions.forEach(a => { const li = document.createElement('li'); li.textContent = a; nextActionsNode.appendChild(li); });");
            js.AppendLine("      if (issueCount === 0 && stats.review > 0) { const note = document.createElement('li'); note.style.fontStyle = 'italic'; note.style.color = 'var(--text-muted)'; note.textContent = 'Key Issues are ranked action items. Human review items may require drawings or specifications even if they are not ranked as key issues.'; nextActionsNode.appendChild(note); }");
            js.AppendLine("    } else if (state.status === 'Insufficient Model Data') {");
            js.AppendLine("      const actions = stats.actions.length > 0 ? stats.actions.slice(0,5) : ['Review these requirements against the relevant specification because the model does not contain enough evidence.'];");
            js.AppendLine("      actions.forEach(a => { const li = document.createElement('li'); li.textContent = a; nextActionsNode.appendChild(li); });");
            js.AppendLine("    } else {");
            js.AppendLine("      stats.actions.slice(0,5).forEach(a => { const li = document.createElement('li'); li.textContent = a; nextActionsNode.appendChild(li); });");
            js.AppendLine("    }");
            js.AppendLine("    if (nextActionsCallout) nextActionsCallout.style.display = nextActionsNode.children.length > 0 ? '' : 'none';");
            js.AppendLine("  }");
            js.AppendLine("  function updateAllocation(cards, stats){");
            js.AppendLine("    if (!disciplineRows.length) return;");
            js.AppendLine("    disciplineRows.forEach(row => {");
            js.AppendLine("      const match = row.dataset.disciplineRow || ''; const show = state.discipline === 'All Disciplines' || match === state.discipline;");
            js.AppendLine("      row.style.display = show ? '' : 'none';");
            js.AppendLine("    });");
            js.AppendLine("  }");
            js.AppendLine("  function updateQuestions(){");
            js.AppendLine("    if (!questionsNode) return;");
            js.AppendLine("    const questions = questionSets[state.discipline] || questionSets['All Disciplines'];");
            js.AppendLine("    questionsNode.innerHTML = '';");
            js.AppendLine("    questions.forEach(q => { const div = document.createElement('div'); div.className = 'question-card'; div.innerHTML = '<div class=\"chip\"><span class=\"label\">Suggested question</span><div class=\"value\"></div></div>'; div.querySelector('.value').textContent = q; questionsNode.appendChild(div); });");
            js.AppendLine("  }");
            js.AppendLine("  function updateGroupTitles(){");
            js.AppendLine("    const groups = Array.from(document.querySelectorAll('[data-urgency-group]'));");
            js.AppendLine("    groups.forEach(group => {");
            js.AppendLine("      const items = Array.from(group.querySelectorAll('[data-report-card=\"issue\"]')).filter(card => !card.hidden);");
            js.AppendLine("      const count = items.length; const title = group.querySelector('[data-group-title]'); const meta = group.querySelector('[data-group-meta]');");
            js.AppendLine("      if (title) title.textContent = (group.dataset.urgencyGroup || 'Needs Review') + ' - ' + count + ' issue(s)';");
            js.AppendLine("      if (meta) { const disciplines = Array.from(new Set(items.map(card => card.dataset.discipline || 'Unknown / Needs Classification'))).join(', '); meta.textContent = 'Affected disciplines: ' + (disciplines || 'None'); }");
            js.AppendLine("      group.style.display = count > 0 ? '' : 'none';");
            js.AppendLine("    });");
            js.AppendLine("  }");
            js.AppendLine("  function updateSections(){");
            js.AppendLine("    if (!disciplineSections.length) return;");
            js.AppendLine("    disciplineSections.forEach(section => {");
            js.AppendLine("      const cards = Array.from(section.querySelectorAll('[data-report-card=\"result\"]'));");
            js.AppendLine("      const visibleCount = cards.filter(card => !card.hidden).length;");
            js.AppendLine("      section.style.display = visibleCount > 0 ? '' : 'none';");
            js.AppendLine("    });");
            js.AppendLine("  }");
            js.AppendLine("  function applyFilter(){");
            js.AppendLine("    disciplineButtons.forEach(btn => btn.classList.toggle('active', btn.dataset.filterValue === state.discipline));");
            js.AppendLine("    statusButtons.forEach(btn => btn.classList.toggle('active', btn.dataset.filterValue === state.status));");
            js.AppendLine("    urgencyButtons.forEach(btn => btn.classList.toggle('active', btn.dataset.filterValue === state.urgency));");
            js.AppendLine("    resultCards.forEach(card => {");
            js.AppendLine("      const visible = matches(card.dataset.discipline || '', state.discipline) && matches(card.dataset.status || '', state.status) && matches(card.dataset.urgency || '', state.urgency);");
            js.AppendLine("      card.style.display = visible ? '' : 'none';");
            js.AppendLine("      card.hidden = !visible;");
            js.AppendLine("    });");
            js.AppendLine("    issueCards.forEach(card => {");
            js.AppendLine("      const visible = matches(card.dataset.discipline || '', state.discipline) && matches(card.dataset.status || '', state.status) && matches(card.dataset.urgency || '', state.urgency);");
            js.AppendLine("      card.style.display = visible ? '' : 'none';");
            js.AppendLine("      card.hidden = !visible;");
            js.AppendLine("    });");
            js.AppendLine("    setSummaryMetrics(resultCards.filter(card => !card.hidden));");
            js.AppendLine("  }");
            js.AppendLine("  function copySummary(){");
            js.AppendLine("    const cards = resultCards.filter(card => !card.hidden);");
            js.AppendLine("    const stats = computeStats(cards);");
            js.AppendLine("    const topIssues = issueCards.filter(card => !card.hidden).slice(0,5).map(card => card.dataset.issueTitle || card.querySelector('.issue-title')?.textContent || 'Issue');");
            js.AppendLine("    const lines = [];");
            js.AppendLine("    const isMasterCopy = state.discipline === 'All Disciplines' && state.status === 'All' && state.urgency === 'All';");
            js.AppendLine("    lines.push('EMA AI Owner Requirement Check');");
            js.AppendLine("    lines.push('Project: ' + " + JsString(state.Report.ProjectName) + ");");
            js.AppendLine("    lines.push('Model: ' + " + JsString(state.Report.ModelName) + ");");
            js.AppendLine("    lines.push('View: ' + (isMasterCopy ? 'Master View' : 'Active Filtered View'));");
            js.AppendLine("    lines.push('Active filter: ' + state.discipline + ' | Status: ' + state.status + ' | Urgency: ' + state.urgency);");
            js.AppendLine("    lines.push('Requirements shown: ' + cards.length + ' of ' + totalResultCount);");
            js.AppendLine("    lines.push((isMasterCopy ? 'Evidence review score: ' : 'Filtered evidence review score: ') + stats.overall.toFixed(1) + '%');");
            js.AppendLine("    // Readiness score removed from clipboard summary");
            js.AppendLine("    lines.push('Status counts: Met ' + stats.met + ', Not Met ' + stats.notMet + ', Needs Human Review ' + stats.review + ', Insufficient Model Data ' + stats.bad + ', Not Applicable ' + stats.na);");
            js.AppendLine("    lines.push('Key issues in view: ' + topIssues.length);");
            js.AppendLine("    if (stats.review > 0) lines.push('Review items in view: ' + stats.review);");
            js.AppendLine("    lines.push('Top issues:');");
            js.AppendLine("    if (topIssues.length === 0) lines.push('  (no key issues in this view)');");
            js.AppendLine("    topIssues.forEach((issue, index) => lines.push((index + 1) + '. ' + issue));");
            js.AppendLine("    lines.push('Top next actions:');");
            js.AppendLine("    const copyActions = stats.actions.slice(0,5);");
            js.AppendLine("    if (copyActions.length === 0) lines.push('  (no corrective actions in this view)');");
            js.AppendLine("    copyActions.forEach((action, index) => lines.push((index + 1) + '. ' + action));");
            js.AppendLine("    lines.push('Report path: ' + " + JsString(state.Report.ReportPath) + ");");
            js.AppendLine("    lines.push('This report is an AI-assisted first-pass model evidence review. Final validation remains subject to engineering review, drawings, specifications, and owner acceptance.');");
            js.AppendLine("    return navigator.clipboard.writeText(lines.join('\\n')).catch(() => {});");
            js.AppendLine("  }");
            js.AppendLine("  function copyElementIds(ids){");
            js.AppendLine("    if (!ids) return Promise.resolve();");
            js.AppendLine("    return navigator.clipboard.writeText(ids).catch(() => {});");
            js.AppendLine("  }");
            js.AppendLine("  function exportPdf(){");
            js.AppendLine("    const original = document.title;");
            js.AppendLine("    const stem = 'EMA_AI_Requirement_Check_' + (state.discipline === 'All Disciplines' ? 'Master' : state.discipline.replace(/[^A-Za-z0-9]+/g, '_')) + '_' + " + JsString(state.Report.GeneratedAt == default(DateTime) ? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) : state.Report.GeneratedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)) + ";");
            js.AppendLine("    document.title = stem;");
            js.AppendLine("    window.addEventListener('afterprint', function restore(){ document.title = original; window.removeEventListener('afterprint', restore); }, { once: true });");
            js.AppendLine("    window.print();");
            js.AppendLine("  }");
            js.AppendLine("  function askAi(){ const target = document.getElementById('ask-ema-ai'); if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' }); }");
            js.AppendLine("  document.addEventListener('click', function(event){");
            js.AppendLine("    const filterButton = event.target.closest('[data-filter-kind]');");
            js.AppendLine("    if (filterButton){ state[filterButton.dataset.filterKind] = filterButton.dataset.filterValue; applyFilter(); return; }");
            js.AppendLine("    const copyIdsButton = event.target.closest('[data-copy-element-ids]');");
            js.AppendLine("    if (copyIdsButton){ copyElementIds(copyIdsButton.dataset.copyElementIds || ''); return; }");
            js.AppendLine("    const actionButton = event.target.closest('[data-action]');");
            js.AppendLine("    if (!actionButton) return;");
            js.AppendLine("    if (actionButton.dataset.action === 'copy') { copySummary(); return; }");
            js.AppendLine("    if (actionButton.dataset.action === 'export') { exportPdf(); return; }");
            js.AppendLine("    if (actionButton.dataset.action === 'ask') { askAi(); return; }");
            js.AppendLine("  });");
            js.AppendLine("  applyFilter();");
            js.AppendLine("})();");
            js.AppendLine(BuildConsoleScript(state));
            return js.ToString();
        }

        private static string BuildConsoleScript(ReportViewState state)
        {
            return """

/* =====================================================================
   EMA AI Schedule Console — tab switching, grid, Ask EMA AI
   ===================================================================== */
(function(){
'use strict';

/* ---- Tab switching ---- */
var tabBtns = Array.from(document.querySelectorAll('.tab-btn'));
var tabPanels = Array.from(document.querySelectorAll('.tab-panel'));
var activeTab = 'summary';
var tabsLoaded = {};

function switchTab(name){
  activeTab = name;
  tabBtns.forEach(function(btn){ btn.classList.toggle('active', btn.dataset.tab === name); btn.setAttribute('aria-selected', btn.dataset.tab === name ? 'true' : 'false'); });
  tabPanels.forEach(function(panel){ panel.classList.toggle('active', panel.id === 'tab-' + name); });
  if (!tabsLoaded[name]){ tabsLoaded[name] = true; onTabFirstLoad(name); }
}

document.addEventListener('click', function(e){
  var btn = e.target.closest('.tab-btn');
  if (btn && btn.dataset.tab){ switchTab(btn.dataset.tab); return; }
  var schedAi = e.target.closest('.sched-ai-btn');
  if (schedAi){ var row = parseInt(schedAi.dataset.row || '0'); switchTab('ask'); prefillAskFromRow(row); return; }
  var sugBtn = e.target.closest('.ask-suggested-btn');
  if (sugBtn){ var q = sugBtn.dataset.question || ''; var inp = document.getElementById('ask-input'); if (inp){ inp.value = q; inp.focus(); } return; }
  var expBtn = e.target.closest('.sched-expand-btn');
  if (expBtn){ toggleDetailRow(expBtn); return; }
  var sortTh = e.target.closest('th.sortable');
  if (sortTh && sortTh.closest('#requirements-schedule-table')){ handleSort(sortTh); return; }
  var pageBtn = e.target.closest('.page-btn[data-page]');
  if (pageBtn){ currentPage = parseInt(pageBtn.dataset.page || '1'); renderSchedulePage(); return; }
  var exportBtn = e.target.closest('[data-export]');
  if (exportBtn){ handleCsvExport(exportBtn.dataset.export); return; }
  var askBtn = e.target.closest('#ask-btn');
  if (askBtn){ handleAsk(); return; }
  var copyAns = e.target.closest('#ask-copy-answer');
  if (copyAns){ copyAnswerText(); return; }
  var clearBtn = e.target.closest('#ask-clear');
  if (clearBtn){ clearAskPanel(); return; }
  var copyCx = e.target.closest('#ask-copy-context');
  if (copyCx){ copyCurrentContext(); return; }
  var evidBtn = e.target.closest('.evidence-view-btn');
  if (evidBtn){ switchEvidenceView(evidBtn); return; }
});

/* ---- Report JSON index ---- */
var reportData = null;
var reportIndex = null;

function loadReportData(){
  if (reportData) return reportData;
  try{
    var el = document.getElementById('ema-ai-report-context');
    if (el){ reportData = JSON.parse(el.textContent || '{}'); reportIndex = buildIndex(reportData); }
  }catch(e){ reportData = {}; }
  return reportData;
}

function buildIndex(data){
  var idx = { byRow:{}, byDiscipline:{}, byStatus:{}, byUrgency:{}, byType:{}, all:[], keyIssues:[], summary:{} };
  idx.all = data.requirement_results || [];
  idx.keyIssues = data.key_issues || [];
  idx.summary = data.summary_counts || {};
  idx.all.forEach(function(r){
    if (r.source_row != null) idx.byRow[r.source_row] = r;
    var d = r.discipline || 'Unknown'; if (!idx.byDiscipline[d]) idx.byDiscipline[d] = []; idx.byDiscipline[d].push(r);
    var s = r.status || ''; if (!idx.byStatus[s]) idx.byStatus[s] = []; idx.byStatus[s].push(r);
    var u = r.urgency || ''; if (!idx.byUrgency[u]) idx.byUrgency[u] = []; idx.byUrgency[u].push(r);
    var t = r.requirement_type || ''; if (!idx.byType[t]) idx.byType[t] = []; idx.byType[t].push(r);
    r._directCount = (r.direct_closing_evidence || []).length;
    r._missingCount = (r.missing_direct_evidence || []).length;
  });
  return idx;
}

/* ---- Schedule grid ---- */
var schedRows = [];
var filteredRows = [];
var currentPage = 1;
var pageSize = 100;
var sortKey = 'source_row';
var sortAsc = true;
var selectedRowNum = null;

function onTabFirstLoad(name){
  if (name === 'requirements'){ loadReportData(); renderSchedule(); wireScheduleFilters(); }
  if (name === 'evidence'){ loadReportData(); renderEvidenceTable('parameter'); }
  if (name === 'elements'){ loadReportData(); renderElementsTable(); }
  if (name === 'rules'){ loadReportData(); renderRulesTable(); }
  if (name === 'ask'){ loadReportData(); }
}

function wireScheduleFilters(){
  function refresh(){ currentPage = 1; applyScheduleFilters(); }
  var search = document.getElementById('schedule-search'); if (search) search.addEventListener('input', refresh);
  var disc = document.getElementById('schedule-discipline'); if (disc) disc.addEventListener('change', refresh);
  var status = document.getElementById('schedule-status'); if (status) status.addEventListener('change', refresh);
  var urg = document.getElementById('schedule-urgency'); if (urg) urg.addEventListener('change', refresh);
  var grp = document.getElementById('schedule-group-by'); if (grp) grp.addEventListener('change', function(){ currentPage = 1; renderSchedulePage(); });
  var clear = document.getElementById('schedule-clear');
  if (clear) clear.addEventListener('click', function(){
    var search = document.getElementById('schedule-search'); if (search) search.value = '';
    var disc = document.getElementById('schedule-discipline'); if (disc) disc.value = '';
    var status = document.getElementById('schedule-status'); if (status) status.value = '';
    var urg = document.getElementById('schedule-urgency'); if (urg) urg.value = '';
    currentPage = 1; applyScheduleFilters();
  });
  var elemSearch = document.getElementById('elements-search'); if (elemSearch) elemSearch.addEventListener('input', function(){ renderElementsTable(); });
}

function renderSchedule(){
  if (!reportIndex){ var tb = document.getElementById('schedule-tbody'); if (tb) tb.innerHTML = '<tr><td colspan="14" style="text-align:center;padding:28px;color:#64748B">No report data found.</td></tr>'; return; }
  schedRows = reportIndex.all.slice();
  applyScheduleFilters();
}

function applyScheduleFilters(){
  var q = (document.getElementById('schedule-search') && document.getElementById('schedule-search').value || '').toLowerCase();
  var disc = document.getElementById('schedule-discipline') && document.getElementById('schedule-discipline').value || '';
  var status = document.getElementById('schedule-status') && document.getElementById('schedule-status').value || '';
  var urg = document.getElementById('schedule-urgency') && document.getElementById('schedule-urgency').value || '';
  filteredRows = schedRows.filter(function(r){
    if (disc && r.discipline !== disc) return false;
    if (status && r.status !== status) return false;
    if (urg && r.urgency !== urg) return false;
    if (q){
      var hay = [r.source_row, r.discipline, r.status, r.urgency, r.requirement_type, r.evidence_alignment, r.requirement_text, r.next_best_action, r.requirement_id].join(' ').toLowerCase();
      if (hay.indexOf(q) === -1) return false;
    }
    return true;
  });
  sortRows();
  renderSchedulePage();
  updateScheduleCount();
}

function sortRows(){
  filteredRows.sort(function(a, b){
    var av = a[sortKey]; var bv = b[sortKey];
    if (av == null) av = 0; if (bv == null) bv = 0;
    if (typeof av === 'number' && typeof bv === 'number') return sortAsc ? av - bv : bv - av;
    av = String(av); bv = String(bv);
    return sortAsc ? av.localeCompare(bv) : bv.localeCompare(av);
  });
}

function handleSort(th){
  var key = th.dataset.sort;
  if (sortKey === key){ sortAsc = !sortAsc; } else { sortKey = key; sortAsc = true; }
  sortRows(); renderSchedulePage();
}

function updateScheduleCount(){
  var el = document.getElementById('schedule-row-count'); if (el) el.textContent = filteredRows.length + ' of ' + schedRows.length + ' rows';
  var tabEl = document.getElementById('tab-schedule-count'); if (tabEl) tabEl.textContent = activeTab === 'requirements' ? (filteredRows.length + ' rows') : '';
}

function renderSchedulePage(){
  var tbody = document.getElementById('schedule-tbody'); if (!tbody) return;
  var groupBy = document.getElementById('schedule-group-by') && document.getElementById('schedule-group-by').value || '';
  var start = (currentPage - 1) * pageSize;
  var page = filteredRows.slice(start, start + pageSize);
  if (page.length === 0){ tbody.innerHTML = '<tr><td colspan="14" style="text-align:center;padding:28px;color:#64748B">No requirements match the current filters.</td></tr>'; renderPagination(); return; }
  var html = ''; var lastGroup = null;
  page.forEach(function(r){
    if (groupBy){
      var gval = r[groupBy] || '(not set)';
      if (gval !== lastGroup){ lastGroup = gval; html += '<tr><td colspan="14" style="background:#E8EDF5;padding:6px 10px;font-weight:700;font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:#334155">' + esc(gval) + '</td></tr>'; }
    }
    var statusCss = statusToCss(r.status);
    var urgCss = urgencyToCss(r.urgency);
    html += '<tr data-row="' + (r.source_row||'') + '">';
    html += '<td><button class="sched-expand-btn" data-row="' + (r.source_row||'') + '" title="Expand">&#9654;</button></td>';
    html += '<td><input type="checkbox" class="sched-select-cb" data-row="' + (r.source_row||'') + '" /></td>';
    html += '<td><strong>' + (r.source_row||'—') + '</strong></td>';
    html += '<td>' + esc(r.discipline || '—') + '</td>';
    html += '<td><span class="sched-status-pill ' + statusCss + '">' + esc(r.status || '—') + '</span></td>';
    html += '<td class="' + urgCss + '">' + esc(r.urgency || '—') + '</td>';
    html += '<td>' + esc((r.requirement_type || '—').substring(0, 28)) + '</td>';
    html += '<td>' + esc(r.evidence_alignment || '—') + '</td>';
    html += '<td>' + (r.confidence != null ? Math.round(r.confidence * 100) + '%' : '—') + '</td>';
    html += '<td>' + (r._directCount || 0) + '</td>';
    html += '<td>' + (r._missingCount || 0) + '</td>';
    html += '<td>' + (r.matched_element_count || 0) + '</td>';
    html += '<td style="max-width:200px;font-size:11px;color:#475569">' + esc((r.next_best_action || '—').substring(0, 80)) + '</td>';
    html += '<td><button class="sched-ai-btn" data-row="' + (r.source_row||'') + '" title="Ask EMA AI about row ' + (r.source_row||'') + '">Ask</button></td>';
    html += '</tr>';
  });
  tbody.innerHTML = html;
  renderPagination();
}

function toggleDetailRow(btn){
  var rowNum = parseInt(btn.dataset.row || '0');
  var tr = btn.closest('tr');
  if (!tr) return;
  var existingDetail = tr.nextSibling;
  if (existingDetail && existingDetail.classList && existingDetail.classList.contains('sched-detail-row')){
    existingDetail.remove(); btn.innerHTML = '&#9654;'; tr.classList.remove('sched-expanded'); return;
  }
  btn.innerHTML = '&#9660;'; tr.classList.add('sched-expanded');
  var r = reportIndex && reportIndex.byRow[rowNum];
  if (!r){ return; }
  var direct = r.direct_closing_evidence || []; var missing = r.missing_direct_evidence || []; var supporting = r.supporting_context || [];
  var html = '<tr class="sched-detail-row"><td colspan="14"><div class="sched-detail-inner">';
  html += '<div class="sched-detail-section"><div class="sched-detail-label">Requirement</div><div class="sched-detail-value">' + esc(r.requirement_text || '—') + '</div></div>';
  html += '<div class="sched-detail-section"><div class="sched-detail-label">Status Reason</div><div class="sched-detail-value">' + esc(r.status_reason || '—') + '</div></div>';
  html += '<div class="sched-detail-section"><div class="sched-detail-label">Direct Closing Evidence (' + direct.length + ')</div><ul class="sched-evidence-list">' + direct.map(function(e){ return '<li>&#10003; ' + esc(e) + '</li>'; }).join('') + (direct.length === 0 ? '<li style="color:#94A3B8">(none captured)</li>' : '') + '</ul></div>';
  html += '<div class="sched-detail-section"><div class="sched-detail-label">Missing Evidence (' + missing.length + ')</div><ul class="sched-evidence-list">' + missing.map(function(e){ return '<li style="color:#DC2626">&#10007; ' + esc(e) + '</li>'; }).join('') + (missing.length === 0 ? '<li style="color:#059669">(none flagged)</li>' : '') + '</ul></div>';
  if (r.next_best_action) html += '<div class="sched-detail-section" style="grid-column:1/-1"><div class="sched-detail-label">Next Best Action</div><div class="sched-detail-value">' + esc(r.next_best_action) + '</div></div>';
  html += '</div></td></tr>';
  var detailTr = document.createElement('tbody');
  detailTr.innerHTML = html;
  tr.parentNode.insertBefore(detailTr.firstElementChild, tr.nextSibling);
}

function renderPagination(){
  var pag = document.getElementById('schedule-pagination'); if (!pag) return;
  var total = filteredRows.length; var pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1){ pag.innerHTML = ''; return; }
  var html = '';
  html += '<button class="page-btn" data-page="' + Math.max(1, currentPage - 1) + '">&laquo;</button>';
  for (var p = Math.max(1, currentPage - 3); p <= Math.min(pages, currentPage + 3); p++){
    html += '<button class="page-btn' + (p === currentPage ? ' active-page' : '') + '" data-page="' + p + '">' + p + '</button>';
  }
  html += '<button class="page-btn" data-page="' + Math.min(pages, currentPage + 1) + '">&raquo;</button>';
  html += '<span class="page-info">Page ' + currentPage + ' of ' + pages + ' &mdash; ' + total + ' rows</span>';
  pag.innerHTML = html;
}

/* ---- Evidence table ---- */
function renderEvidenceTable(view){
  var tbody = document.getElementById('evidence-tbody'); if (!tbody || !reportIndex) return;
  var rows = reportIndex.all;
  var counts = {};
  rows.forEach(function(r){
    var keys = [];
    if (view === 'parameter'){ keys = (r.direct_closing_evidence || []).concat(r.missing_direct_evidence || []); }
    else if (view === 'rule'){ keys = [r.requirement_type || '(not set)']; }
    else if (view === 'category'){ keys = [r.discipline || 'Unknown']; }
    else { keys = ['Row ' + r.source_row]; }
    keys.forEach(function(k){
      if (!k) return;
      if (!counts[k]) counts[k] = { direct:0, missing:0, reqs:0, discs:new Set() };
      counts[k].reqs += 1;
      counts[k].discs.add(r.discipline || 'Unknown');
      if ((r.direct_closing_evidence || []).indexOf(k) > -1) counts[k].direct += 1;
      if ((r.missing_direct_evidence || []).indexOf(k) > -1) counts[k].missing += 1;
    });
  });
  var sorted = Object.keys(counts).sort(function(a, b){ return counts[b].missing - counts[a].missing || counts[b].reqs - counts[a].reqs; }).slice(0, 200);
  var html = sorted.map(function(k){ var c = counts[k]; return '<tr><td>' + esc(k) + '</td><td>' + c.reqs + '</td><td>' + c.direct + '</td><td>' + (c.missing > 0 ? '<strong style="color:#DC2626">' + c.missing + '</strong>' : '0') + '</td><td style="font-size:11px">' + Array.from(c.discs).join(', ') + '</td></tr>'; }).join('');
  tbody.innerHTML = html || '<tr><td colspan="5" style="text-align:center;padding:14px;color:#64748B">No evidence data found.</td></tr>';
}

function switchEvidenceView(btn){
  document.querySelectorAll('.evidence-view-btn').forEach(function(b){ b.classList.remove('active'); });
  btn.classList.add('active');
  renderEvidenceTable(btn.dataset.evidenceView || 'parameter');
}

/* ---- Elements table ---- */
function renderElementsTable(){
  var tbody = document.getElementById('elements-tbody'); if (!tbody || !reportIndex) return;
  var q = (document.getElementById('elements-search') && document.getElementById('elements-search').value || '').toLowerCase();
  var elementMap = {};
  reportIndex.all.forEach(function(r){
    (r.element_traceability || r.matched_elements || []).forEach(function(el){
      var id = el.id || el.element_id || el.Id || '';
      if (!id) return;
      if (!elementMap[id]) elementMap[id] = { id:id, category:el.category||'', family:el.family||'', type:el.type_name||el.type||'', level:el.level||'', reqs:[], roles:[], missing:[] };
      elementMap[id].reqs.push(r.source_row || '');
      if (r.status !== 'Met') elementMap[id].missing = elementMap[id].missing.concat(r.missing_direct_evidence || []);
      elementMap[id].roles.push(r.evidence_alignment || '');
    });
  });
  var allElements = Object.values(elementMap);
  var elCount = document.getElementById('elements-count'); if (elCount) elCount.textContent = allElements.length + ' elements';
  if (q) allElements = allElements.filter(function(el){ return (el.id + ' ' + el.category + ' ' + el.family + ' ' + el.type).toLowerCase().indexOf(q) > -1; });
  if (allElements.length === 0){
    tbody.innerHTML = '<tr><td colspan="9" style="text-align:center;padding:18px;color:#64748B">No elements found. Run Owner Requirements Check to populate.</td></tr>'; return;
  }
  var html = allElements.slice(0, 500).map(function(el){
    return '<tr><td><code style="font-size:11px">' + esc(el.id) + '</code></td><td>' + esc(el.category) + '</td><td>' + esc(el.family) + '</td><td>' + esc(el.type) + '</td><td>' + esc(el.level) + '</td><td style="font-size:11px">' + el.reqs.join(', ') + '</td><td style="font-size:11px">' + el.roles[0] + '</td><td style="font-size:11px;color:#DC2626">' + el.missing.slice(0,3).join(', ') + '</td><td><button class="page-btn" onclick="navigator.clipboard.writeText(\'' + el.id + '\')">Copy ID</button></td></tr>';
  }).join('');
  tbody.innerHTML = html;
}

/* ---- Rules table ---- */
function renderRulesTable(){
  var tbody = document.getElementById('rules-tbody'); if (!tbody || !reportIndex) return;
  var typeCounts = {};
  reportIndex.all.forEach(function(r){
    var t = r.requirement_type || '(not set)'; var v = r.validation_type || '';
    var key = t + '||' + v;
    if (!typeCounts[key]) typeCounts[key] = { type:t, validation:v, count:0, canClose:0, missing:[], rows:[] };
    typeCounts[key].count += 1;
    typeCounts[key].rows.push(r.source_row || '');
    if (r.status === 'Met') typeCounts[key].canClose += 1;
    (r.missing_direct_evidence || []).forEach(function(m){ if (typeCounts[key].missing.indexOf(m) === -1) typeCounts[key].missing.push(m); });
  });
  var sorted = Object.values(typeCounts).sort(function(a, b){ return b.count - a.count; });
  var html = sorted.map(function(t){ return '<tr><td><strong>' + esc(t.type) + '</strong></td><td>' + esc(t.validation) + '</td><td>' + t.canClose + '/' + t.count + ' closed</td><td>&mdash;</td><td style="font-size:11px">' + t.missing.slice(0,4).join(', ') + '</td><td>&mdash;</td><td style="font-size:11px;color:#64748B">' + (t.count - t.canClose > 0 ? 'Missing direct evidence for ' + (t.count - t.canClose) + ' rows' : 'All closed') + '</td><td style="font-size:11px">' + t.rows.slice(0,5).join(', ') + (t.rows.length > 5 ? '...' : '') + '</td></tr>'; }).join('');
  tbody.innerHTML = html || '<tr><td colspan="8" style="text-align:center;padding:14px;color:#64748B">No type data found.</td></tr>';
}

/* ---- CSV Export ---- */
function handleCsvExport(type){
  loadReportData();
  var rows = reportIndex ? reportIndex.all : [];
  var cols, data;
  if (type === 'csv-notmet') rows = rows.filter(function(r){ return r.status === 'Not Met'; });
  if (type === 'csv-review') rows = rows.filter(function(r){ return r.status === 'Needs Human Review'; });
  if (type === 'csv-missing') rows = rows.filter(function(r){ return (r.missing_direct_evidence || []).length > 0; });
  if (type === 'csv-elements'){
    exportElementsCsv(rows); return;
  }
  cols = ['source_row','discipline','status','urgency','requirement_type','validation_type','evidence_alignment','confidence','matched_element_count','next_best_action','requirement_id','requirement_text'];
  if (type === 'csv-missing') cols = ['source_row','discipline','status','urgency','requirement_type','missing_direct_evidence','next_best_action'];
  data = rows.map(function(r){
    return cols.map(function(c){
      var v = r[c];
      if (Array.isArray(v)) v = v.join('; ');
      if (v == null) v = '';
      v = String(v).replace(/"/g, '""');
      return '"' + v + '"';
    }).join(',');
  });
  var csv = [cols.map(function(c){ return '"' + c + '"'; }).join(',')].concat(data).join('\n');
  downloadCsv(csv, 'EMA_AI_Requirements_' + type + '.csv');
}

function exportElementsCsv(rows){
  var elementMap = {};
  rows.forEach(function(r){
    (r.element_traceability || r.matched_elements || []).forEach(function(el){
      var id = el.id || el.element_id || ''; if (!id) return;
      if (!elementMap[id]) elementMap[id] = { id:id, category:el.category||'', family:el.family||'', type:el.type_name||el.type||'', level:el.level||'', reqs:[], missing:[] };
      elementMap[id].reqs.push(r.source_row || ''); elementMap[id].missing = elementMap[id].missing.concat(r.missing_direct_evidence || []);
    });
  });
  var cols = ['element_id','category','family','type','level','linked_requirement_rows','missing_evidence'];
  var data = Object.values(elementMap).map(function(el){
    return [el.id, el.category, el.family, el.type, el.level, el.reqs.join('; '), el.missing.slice(0,5).join('; ')].map(function(v){ return '"' + String(v).replace(/"/g,'""') + '"'; }).join(',');
  });
  var csv = [cols.map(function(c){ return '"' + c + '"'; }).join(',')].concat(data).join('\n');
  downloadCsv(csv, 'EMA_AI_Elements.csv');
}

function downloadCsv(csv, filename){
  try{
    var blob = new Blob([csv], { type:'text/csv' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url; a.download = filename; a.click();
    setTimeout(function(){ URL.revokeObjectURL(url); }, 1000);
  }catch(e){ navigator.clipboard.writeText(csv).catch(function(){}); }
}

/* ---- Ask EMA AI ---- */
var lastAnswer = '';
var lastContext = '';
var selectedScheduleRow = null;

function prefillAskFromRow(rowNum){
  selectedScheduleRow = rowNum;
  var inp = document.getElementById('ask-input'); if (!inp) return;
  inp.value = 'Why is Row ' + rowNum + ' ' + ((reportIndex && reportIndex.byRow[rowNum] && reportIndex.byRow[rowNum].status) || 'Not Met') + '?';
  var cx = document.getElementById('ask-context-scope'); if (cx) cx.value = 'selected';
  updateSelectedContext();
}

function updateSelectedContext(){
  var el = document.getElementById('ask-selected-context'); if (!el) return;
  if (selectedScheduleRow && reportIndex && reportIndex.byRow[selectedScheduleRow]){
    var r = reportIndex.byRow[selectedScheduleRow];
    el.textContent = 'Row ' + r.source_row + ': ' + (r.discipline || '') + ' | ' + (r.status || '') + ' | ' + (r.requirement_type || '');
  } else { el.textContent = 'No requirement selected. Using report summary as context.'; }
}

document.addEventListener('change', function(e){
  if (e.target && e.target.id === 'ask-model-select'){ updateProviderBadge(); }
  if (e.target && e.target.id === 'ask-context-scope'){ updateSelectedContext(); }
});

function updateProviderBadge(){
  var sel = document.getElementById('ask-model-select'); if (!sel) return;
  var val = sel.value;
  var badge = document.getElementById('ask-provider-badge');
  var disc = document.getElementById('ask-provider-disclosure');
  if (val === 'deterministic'){ if (badge){ badge.className = 'provider-badge provider-deterministic'; badge.textContent = 'Deterministic'; } if (disc) disc.textContent = 'Report data stays local. No external calls.'; }
  else if (val.startsWith('ollama/')){ if (badge){ badge.className = 'provider-badge provider-local'; badge.textContent = 'Local'; } if (disc) disc.textContent = 'Data sent to local Ollama. Stays on your machine.'; }
  else if (val.startsWith('openrouter/')){ if (badge){ badge.className = 'provider-badge provider-cloud'; badge.textContent = 'Cloud'; } if (disc) disc.textContent = 'Data sent to OpenRouter cloud. Requires valid credentials.'; }
}

function handleAsk(){
  var inp = document.getElementById('ask-input'); if (!inp) return;
  var question = (inp.value || '').trim(); if (!question) return;
  loadReportData();
  var model = document.getElementById('ask-model-select') && document.getElementById('ask-model-select').value || 'deterministic';
  var scope = document.getElementById('ask-context-scope') && document.getElementById('ask-context-scope').value || 'summary';
  var btn = document.getElementById('ask-btn'); if (btn) { btn.disabled = true; btn.textContent = 'Thinking...'; }
  try{
    var answer = deterministicAnswer(question, scope, selectedScheduleRow);
    showAnswer(answer);
  }catch(ex){ showAnswer({ text: 'Error: ' + (ex && ex.message || 'Unknown error'), refs: [] }); }
  if (btn){ btn.disabled = false; btn.textContent = 'Ask'; }
}

function deterministicAnswer(question, scope, selectedRow){
  if (!reportIndex) return { text: 'No report data loaded. Run Owner Requirements Check first.', refs: [] };
  var q = question.toLowerCase();
  var rows = retrieveRows(q, scope, selectedRow);
  var text = formatAnswer(question, q, rows);
  var refs = rows.slice(0, 10).map(function(r){ return 'Row ' + r.source_row + ': ' + (r.discipline||'') + ' | ' + (r.status||'') + ' | ' + (r.requirement_type||''); });
  lastContext = buildContextSummary(scope, selectedRow);
  return { text: text, refs: refs };
}

function retrieveRows(q, scope, selectedRow){
  var rows = [];
  // Row number match
  var rowMatches = q.match(/row\s*(\d+)/g) || [];
  rowMatches.forEach(function(m){ var n = parseInt(m.replace(/\D/g,'')); if (n && reportIndex.byRow[n] && rows.indexOf(reportIndex.byRow[n]) === -1) rows.push(reportIndex.byRow[n]); });
  // Selected row
  if (selectedRow && reportIndex.byRow[selectedRow] && rows.indexOf(reportIndex.byRow[selectedRow]) === -1) rows.push(reportIndex.byRow[selectedRow]);
  // Discipline + status filters
  var disc = null, status = null, urgency = null;
  ['electrical','lighting','mechanical','plumbing','technology'].forEach(function(d){ if (q.indexOf(d) > -1) disc = d.charAt(0).toUpperCase() + d.slice(1); });
  if (q.indexOf('not met') > -1) status = 'Not Met';
  else if (q.indexOf('met') > -1 && q.indexOf('not') === -1) status = 'Met';
  else if (q.indexOf('human review') > -1 || q.indexOf('needs review') > -1) status = 'Needs Human Review';
  else if (q.indexOf('insufficient') > -1) status = 'Insufficient Model Data';
  if (q.indexOf('critical') > -1) urgency = 'Critical';
  else if (q.indexOf('high') > -1) urgency = 'High';
  if (disc || status || urgency){
    var cands = reportIndex.all.filter(function(r){
      if (disc && r.discipline !== disc) return false;
      if (status && r.status !== status) return false;
      if (urgency && r.urgency !== urgency) return false;
      return true;
    });
    cands.slice(0, 20).forEach(function(r){ if (rows.indexOf(r) === -1) rows.push(r); });
  }
  // Key issues fallback
  if (rows.length === 0 || scope === 'key_issues'){
    reportIndex.keyIssues.slice(0, 10).forEach(function(ki){ var r = reportIndex.byRow[ki.source_row]; if (r && rows.indexOf(r) === -1) rows.push(r); });
  }
  // Summary scope
  if (scope === 'summary' && rows.length === 0) rows = reportIndex.all.filter(function(r){ return r.status !== 'Met' && r.status !== 'Not Applicable'; }).slice(0, 15);
  // Discipline scope from schedule filter
  if (scope === 'discipline'){
    var schedDisc = document.getElementById('schedule-discipline') && document.getElementById('schedule-discipline').value || '';
    if (schedDisc) rows = (reportIndex.byDiscipline[schedDisc] || []).slice(0, 20);
  }
  return rows.length > 0 ? rows : reportIndex.all.slice(0, 10);
}

function formatAnswer(question, q, rows){
  if (rows.length === 0) return 'No matching requirements found. Try a row number, discipline, or status in your question.\n\nThis is a first-pass evidence review, not a compliance certification.';
  var lines = [];
  // Single row: detailed answer
  if (rows.length === 1 || (rows.length <= 3 && /row\s*\d+/.test(q))){
    var r = rows[0];
    lines.push('Row ' + r.source_row + ' — ' + (r.discipline || 'Unknown') + '\n');
    lines.push('Requirement: ' + (r.requirement_text || '(not captured)'));
    lines.push('Status: ' + (r.status || '—') + ' | Urgency: ' + (r.urgency || '—'));
    lines.push('Type: ' + (r.requirement_type || '—') + ' | Validation: ' + (r.validation_type || '—'));
    lines.push('Evidence Alignment: ' + (r.evidence_alignment || '—'));
    lines.push('Confidence: ' + (r.confidence != null ? Math.round(r.confidence * 100) + '%' : '—'));
    lines.push('');
    if (r.status_reason) lines.push('Why this status:\n' + r.status_reason + '\n');
    var direct = r.direct_closing_evidence || [];
    var missing = r.missing_direct_evidence || [];
    if (direct.length > 0){ lines.push('Direct Closing Evidence (' + direct.length + '):'); direct.forEach(function(e){ lines.push('  ✓ ' + e); }); lines.push(''); }
    else { lines.push('Direct Closing Evidence: None found in current model export.\n'); }
    if (missing.length > 0){ lines.push('Missing Evidence (' + missing.length + '):'); missing.forEach(function(e){ lines.push('  ✗ ' + e); }); lines.push(''); }
    if (r.next_best_action) lines.push('Next Best Action:\n' + r.next_best_action + '\n');
    if (r.why_not_model_closeable) lines.push('Why model data may not close this:\n' + r.why_not_model_closeable + '\n');
    if (r.matched_element_count > 0) lines.push('Matched Revit Elements: ' + r.matched_element_count);
    lines.push('');
    lines.push('Source: Row ' + r.source_row + ' | ' + (r.source_worksheet || '(unknown worksheet)') + ' | ' + (r.requirement_id || ''));
  } else {
    // Multi-row summary
    var statusCounts = {}, topMissing = {}, topActions = [];
    rows.forEach(function(r){
      statusCounts[r.status] = (statusCounts[r.status] || 0) + 1;
      (r.missing_direct_evidence || []).forEach(function(m){ topMissing[m] = (topMissing[m] || 0) + 1; });
      if (r.next_best_action && topActions.indexOf(r.next_best_action) === -1 && r.status !== 'Met') topActions.push(r.next_best_action);
    });
    lines.push('Summary — ' + rows.length + ' requirement(s) matched\n');
    Object.keys(statusCounts).forEach(function(s){ lines.push('  ' + s + ': ' + statusCounts[s]); });
    lines.push('');
    var groupedByDisc = {};
    rows.forEach(function(r){ var d = r.discipline || 'Unknown'; if (!groupedByDisc[d]) groupedByDisc[d] = []; groupedByDisc[d].push(r); });
    Object.keys(groupedByDisc).forEach(function(d){
      var dr = groupedByDisc[d];
      lines.push(d + ' (' + dr.length + ' rows):');
      dr.slice(0, 6).forEach(function(r){ lines.push('  Row ' + r.source_row + ': ' + r.status + ' | ' + r.urgency + ' | ' + (r.requirement_type || '—').substring(0,40)); });
      if (dr.length > 6) lines.push('  ... and ' + (dr.length - 6) + ' more.');
      lines.push('');
    });
    var missList = Object.keys(topMissing).sort(function(a,b){ return topMissing[b] - topMissing[a]; }).slice(0,5);
    if (missList.length > 0){ lines.push('Top Missing Evidence:'); missList.forEach(function(m){ lines.push('  ✗ ' + m + ' (' + topMissing[m] + ' requirements)'); }); lines.push(''); }
    if (topActions.length > 0){ lines.push('Recommended Actions:'); topActions.slice(0,5).forEach(function(a, i){ lines.push((i+1) + '. ' + a); }); }
  }
  lines.push('');
  lines.push('This is a first-pass evidence review. Not a compliance certification.');
  return lines.join('\n');
}

function buildContextSummary(scope, selectedRow){
  if (!reportIndex) return '';
  if (selectedRow && reportIndex.byRow[selectedRow]){
    var r = reportIndex.byRow[selectedRow];
    return 'Row ' + r.source_row + ' | ' + r.discipline + ' | ' + r.status + '\n' + (r.requirement_text || '') + '\nMissing: ' + (r.missing_direct_evidence || []).join('; ');
  }
  return 'Report scope: ' + reportIndex.all.length + ' requirements. Not Met: ' + (reportIndex.byStatus['Not Met'] || []).length + '. Needs Review: ' + (reportIndex.byStatus['Needs Human Review'] || []).length;
}

function showAnswer(result){
  lastAnswer = result.text;
  var area = document.getElementById('ask-answer-area'); if (area) { area.style.display = ''; }
  var textEl = document.getElementById('ask-answer-text');
  if (textEl) textEl.textContent = result.text;
  var refsEl = document.getElementById('ask-references');
  var refsList = document.getElementById('ask-references-list');
  if (refsEl && refsList && result.refs && result.refs.length > 0){
    refsList.innerHTML = result.refs.map(function(r){ return '<div style="font-size:12px;padding:3px 0;border-bottom:1px solid #E2E8F0">' + esc(r) + '</div>'; }).join('');
    refsEl.style.display = '';
  } else if (refsEl){ refsEl.style.display = 'none'; }
  var actRow = document.getElementById('ask-actions-row'); if (actRow) actRow.style.display = '';
}

function copyAnswerText(){ if (lastAnswer) navigator.clipboard.writeText(lastAnswer).catch(function(){}); }
function copyCurrentContext(){ if (lastContext) navigator.clipboard.writeText(lastContext).catch(function(){}); }
function clearAskPanel(){
  var area = document.getElementById('ask-answer-area'); if (area) area.style.display = 'none';
  var textEl = document.getElementById('ask-answer-text'); if (textEl) textEl.textContent = '';
  var inp = document.getElementById('ask-input'); if (inp) inp.value = '';
  var actRow = document.getElementById('ask-actions-row'); if (actRow) actRow.style.display = 'none';
  lastAnswer = ''; lastContext = '';
}

/* ---- Helpers ---- */
function statusToCss(s){ if (s === 'Met') return 'sched-met'; if (s === 'Not Met') return 'sched-notmet'; if (s === 'Needs Human Review') return 'sched-review'; if (s === 'Insufficient Model Data') return 'sched-insufficient'; return 'sched-na'; }
function urgencyToCss(u){ if (u === 'Critical') return 'sched-urg-critical'; if (u === 'High') return 'sched-urg-high'; if (u === 'Medium') return 'sched-urg-medium'; return 'sched-urg-low'; }
function esc(s){ if (s == null) return ''; return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""/g,'&quot;'); }

/* ---- Bridge scaffold (disabled until ExternalEvent pass) ---- */
window.emaAiSendCommand = window.emaAiSendCommand || function(cmd){ console.log('[EMA AI Bridge]', JSON.stringify(cmd)); };

})();
""";
        }

        private static string BuildQuestionSetLiteral()
        {
            Dictionary<string, List<string>> questions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "All Disciplines", BuildSuggestedQuestions("All Disciplines") },
                { "Electrical", BuildSuggestedQuestions("Electrical") },
                { "Lighting", BuildSuggestedQuestions("Lighting") },
                { "Mechanical", BuildSuggestedQuestions("Mechanical") },
                { "Plumbing", BuildSuggestedQuestions("Plumbing") },
                { "Technology", BuildSuggestedQuestions("Technology") },
                { "Unknown / Needs Classification", BuildSuggestedQuestions("Unknown / Needs Classification") }
            };

            StringBuilder js = new StringBuilder();
            js.Append('{');
            bool firstGroup = true;
            foreach (KeyValuePair<string, List<string>> pair in questions)
            {
                if (!firstGroup)
                {
                    js.Append(',');
                }
                firstGroup = false;
                js.Append(JsString(pair.Key));
                js.Append(':');
                js.Append('[');
                for (int index = 0; index < pair.Value.Count; index++)
                {
                    if (index > 0)
                    {
                        js.Append(',');
                    }
                    js.Append(JsString(pair.Value[index]));
                }
                js.Append(']');
            }
            js.Append('}');
            return js.ToString();
        }

        private static string BuildIssueRow(KeyIssue issue, bool initiallyVisible)
        {
            if (issue == null)
            {
                return string.Empty;
            }

            string requirementAnchor = BuildRequirementAnchor(new RequirementCheckResult
            {
                RequirementId = issue.RequirementId,
                SourceRow = issue.SourceRow
            });
            string disciplineClass = DisciplineCssClass(issue.Discipline);
            string disciplineBadgeClass = DisciplineBadgeClass(issue.Discipline);
            string normalizedUrgency = NormalizeUrgency(issue.SeverityLabel);
            string urgencyClass = UrgencyCssClass(normalizedUrgency);
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"compact-row issue-card " + disciplineClass + " " + StatusCssClass(issue.Status) + " " + urgencyClass + "\" id=\"" + Encode(requirementAnchor + "-issue") + "\" data-report-card=\"issue\" data-discipline=\"" + Encode(issue.Discipline) + "\" data-status=\"" + Encode(StatusLabel(issue.Status)) + "\" data-urgency=\"" + Encode(normalizedUrgency) + "\" data-keyissue=\"true\" data-issue-title=\"" + Encode(issue.IssueTitle) + "\" data-requirement-anchor=\"" + Encode(requirementAnchor) + "\"" + (initiallyVisible ? string.Empty : " style=\"display:none\"") + ">");
            html.AppendLine("<div class=\"result-head\">");
            html.AppendLine("<div>");
            html.AppendLine("<div class=\"title\">#" + issue.Rank.ToString(CultureInfo.InvariantCulture) + " " + Encode(SafeText(issue.IssueTitle)) + "</div>");
            html.AppendLine("<div class=\"subtitle\">" + Encode(issue.RequirementId) + " | " + Encode(issue.Discipline) + " | " + Encode(issue.SourceWorksheet) + " | Row " + issue.SourceRow.ToString(CultureInfo.InvariantCulture) + "</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div style=\"display:flex;gap:8px;flex-wrap:wrap;align-items:center;justify-content:flex-end\">");
            html.AppendLine("<span class=\"discipline-pill " + disciplineBadgeClass + "\">" + Encode(issue.Discipline) + "</span>");
            html.AppendLine("<span class=\"pill " + IssuePillClass(issue.Status) + "\">" + Encode(StatusLabel(issue.Status)) + "</span>");
            html.AppendLine("<span class=\"pill " + urgencyClass + "\">" + Encode(normalizedUrgency) + "</span>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"detail-grid\">");
            AddField(html, "Why this is urgent", SafeText(issue.UrgencyReason));
            AddField(html, "Evidence gap", SafeText(issue.EvidenceGap));
            AddField(html, "Affected scoped elements", issue.AffectedScopedElements.ToString(CultureInfo.InvariantCulture));
            AddField(html, "Next Best Action", SafeText(issue.NextBestAction));
            AddField(html, "Key Issue Score", Math.Round(issue.KeyIssueScore * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%");
            AddFieldHtml(html, "Requirement Link", "<a class=\"discipline-link\" href=\"#" + Encode(requirementAnchor) + "\">Jump to requirement detail</a>");
            html.AppendLine("</div>");
            html.AppendLine(BuildKeyIssueDetails(issue));
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static string BuildKeyIssueDetails(KeyIssue issue)
        {
            if (issue == null)
            {
                return string.Empty;
            }

            StringBuilder html = new StringBuilder();
            html.AppendLine("<details class=\"traceability\">");
            html.AppendLine("<summary>View Score Details</summary>");
            html.AppendLine("<div class=\"details-grid\">");
            AddField(html, "StatusSeverity", issue.StatusSeverityScore.ToString("0.00", CultureInfo.InvariantCulture));
            AddField(html, "DeliverableImpact", issue.DeliverableImpactScore.ToString("0.00", CultureInfo.InvariantCulture));
            AddField(html, "Actionability", issue.ActionabilityScore.ToString("0.00", CultureInfo.InvariantCulture) + " - " + SafeText(issue.Actionability));
            AddField(html, "EvidenceGap", issue.EvidenceGapScore.ToString("0.00", CultureInfo.InvariantCulture));
            AddField(html, "RequirementTypeRisk", issue.RequirementTypeRiskScore.ToString("0.00", CultureInfo.InvariantCulture));
            AddField(html, "ImpactScale", issue.ImpactScaleScore.ToString("0.00", CultureInfo.InvariantCulture));
            AddField(html, "CandidateScopeValid", issue.CandidateScopeValid ? "Yes" : "No");
            AddField(html, "FullModelFallbackUsed", issue.FullModelFallbackUsed ? "Yes" : "No");
            AddField(html, "Score Formula", SafeText(issue.KeyIssueScoreReason));
            html.AppendLine("</div>");
            html.AppendLine("</details>");

            html.AppendLine("<details class=\"traceability\">");
            html.AppendLine("<summary>View Evidence Behind This Issue</summary>");
            html.AppendLine("<div class=\"details-grid\">");
            AddField(html, "Evidence Summary", SafeText(issue.EvidenceSummary));
            AddField(html, "Reasoning", SafeText(issue.Reasoning));
            AddField(html, "Requirement Type", SafeText(issue.RequirementType));
            AddField(html, "Discipline Owner", SafeText(issue.ResponsibleRole));
            AddField(html, "Confidence", Math.Round(issue.Confidence * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%");
            AddField(html, "Source File", FormatSourceFileName(issue.SourceFile));
            AddField(html, "Source Worksheet", SafeText(issue.SourceWorksheet));
            AddField(html, "Source Row", issue.SourceRow.ToString(CultureInfo.InvariantCulture));
            html.AppendLine("</div>");
            html.AppendLine("</details>");
            return html.ToString();
        }

        private static bool MatchesIssueFilter(KeyIssue issue, string discipline, string status, string urgency)
        {
            if (issue == null)
            {
                return false;
            }

            bool disciplineMatch = string.Equals(discipline, "All Disciplines", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Discipline, discipline, StringComparison.OrdinalIgnoreCase);
            bool statusMatch = string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(StatusLabel(issue.Status), status, StringComparison.OrdinalIgnoreCase);
            bool urgencyMatch = string.Equals(urgency, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeUrgency(issue.SeverityLabel), urgency, StringComparison.OrdinalIgnoreCase);

            return disciplineMatch && statusMatch && urgencyMatch;
        }

        private static string BuildResultCard(RequirementCheckResult result, bool initiallyVisible)
        {
            if (result == null)
            {
                return string.Empty;
            }

            string discipline = GetResultDiscipline(result);
            string statusLabel = result.StatusLabel;
            string urgency = NormalizeUrgency(string.IsNullOrWhiteSpace(result.Urgency) ? DefaultUrgency(result.Status) : result.Urgency);
            string requirementAnchor = BuildRequirementAnchor(result);
            string statusClass = StatusCssClass(result.Status);
            string urgencyClass = UrgencyCssClass(urgency);
            string disciplineClass = DisciplineCssClass(discipline);
            string disciplineBadgeClass = DisciplineBadgeClass(discipline);
            string disciplineCardClass = DisciplineCardClass(discipline);
            StringBuilder html = new StringBuilder();
            html.AppendLine("<article class=\"result-card requirement-card " + disciplineClass + " " + disciplineCardClass + " " + statusClass + " " + urgencyClass + "\" id=\"" + Encode(requirementAnchor) + "\" data-report-card=\"result\" data-discipline=\"" + Encode(discipline) + "\" data-status=\"" + Encode(statusLabel) + "\" data-urgency=\"" + Encode(urgency) + "\" data-keyissue=\"" + Encode(result.IsKeyIssue ? "true" : "false") + "\" data-confidence=\"" + result.Confidence.ToString("0.00", CultureInfo.InvariantCulture) + "\" data-nextaction=\"" + Encode(SafeText(result.NextBestAction)) + "\" data-requirement-anchor=\"" + Encode(requirementAnchor) + "\" style=\"border-left:6px solid " + DisciplinePrimaryColor(discipline) + ";" + (initiallyVisible ? string.Empty : "display:none;") + "\">");
            html.AppendLine("<div class=\"result-head\">");
            html.AppendLine("<div>");
            html.AppendLine("<div class=\"result-title\">" + Encode(string.IsNullOrWhiteSpace(result.IssueTitle) ? "Requirement" : result.IssueTitle) + "</div>");
            html.AppendLine("<div class=\"result-meta\">" + Encode(BuildRequirementReference(result)) + " | Discipline: " + Encode(discipline) + " | Role: " + Encode(string.IsNullOrWhiteSpace(result.ResponsibleRole) ? "Unknown / Needs Classification" : result.ResponsibleRole) + "</div>");
            html.AppendLine("</div>");
            html.AppendLine("<div style=\"display:flex;gap:8px;flex-wrap:wrap;align-items:center;justify-content:flex-end\">");
            html.AppendLine("<span class=\"discipline-pill " + disciplineBadgeClass + "\">" + Encode(discipline) + "</span>");
            html.AppendLine("<span class=\"pill " + ResultPillClass(result.Status) + "\">" + Encode(statusLabel) + "</span>");
            html.AppendLine("<span class=\"pill " + urgencyClass + "\">" + Encode(urgency) + "</span>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            html.AppendLine("<div class=\"detail-grid\">");
            AddField(html, "Requirement ID / Row", BuildRequirementReference(result));
            AddField(html, "Validation Type", result.ValidationType.ToString());
            AddField(html, "Requirement Type", SafeText(string.IsNullOrWhiteSpace(result.RequirementType) ? "unknown_ambiguous" : result.RequirementType));
            AddField(html, "Confidence / Evidence Confidence", Math.Round(result.Confidence * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "% | " + result.EvidenceAlignmentLabel);
            html.AppendLine("</div>");

            html.AppendLine("<div class=\"requirement-text-block\"><span class=\"field-label\">Requirement Text</span>" + Encode(SafeText(!string.IsNullOrWhiteSpace(result.RequirementText) ? result.RequirementText : result.Requirement != null ? result.Requirement.RequirementText : null)) + "</div>");
            html.AppendLine(BuildDecisionSummary(result));
            html.AppendLine(BuildKeyEvidenceSnapshot(result));
            html.AppendLine(BuildKeyParametersConsidered(result, 8));
            AddField(html, "Next Best Action", SafeText(result.NextBestAction), "next-action-block");
            html.AppendLine(BuildRequirementDropdowns(result));
            html.AppendLine("</article>");
            return html.ToString();
        }

        private static string BuildDecisionSummary(RequirementCheckResult result)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"section-copy\" style=\"margin-top:14px;margin-bottom:6px;font-weight:800;color:var(--navy);\">Decision Summary</div>");
            html.AppendLine("<div class=\"decision-grid\">");
            AddField(html, "Why this status?", SafeText(BuildStatusReasonDisplay(result)), "reasoning-block");
            AddFieldHtml(html, "What evidence was used?", BuildEvidenceSummaryBlock(result), "evidence-block");
            AddFieldHtml(html, "What is missing?", BuildMissingEvidenceBlock(result), "missing-evidence-block");
            AddField(html, "Human review needed?", result != null && result.HumanReviewNeeded ? "Yes" : "No");
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static string BuildKeyEvidenceSnapshot(RequirementCheckResult result)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"snapshot-grid\">");
            AddField(html, "Matched Element Count", result == null ? "0" : result.MatchedModelElementCount.ToString(CultureInfo.InvariantCulture));
            AddFieldHtml(html, "Category / Family / Type Examples", BuildFamilyTypeSummaryBlock(result), "family-type-block");
            AddField(html, "Missing Parameter Names", result == null ? "(not captured)" : FormatList(result.MissingExpectedParameters != null && result.MissingExpectedParameters.Count > 0 ? result.MissingExpectedParameters : result.MissingEvidence));
            AddFieldHtml(html, "Evidence Alignment", BuildEvidenceAlignmentBadge(result), "alignment-block");
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static string BuildRequirementDropdowns(RequirementCheckResult result)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<details class=\"traceability\">");
            html.AppendLine("<summary>View Rule &amp; Decision Logic</summary>");
            html.AppendLine("<div class=\"details-grid\">");
            AddFieldHtml(html, "Requirement Type", BuildRequirementTypeBlock(result));
            AddFieldHtml(html, "Rule Applied", BuildRuleAppliedBlock(result), "rule-block");
            AddField(html, "Rule Family", SafeText(result != null ? result.RuleFamily : null));
            AddField(html, "Trigger Keywords", result == null ? "(not captured)" : FormatList(result.RuleTriggerKeywords));
            AddFieldHtml(html, "Validation Type Reason", BuildValidationTypeBlock(result));
            AddField(html, "Evidence Alignment Reason", SafeText(result != null ? result.EvidenceAlignmentReason : null));
            AddField(html, "Status Reason", SafeText(BuildStatusReasonDisplay(result)));
            AddField(html, "Confidence Reason", SafeText(BuildConfidenceReasonDisplay(result)));
            AddField(html, "Guardrails Applied", SafeText(result != null ? result.ModelEvidenceLimitations : null));
            AddField(html, "Why model data is or is not sufficient", SafeText(result != null ? (!string.IsNullOrWhiteSpace(result.WhyNotModelCloseable) ? result.WhyNotModelCloseable : result.ModelEvidenceSufficiency) : null));
            html.AppendLine("</div>");
            html.AppendLine("</details>");

            html.AppendLine("<details class=\"traceability\">");
            html.AppendLine("<summary>View Filtering Details</summary>");
            AddFieldHtml(html, "Full filtering trace", BuildFilteringDetailsBlock(result), "filtering-block");
            html.AppendLine("</details>");

            html.AppendLine("<details class=\"traceability\">");
            html.AppendLine("<summary>View Parameter Checks</summary>");
            AddFieldHtml(html, "All Parameter Checks", BuildParameterChecksBlock(result), "parameter-checks-block");
            html.AppendLine("</details>");

            html.AppendLine(BuildEvidenceTraceabilityBlock(result));

            html.AppendLine("<details class=\"traceability\">");
            html.AppendLine("<summary>View Source &amp; Traceability</summary>");
            html.AppendLine("<div class=\"details-grid\">");
            AddField(html, "Source Worksheet", SafeText(result != null && !string.IsNullOrWhiteSpace(result.SourceWorksheet) ? result.SourceWorksheet : result != null && result.Requirement != null ? result.Requirement.SourceSheet : null));
            AddField(html, "Source Row", result != null && result.SourceRow > 0 ? result.SourceRow.ToString(CultureInfo.InvariantCulture) : "(not set)");
            AddField(html, "Source File", FormatSourceFileName(result != null && !string.IsNullOrWhiteSpace(result.SourceFile) ? result.SourceFile : result != null && result.Requirement != null ? result.Requirement.SourceFile : null));
            AddFieldHtml(html, "Generated Anchor", "<a class=\"discipline-link\" href=\"#" + Encode(result == null ? string.Empty : BuildRequirementAnchor(result)) + "\">#" + Encode(result == null ? string.Empty : BuildRequirementAnchor(result)) + "</a>");
            html.AppendLine("</div>");
            html.AppendLine("</details>");
            return html.ToString();
        }

        private static bool MatchesFilter(RequirementCheckResult result, string discipline, string status, string urgency)
        {
            if (result == null)
            {
                return false;
            }

            string resultDiscipline = GetResultDiscipline(result);
            string resultStatus = result.StatusLabel;
            string resultUrgency = NormalizeUrgency(string.IsNullOrWhiteSpace(result.Urgency) ? DefaultUrgency(result.Status) : result.Urgency);

            bool disciplineMatch = string.Equals(discipline, "All Disciplines", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultDiscipline, discipline, StringComparison.OrdinalIgnoreCase);
            bool statusMatch = string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultStatus, status, StringComparison.OrdinalIgnoreCase);
            bool urgencyMatch = string.Equals(urgency, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resultUrgency, urgency, StringComparison.OrdinalIgnoreCase);

            return disciplineMatch && statusMatch && urgencyMatch;
        }

        private static List<RequirementCheckResult> FilterResults(IReadOnlyCollection<RequirementCheckResult> results, string discipline, string status, string urgency)
        {
            if (results == null)
            {
                return new List<RequirementCheckResult>();
            }

            return results
                .Where(result => MatchesFilter(result, discipline, status, urgency))
                .OrderByDescending(ResultPriority)
                .ThenByDescending(item => item != null ? item.Confidence : 0.0)
                .ThenBy(item => BuildRequirementReference(item))
                .ToList();
        }

        private static List<KeyIssue> FilterIssues(IReadOnlyCollection<KeyIssue> issues, string discipline, string status, string urgency)
        {
            if (issues == null)
            {
                return new List<KeyIssue>();
            }

            return issues
                .Where(issue => issue != null &&
                    (string.Equals(discipline, "All Disciplines", StringComparison.OrdinalIgnoreCase) || string.Equals(issue.Discipline, discipline, StringComparison.OrdinalIgnoreCase)) &&
                    (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(StatusLabel(issue.Status), status, StringComparison.OrdinalIgnoreCase)) &&
                    (string.Equals(urgency, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(NormalizeUrgency(issue.SeverityLabel), urgency, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(item => item.KeyIssueScore)
                .ThenBy(item => item.Rank)
                .ToList();
        }

        private static List<string> BuildTopActions(IReadOnlyCollection<RequirementCheckResult> results, int topCount)
        {
            if (results == null)
            {
                return new List<string>();
            }

            return results
                .Where(item => item != null && item.Status != RequirementCheckStatus.Met && item.Status != RequirementCheckStatus.NotApplicable && !string.IsNullOrWhiteSpace(item.NextBestAction))
                .OrderByDescending(ResultPriority)
                .ThenByDescending(item => item.Confidence)
                .Select(item => item.NextBestAction)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(topCount, 0))
                .ToList();
        }

        private static List<string> BuildSuggestedQuestions(string discipline)
        {
            if (string.Equals(discipline, "Electrical", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Electrical issues?",
                    "Why are these Electrical requirements Not Met?",
                    "Which Electrical requirements need human review?",
                    "What should the Electrical team fix first?",
                    "Which Electrical model data is missing?"
                };
            }

            if (string.Equals(discipline, "Lighting", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Lighting issues?",
                    "Why are these Lighting requirements Not Met?",
                    "Which Lighting requirements need human review?",
                    "What should the Lighting team fix first?",
                    "Which Lighting model data is missing?"
                };
            }

            if (string.Equals(discipline, "Mechanical", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Mechanical issues?",
                    "Why are these Mechanical requirements Not Met?",
                    "Which Mechanical requirements need human review?",
                    "What should the Mechanical team fix first?",
                    "Which Mechanical model data is missing?"
                };
            }

            if (string.Equals(discipline, "Plumbing", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Plumbing issues?",
                    "Why are these Plumbing requirements Not Met?",
                    "Which Plumbing requirements need human review?",
                    "What should the Plumbing team fix first?",
                    "Which Plumbing model data is missing?"
                };
            }

            if (string.Equals(discipline, "Technology", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Technology issues?",
                    "Why are these Technology requirements Not Met?",
                    "Which Technology requirements need human review?",
                    "What should the Technology team fix first?",
                    "Which Technology model data is missing?"
                };
            }

            return new List<string>
            {
                "What are the top project-level issues?",
                "Which discipline has the most risk?",
                "What should be fixed first?",
                "Which requirements need human review?",
                "Which model data is missing?"
            };
        }

        private static List<string> BuildMatchedKeywords(RequirementCheckResult result)
        {
            HashSet<string> keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in Tokenize(result != null ? result.RequirementText : null))
            {
                keywords.Add(token);
            }

            foreach (string token in Tokenize(result != null ? result.IssueTitle : null))
            {
                keywords.Add(token);
            }

            AddCollectionTokens(keywords, result != null ? result.MatchedCategories : null);
            AddCollectionTokens(keywords, result != null ? result.MatchedParameters : null);
            AddCollectionTokens(keywords, result != null ? result.MatchedFamilies : null);
            AddCollectionTokens(keywords, result != null ? result.MatchedTypes : null);

            return keywords.Take(12).ToList();
        }

        private static List<string> BuildSearchTerms(RequirementCheckResult result)
        {
            HashSet<string> terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddCollectionTokens(terms, result != null ? result.MatchedCategories : null);
            AddCollectionTokens(terms, result != null ? result.MatchedParameters : null);
            AddCollectionTokens(terms, result != null ? result.MatchedFamilies : null);
            AddCollectionTokens(terms, result != null ? result.MatchedTypes : null);
            foreach (string token in Tokenize(result != null ? result.RequirementText : null))
            {
                terms.Add(token);
            }

            return terms.Take(12).ToList();
        }

        private static string BuildEvidenceLocation(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "revit_model_elements";
            }

            switch (result.Status)
            {
                case RequirementCheckStatus.NeedsHumanReview:
                    return "specification_or_manual_review";
                case RequirementCheckStatus.NotApplicable:
                    return "scope_filter";
                case RequirementCheckStatus.InsufficientModelData:
                case RequirementCheckStatus.NotMet:
                case RequirementCheckStatus.Met:
                default:
                    return "revit_model_elements";
            }
        }

        private static string BuildSuggestedQuestionForResult(RequirementCheckResult result)
        {
            string discipline = GetResultDiscipline(result);
            switch (result != null ? result.Status : RequirementCheckStatus.NotApplicable)
            {
                case RequirementCheckStatus.NotMet:
                    return "Why is this " + discipline + " requirement Not Met?";
                case RequirementCheckStatus.NeedsHumanReview:
                    return "What needs human review for this " + discipline + " requirement?";
                case RequirementCheckStatus.InsufficientModelData:
                    return "What model data is missing for this " + discipline + " requirement?";
                case RequirementCheckStatus.Met:
                    return "What evidence supports this " + discipline + " requirement being Met?";
                case RequirementCheckStatus.NotApplicable:
                default:
                    return "Why is this requirement marked Not Applicable?";
            }
        }

        private static double CalculateEvidenceStrength(RequirementCheckResult result)
        {
            if (result == null)
            {
                return 0.0;
            }

            double evidenceScore = result.Evidence == null ? 0.0 : Math.Min(1.0, result.Evidence.Count / 5.0);
            double modelScore = result.MatchedModelElementCount <= 0 ? 0.0 : Math.Min(1.0, result.MatchedModelElementCount / 25.0);
            double confidenceScore = Math.Max(0.0, Math.Min(1.0, result.Confidence));

            return Math.Round((evidenceScore * 0.35) + (modelScore * 0.35) + (confidenceScore * 0.30), 2);
        }

        private static void AddCollectionTokens(ISet<string> target, IEnumerable<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (string item in source)
            {
                foreach (string token in Tokenize(item))
                {
                    target.Add(token);
                }
            }
        }

        private static IEnumerable<string> Tokenize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (string token in value
                .Split(new[] { ' ', '\t', '\r', '\n', ',', ';', ':', '.', '/', '\\', '-', '(', ')', '[', ']', '{', '}', '|', '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = token.Trim();
                if (trimmed.Length < 3)
                {
                    continue;
                }

                yield return trimmed;
            }
        }

        private static string GetDisciplineLabel(RequirementDiscipline discipline)
        {
            return discipline == RequirementDiscipline.All ? "All Disciplines" : discipline.ToString();
        }

        private static RequirementDiscipline ParseDiscipline(string discipline)
        {
            return RequirementDisciplineNormalizer.Parse(discipline, RequirementDiscipline.All);
        }

        private static string GetResultDiscipline(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "Unknown / Needs Classification";
            }

            string discipline = !string.IsNullOrWhiteSpace(result.Discipline)
                ? result.Discipline
                : result.Requirement != null ? result.Requirement.Discipline : string.Empty;
            RequirementDiscipline parsed = RequirementDisciplineNormalizer.Parse(discipline, RequirementDiscipline.All);
            return parsed == RequirementDiscipline.All ? "Unknown / Needs Classification" : parsed.ToString();
        }

        private static string GetScopeLabel(RequirementModelScope scope)
        {
            return scope == RequirementModelScope.CurrentView ? "Current View" : "Entire Model";
        }

        private static string BuildPdfStem(string discipline, DateTime generatedAt)
        {
            string safeDiscipline = string.Equals(discipline, "All Disciplines", StringComparison.OrdinalIgnoreCase)
                ? "Master"
                : SanitizeFileName(discipline);
            string timestamp = generatedAt == default(DateTime)
                ? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                : generatedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return "EMA_AI_Requirement_Check_" + safeDiscipline + "_" + timestamp;
        }

        private static string FormatTimestamp(DateTime value)
        {
            if (value == default(DateTime))
            {
                return "(not set)";
            }

            return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(not set)" : value;
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? string.Empty : value);
        }

        private static string JsString(string value)
        {
            string safe = value ?? string.Empty;
            return "'" + safe.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", string.Empty).Replace("\n", "\\n") + "'";
        }

        private static string BuildRequirementReference(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "(unknown requirement)";
            }

            string id = !string.IsNullOrWhiteSpace(result.RequirementId)
                ? result.RequirementId
                : result.Requirement != null && !string.IsNullOrWhiteSpace(result.Requirement.RequirementId)
                    ? result.Requirement.RequirementId
                    : "(no id)";
            int rowNumber = result.SourceRow > 0 ? result.SourceRow : result.Requirement != null ? result.Requirement.RowNumber : 0;
            string rowLabel = rowNumber > 0 ? "Row " + rowNumber.ToString(CultureInfo.InvariantCulture) : "(row unknown)";
            string worksheet = !string.IsNullOrWhiteSpace(result.SourceWorksheet)
                ? result.SourceWorksheet
                : result.Requirement != null && !string.IsNullOrWhiteSpace(result.Requirement.SourceSheet)
                    ? result.Requirement.SourceSheet
                    : "(worksheet unknown)";

            return id + " | " + worksheet + " | " + rowLabel;
        }

        private static string BuildEvidenceAlignmentBadge(RequirementCheckResult result)
        {
            if (result == null) return Encode("Unknown");

            string level = result.EvidenceAlignmentLabel;
            string reason = string.IsNullOrWhiteSpace(result.EvidenceAlignmentReason)
                ? string.Empty
                : result.EvidenceAlignmentReason;
            string colorClass = result.EvidenceAlignment switch
            {
                EvidenceAlignmentLevel.Strong => "color:var(--met)",
                EvidenceAlignmentLevel.Partial => "color:var(--review)",
                EvidenceAlignmentLevel.Weak => "color:var(--insufficient)",
                EvidenceAlignmentLevel.MismatchRisk => "color:var(--not-met);font-weight:600",
                EvidenceAlignmentLevel.ManualOnly => "color:var(--muted)",
                _ => "color:var(--muted)"
            };
            string html = "<span style=\"" + colorClass + ";font-weight:600;\">" + Encode(level) + "</span>";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                html += "<div style=\"margin-top:4px;font-size:12px;color:var(--text-secondary);\">" + Encode(reason) + "</div>";
            }
            return html;
        }

        private static string BuildValidationTypeBlock(RequirementCheckResult result)
        {
            if (result == null) return Encode("Unknown");

            string typeLabel = result.ValidationType.ToString();
            string reason = string.IsNullOrWhiteSpace(result.ValidationTypeReason)
                ? string.Empty
                : result.ValidationTypeReason;
            string html = "<strong>" + Encode(typeLabel) + "</strong>";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                html += "<div style=\"margin-top:4px;font-size:12px;color:var(--text-secondary);\">" + Encode(reason) + "</div>";
            }
            return html;
        }

        private static string BuildRequirementTypeBlock(RequirementCheckResult result)
        {
            if (result == null)
            {
                return Encode("Unknown");
            }

            string typeLabel = string.IsNullOrWhiteSpace(result.RequirementType) ? "unknown_ambiguous" : result.RequirementType;
            string reason = string.IsNullOrWhiteSpace(result.RequirementTypeReason)
                ? string.Empty
                : result.RequirementTypeReason;
            StringBuilder html = new StringBuilder();
            html.Append("<strong>" + Encode(typeLabel) + "</strong>");
            if (!string.IsNullOrWhiteSpace(reason))
            {
                html.Append("<div style=\"margin-top:4px;font-size:12px;color:var(--text-secondary);\">" + Encode(reason) + "</div>");
            }
            return html.ToString();
        }

        private static string BuildRuleAppliedBlock(RequirementCheckResult result)
        {
            if (result == null) return Encode("(none)");

            string ruleName = string.IsNullOrWhiteSpace(result.RuleApplied) ? "(none)" : result.RuleApplied;
            string ruleFamily = string.IsNullOrWhiteSpace(result.RuleFamily) ? string.Empty : result.RuleFamily;
            string expectedEvidence = string.IsNullOrWhiteSpace(result.RuleExpectedEvidence) ? string.Empty : result.RuleExpectedEvidence;
            List<string> keywords = result.RuleTriggerKeywords ?? new List<string>();

            StringBuilder html = new StringBuilder();
            html.Append("<div style=\"font-size:13px;\">");
            html.Append("<strong>Rule:</strong> " + Encode(ruleName));
            if (!string.IsNullOrWhiteSpace(ruleFamily))
            {
                html.Append(" <span style=\"color:var(--muted);\">[" + Encode(ruleFamily) + "]</span>");
            }
            html.Append("</div>");

            if (keywords.Count > 0)
            {
                html.Append("<div style=\"margin-top:4px;font-size:12px;\"><strong>Triggered by:</strong> ");
                html.Append(string.Join(", ", keywords.Select(kw => "<code style=\"background:var(--bg-secondary);padding:1px 5px;border-radius:3px;font-size:11px;\">" + Encode(kw) + "</code>")));
                html.Append("</div>");
            }

            if (!string.IsNullOrWhiteSpace(expectedEvidence))
            {
                html.Append("<div style=\"margin-top:4px;font-size:12px;color:var(--text-secondary);\"><strong>Expected:</strong> " + Encode(expectedEvidence) + "</div>");
            }

            return html.ToString();
        }

        private static string BuildFilteringDetailsBlock(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "<span style=\"color:var(--muted);font-size:12px;\">No filtering trace captured.</span>";
            }

            RequirementFilterTrace trace = result.FilterTrace ?? new RequirementFilterTrace();
            StringBuilder html = new StringBuilder();
            html.Append("<div style=\"display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:8px;\">");
            AddInlineTraceChip(html, "Requirement Type", SafeText(trace.RequirementType));
            AddInlineTraceChip(html, "Discipline", SafeText(trace.DisciplineFilter));
            AddInlineTraceChip(html, "Scope", SafeText(trace.ScopeFilter));
            AddInlineTraceChip(html, "Requirement Intent", SafeText(trace.RequirementIntent));
            AddInlineTraceChip(html, "Validation Type", SafeText(trace.ValidationType));
            AddInlineTraceChip(html, "Rule Applied", SafeText(trace.RuleApplied));
            AddInlineTraceChip(html, "Candidate Scope", SafeText(trace.CandidateScopeReason));
            AddInlineTraceChip(html, "Fallback Allowed", trace.FallbackAllowed ? "Yes" : "No");
            AddInlineTraceChip(html, "Expected Params", trace.ExpectedParameters.Count == 0 ? "(none)" : string.Join(", ", trace.ExpectedParameters.Take(5)));
            AddInlineTraceChip(html, "Allowed Categories", trace.AllowedCategories.Count == 0 ? "(none)" : string.Join(", ", trace.AllowedCategories.Take(5)));
            AddInlineTraceChip(html, "Excluded Categories", trace.ExcludedCategories.Count == 0 ? "(none)" : string.Join(", ", trace.ExcludedCategories.Take(5)));
            AddInlineTraceChip(html, "Model Sufficiency", SafeText(trace.ModelEvidenceSufficiency));
            html.Append("</div>");

            html.Append("<details class=\"traceability\" style=\"margin-top:8px;\">");
            html.Append("<summary>View filtering stages</summary>");
            html.Append("<div class=\"matched-elements-scroll\" style=\"margin-top:10px;max-height:260px;\">");
            html.Append("<table><thead><tr><th>Stage</th><th>Input</th><th>Output</th><th>Criteria</th><th>Examples</th></tr></thead><tbody>");
            if (trace.CandidateStages != null && trace.CandidateStages.Count > 0)
            {
                foreach (FilterStageTrace stage in trace.CandidateStages)
                {
                    html.Append("<tr>");
                    html.Append("<td>" + Encode(stage.StageName) + "</td>");
                    html.Append("<td>" + Encode(stage.InputCount.ToString(CultureInfo.InvariantCulture)) + "</td>");
                    html.Append("<td>" + Encode(stage.OutputCount.ToString(CultureInfo.InvariantCulture)) + "</td>");
                    html.Append("<td>" + Encode(stage.Criteria) + "</td>");
                    html.Append("<td>" + Encode(stage.ExampleMatchedValues == null ? string.Empty : string.Join("; ", stage.ExampleMatchedValues.Take(3))) + "</td>");
                    html.Append("</tr>");
                }
            }
            else
            {
                html.Append("<tr><td colspan=\"5\" style=\"color:var(--muted);\">No filtering stages were captured for this sample result.</td></tr>");
            }
            html.Append("</tbody></table></div></details>");

            return html.ToString();
        }

        private static string BuildParameterChecksBlock(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "<span style=\"color:var(--muted);font-size:12px;\">No parameter checks captured.</span>";
            }

            List<ParameterCheckResult> checks = result.ParameterChecks ?? new List<ParameterCheckResult>();
            if (checks.Count == 0)
            {
                return BuildParameterExamplesBlock(result);
            }

            StringBuilder html = new StringBuilder();
            html.Append("<div class=\"matched-elements-scroll\" style=\"max-height:360px;\">");
            html.Append("<table><thead><tr><th>Parameter Name</th><th>Expected Meaning</th><th>Expected Value / Pattern</th><th>Actual Value</th><th>Source</th><th>Result</th><th>Reason</th></tr></thead><tbody>");
            foreach (ParameterCheckResult check in checks)
            {
                string resultLabel = ParameterResultLabel(check);
                html.Append("<tr>");
                html.Append("<td>" + Encode(check.ParameterName) + "</td>");
                html.Append("<td>" + Encode(check.ExpectedMeaning) + "</td>");
                html.Append("<td>" + Encode(check.ExpectedValuePattern) + "</td>");
                html.Append("<td>" + Encode(FormatParameterActualValue(check)) + "</td>");
                html.Append("<td>" + Encode(check.Source) + "</td>");
                html.Append("<td>" + Encode(resultLabel) + "</td>");
                html.Append("<td>" + Encode(BuildParameterRelevanceReason(check, result)) + "</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table></div>");
            return html.ToString();
        }

        private static string BuildKeyParametersConsidered(RequirementCheckResult result, int maxVisible)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"parameter-summary\">");
            html.AppendLine("<span class=\"field-label\">Key Parameters Considered</span>");
            List<ParameterCheckResult> checks = BuildVisibleParameterChecks(result).Take(Math.Max(1, maxVisible)).ToList();
            if (checks.Count == 0)
            {
                html.AppendLine("<div class=\"empty-state\">No parameter checks were captured for this requirement. Model evidence is not sufficient to close this requirement from parameters alone.</div>");
            }
            else
            {
                html.AppendLine("<div class=\"parameter-list\">");
                foreach (ParameterCheckResult check in checks)
                {
                    string label = ParameterResultLabel(check);
                    string chipClass = ParameterResultCssClass(label);
                    html.AppendLine("<div class=\"parameter-row\">");
                    html.AppendLine("<div class=\"parameter-name\">" + Encode(check.ParameterName) + "</div>");
                    html.AppendLine("<div class=\"parameter-value\">" + Encode(FormatParameterActualValue(check)) + "</div>");
                    html.AppendLine("<div class=\"parameter-reason\">" + Encode(BuildParameterRelevanceReason(check, result)) + "</div>");
                    html.AppendLine("<div><span class=\"result-chip " + chipClass + "\">" + Encode(label) + "</span></div>");
                    html.AppendLine("</div>");
                }
                html.AppendLine("</div>");
            }
            html.AppendLine("</div>");
            return html.ToString();
        }

        private static List<ParameterCheckResult> BuildVisibleParameterChecks(RequirementCheckResult result)
        {
            if (result == null)
            {
                return new List<ParameterCheckResult>();
            }

            List<ParameterCheckResult> checks = result.ParameterChecks != null
                ? result.ParameterChecks.Where(check => check != null).ToList()
                : new List<ParameterCheckResult>();

            foreach (string example in result.ParameterValueExamples ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(example) || !example.Contains("="))
                {
                    continue;
                }

                string[] parts = example.Split(new[] { '=' }, 2);
                string name = parts[0].Trim();
                if (checks.Any(check => string.Equals(check.ParameterName, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string value = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                checks.Add(new ParameterCheckResult
                {
                    ParameterName = name,
                    ActualValue = value,
                    Source = "parameter value example",
                    IsPresent = true,
                    IsEmpty = string.IsNullOrWhiteSpace(value) || IsUnavailableParameterValue(value),
                    IsMatch = !string.IsNullOrWhiteSpace(value) && !IsUnavailableParameterValue(value),
                    ExpectedMeaning = "Exported parameter evidence"
                });
            }

            foreach (string missing in result.MissingExpectedParameters ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(missing) ||
                    checks.Any(check => string.Equals(check.ParameterName, missing, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                checks.Add(new ParameterCheckResult
                {
                    ParameterName = missing,
                    ActualValue = string.Empty,
                    Source = "expected parameter",
                    IsPresent = false,
                    IsEmpty = true,
                    IsMatch = false,
                    IsRequired = true,
                    ExpectedMeaning = "Expected evidence for this requirement",
                    FailureReason = "Not available in current export."
                });
            }

            foreach (MissingEvidenceDetail missing in result.MissingEvidenceDetails ?? new List<MissingEvidenceDetail>())
            {
                if (missing == null || string.IsNullOrWhiteSpace(missing.ParameterName) ||
                    checks.Any(check => string.Equals(check.ParameterName, missing.ParameterName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                checks.Add(new ParameterCheckResult
                {
                    ParameterName = missing.ParameterName,
                    ActualValue = string.Empty,
                    Source = missing.Reason == MissingEvidenceReason.NotInExport ? "current export" : "model parameter",
                    IsPresent = missing.Reason != MissingEvidenceReason.NotInExport,
                    IsEmpty = missing.Reason == MissingEvidenceReason.EmptyValue,
                    IsMatch = false,
                    IsRequired = true,
                    ExpectedMeaning = missing.ReasonLabel,
                    FailureReason = missing.ReasonLabel
                });
            }

            return checks;
        }

        private static string ParameterResultLabel(ParameterCheckResult check)
        {
            if (check == null)
            {
                return "Unavailable";
            }

            if (check.IsMatch && !IsUnavailableParameterValue(check.ActualValue))
            {
                return "Pass";
            }

            if (!check.IsPresent || IsUnavailableParameterValue(check.ActualValue))
            {
                return "Unavailable";
            }

            if (check.IsEmpty)
            {
                return "Empty";
            }

            if (!string.IsNullOrWhiteSpace(check.FailureReason))
            {
                return check.IsRequired ? "Missing" : "Needs Review";
            }

            return "Fail";
        }

        private static string ParameterResultCssClass(string label)
        {
            if (string.Equals(label, "Pass", StringComparison.OrdinalIgnoreCase)) return "pass";
            if (string.Equals(label, "Missing", StringComparison.OrdinalIgnoreCase)) return "missing";
            if (string.Equals(label, "Empty", StringComparison.OrdinalIgnoreCase)) return "empty";
            if (string.Equals(label, "Fail", StringComparison.OrdinalIgnoreCase)) return "fail";
            if (string.Equals(label, "Unavailable", StringComparison.OrdinalIgnoreCase)) return "unavailable";
            return "needs-review";
        }

        private static string FormatParameterActualValue(ParameterCheckResult check)
        {
            if (check == null)
            {
                return "Not available in current export.";
            }

            if (!check.IsPresent || IsUnavailableParameterValue(check.ActualValue))
            {
                return "Not available in current export.";
            }

            if (string.IsNullOrWhiteSpace(check.ActualValue) || check.IsEmpty)
            {
                return "Present but not populated.";
            }

            return check.ActualValue;
        }

        private static string BuildParameterRelevanceReason(ParameterCheckResult check, RequirementCheckResult result)
        {
            string name = check == null ? string.Empty : check.ParameterName ?? string.Empty;
            string lower = name.ToLowerInvariant();
            string requirementText = result == null ? string.Empty : result.RequirementText ?? result.Requirement?.RequirementText ?? string.Empty;
            string text = requirementText.ToLowerInvariant();
            string suffix = ParameterEvidenceSuffix(check, result);

            if (lower.Contains("voltage"))
            {
                return "Voltage is considered because the requirement mentions 120V, power, receptacle, or electrical service. " + suffix;
            }
            if (lower.Contains("panel"))
            {
                return "Panel is considered because the requirement asks for circuit or power connection evidence. " + suffix;
            }
            if (lower.Contains("circuit"))
            {
                return "Circuit Number is considered because the requirement must be connected to a circuit. " + suffix;
            }
            if (lower.Contains("manufacturer") || lower.Contains("model") || lower.Contains("catalog"))
            {
                return "Manufacturer is considered because the requirement names acceptable manufacturers, products, models, or catalog evidence. " + suffix;
            }
            if (lower == "mark" || lower.Contains("type mark") || lower.Contains("tag") || lower.Contains("label"))
            {
                return "Mark / Type Mark is considered because the requirement asks for identification, labels, or equipment tags. " + suffix;
            }
            if (lower.Contains("phase demolished") || lower.Contains("demolished") || text.Contains("demolition") || text.Contains("abandoned"))
            {
                return "Phase Demolished is considered because the requirement references demolition or abandoned devices. " + suffix;
            }
            if (lower.Contains("ground"))
            {
                return "Ground Wire Size is considered because the requirement mentions grounding conductors or bonding evidence. " + suffix;
            }
            if (lower.Contains("family") || lower.Contains("type"))
            {
                return "Family / Type is considered because the requirement references a specific device type such as hose bibb, RPZ, receptacle, or data rack. " + suffix;
            }
            if (lower.Contains("level"))
            {
                return "Level is considered only when the requirement is about location, mounting, placement, or when it supports the element context. " + suffix;
            }

            return (string.IsNullOrWhiteSpace(check?.ExpectedMeaning)
                ? "This parameter is considered because it was listed as expected evidence for the deterministic rule."
                : check.ExpectedMeaning) + " " + suffix;
        }

        private static string ParameterEvidenceSuffix(ParameterCheckResult check, RequirementCheckResult result)
        {
            if (check == null || !check.IsPresent || IsUnavailableParameterValue(check.ActualValue))
            {
                return "Not available in current export. Model evidence is not sufficient to close this requirement.";
            }

            if (check.IsEmpty || string.IsNullOrWhiteSpace(check.ActualValue))
            {
                return "Present but not populated. Model evidence is not sufficient to close this requirement.";
            }

            if (check.IsMatch && !IsUnavailableParameterValue(check.ActualValue))
            {
                string value = check.ActualValue;
                return check.ParameterName + " = " + value + ". This supports the decision evidence, but it does not certify compliance beyond the first-pass model review.";
            }

            return "The captured value does not satisfy the expected pattern. Model evidence is not sufficient to close this requirement.";
        }

        private static bool IsUnavailableParameterValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            return string.Equals(trimmed, "undefined", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "(null)", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "<null>", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "n/a", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFamilyTypeSummaryBlock(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "<span style=\"color:var(--muted);font-size:12px;\">No family/type summary captured.</span>";
            }

            List<string> summaries = result.MatchedFamilyTypeSummary ?? new List<string>();
            if (summaries.Count == 0)
            {
                if ((result.MatchedFamilies ?? new List<string>()).Count == 0 && (result.MatchedTypes ?? new List<string>()).Count == 0)
                {
                    return "<span style=\"color:var(--muted);font-size:12px;\">Family/type data was not available in the current export.</span>";
                }

                summaries = new List<string>
                {
                    string.Join(", ", result.MatchedFamilies ?? new List<string>()),
                    string.Join(", ", result.MatchedTypes ?? new List<string>())
                }.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
            }

            return "<ul class=\"evidence-list\">" + string.Join(string.Empty, summaries.Take(10).Select(item => "<li>" + Encode(item) + "</li>")) + "</ul>";
        }

        private static string BuildMissingEvidenceBlock(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "<span style=\"color:var(--muted);font-size:12px;\">No missing evidence captured.</span>";
            }

            List<MissingEvidenceDetail> missing = result.MissingEvidenceDetails ?? new List<MissingEvidenceDetail>();
            List<string> limitations = new List<string>();
            if (!string.IsNullOrWhiteSpace(result.ModelEvidenceLimitations))
            {
                limitations.Add(result.ModelEvidenceLimitations);
            }

            if (missing.Count == 0 && limitations.Count == 0)
            {
                return "<span style=\"color:var(--muted);font-size:12px;\">No missing evidence was identified in this pass.</span>";
            }

            StringBuilder html = new StringBuilder();
            if (limitations.Count > 0)
            {
                html.Append("<div style=\"margin-bottom:6px;color:var(--text-secondary);font-size:12px;\">" + Encode(string.Join(" ", limitations)) + "</div>");
            }

            if (missing.Count > 0)
            {
                html.Append("<ul class=\"evidence-list\">");
                foreach (MissingEvidenceDetail detail in missing.Take(8))
                {
                    html.Append("<li><strong>" + Encode(detail.ParameterName) + ":</strong> " + Encode(detail.ReasonLabel) + "</li>");
                }
                html.Append("</ul>");
            }

            return html.ToString();
        }

        private static string BuildStatusReasonDisplay(RequirementCheckResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(result.StatusReason))
            {
                return result.StatusReason;
            }

            return string.IsNullOrWhiteSpace(result.Reasoning) ? string.Empty : result.Reasoning;
        }

        private static string BuildConfidenceReasonDisplay(RequirementCheckResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(result.ConfidenceReason))
            {
                return result.ConfidenceReason;
            }

            return string.IsNullOrWhiteSpace(result.EvidenceAlignmentReason) ? string.Empty : result.EvidenceAlignmentReason;
        }

        private static void AddInlineTraceChip(StringBuilder html, string label, string value)
        {
            html.Append("<div class=\"chip\"><span class=\"label\">" + Encode(label) + "</span><span class=\"value\">" + Encode(value) + "</span></div>");
        }

        private static string BuildParameterExamplesBlock(RequirementCheckResult result)
        {
            if (result == null) return "(none)";

            List<string> examples = result.ParameterValueExamples ?? new List<string>();
            List<MissingEvidenceDetail> missing = result.MissingEvidenceDetails ?? new List<MissingEvidenceDetail>();

            if (examples.Count == 0 && missing.Count == 0)
            {
                return "<span style=\"color:var(--muted);font-size:12px;\">No parameter data captured for this requirement.</span>";
            }

            StringBuilder html = new StringBuilder();

            if (examples.Count > 0)
            {
                html.Append("<div style=\"margin-bottom:6px;\"><strong style=\"font-size:12px;\">Sample values found:</strong></div>");
                html.Append("<ul style=\"margin:0;padding-left:18px;font-size:12px;\">");
                foreach (string example in examples.Take(5))
                {
                    html.Append("<li><code style=\"background:var(--bg-secondary);padding:1px 4px;border-radius:3px;\">" + Encode(example) + "</code></li>");
                }
                html.Append("</ul>");
            }

            if (missing.Count > 0)
            {
                html.Append("<div style=\"margin-top:6px;margin-bottom:4px;\"><strong style=\"font-size:12px;color:var(--insufficient);\">Missing evidence:</strong></div>");
                html.Append("<ul style=\"margin:0;padding-left:18px;font-size:12px;\">");
                foreach (var detail in missing.Take(5))
                {
                    html.Append("<li>" + Encode(detail.ParameterName) + " — <em>" + Encode(detail.ReasonLabel) + "</em></li>");
                }
                html.Append("</ul>");
            }

            return html.ToString();
        }

        private static string BuildEvidenceSummaryBlock(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "No evidence captured.";
            }

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(result.EvidenceSummary))
            {
                parts.Add(Encode(result.EvidenceSummary));
            }

            string evidenceList = BuildEvidenceListHtml(result.Evidence);
            if (!string.IsNullOrWhiteSpace(evidenceList))
            {
                parts.Add(evidenceList);
            }

            return parts.Count == 0 ? "No evidence captured." : string.Join("<br/>", parts);
        }

        private static string BuildEvidenceTraceabilityBlock(RequirementCheckResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            List<string> sampleIds = result.MatchedElementIds == null
                ? new List<string>()
                : result.MatchedElementIds.Take(10).Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
            int totalIdCount = result.MatchedElementIds == null ? 0 : result.MatchedElementIds.Count;
            List<string> sampleUniqueIds = result.MatchedUniqueIds == null
                ? new List<string>()
                : result.MatchedUniqueIds.Take(5).ToList();
            int totalUniqueIdCount = result.MatchedUniqueIds == null ? 0 : result.MatchedUniqueIds.Count;
            bool copyTextTruncated;
            string elementIdCopyText = BuildCappedElementIdCopyText(result, out copyTextTruncated);
            string copyButtonLabel = copyTextTruncated
                ? "Copy Revit Element IDs (first " + EvidenceEmbedLimits.MaxCopyElementIds.ToString(CultureInfo.InvariantCulture) + " of " + totalIdCount.ToString(CultureInfo.InvariantCulture) + ")"
                : "Copy Revit Element IDs";
            bool broadMatch = IsBroadCategoryMatch(result);
            string previewIds = sampleIds.Count > 0 ? string.Join("; ", sampleIds.Take(5)) : "(none)";
            if (totalIdCount > 5)
            {
                previewIds += " ... +" + (totalIdCount - 5).ToString(CultureInfo.InvariantCulture) + " more";
            }

            StringBuilder html = new StringBuilder();
            html.AppendLine("<div class=\"field element-id-box traceability-block\" style=\"grid-column:1 / -1;\">");
            html.AppendLine("<span class=\"field-label\">Evidence Traceability</span>");

            html.AppendLine("<div style=\"display:flex;flex-wrap:wrap;gap:12px;align-items:center;margin-top:8px;\">");
            html.AppendLine("<span style=\"font-size:13px;color:var(--text-secondary);\"><strong>" + result.MatchedModelElementCount.ToString(CultureInfo.InvariantCulture) + "</strong> matched elements</span>");
            html.AppendLine("<span class=\"id-preview\" title=\"Preview of first Element IDs\">" + Encode(previewIds) + "</span>");
            if (!string.IsNullOrWhiteSpace(elementIdCopyText))
            {
                html.AppendLine("<button type=\"button\" class=\"copy-ids\" data-copy-element-ids=\"" + Encode(elementIdCopyText) + "\">" + Encode(copyButtonLabel) + "</button>");
            }
            html.AppendLine("</div>");
            html.AppendLine("<div style=\"margin-top:6px;color:var(--muted);font-size:12px;\">Paste into Revit &#8594; Select Elements by ID.</div>");

            if (broadMatch)
            {
                html.AppendLine("<div class=\"scope-note\">Broad category match: " + totalIdCount.ToString("N0", CultureInfo.InvariantCulture) + " elements share this category/keyword scope. Treat this list as supporting context for review &#8212; it is not item-specific accepted evidence.</div>");
            }

            html.AppendLine("<details class=\"traceability\">");
            html.AppendLine("<summary>View Evidence Summary</summary>");
            html.AppendLine("<div class=\"detail-grid\" style=\"margin-top:10px;\">");
            AddField(html, "Matched Elements", result.MatchedModelElementCount > 0 ? result.MatchedModelElementCount.ToString(CultureInfo.InvariantCulture) : "0");
            AddField(html, "Matched Categories", FormatList(result.MatchedCategories));
            AddField(html, "Matched Families / Types", FormatList(result.MatchedFamilies) + (result.MatchedTypes.Count > 0 ? " | " + FormatList(result.MatchedTypes) : string.Empty));
            AddField(html, "Matched Parameters", FormatList(result.MatchedParameters));
            AddField(html, "Missing Parameters", FormatList(result.MissingEvidence));
            AddField(html, "Evidence Location", result.Status == RequirementCheckStatus.NeedsHumanReview ? "specification_or_manual_review" : "revit_model_elements");
            html.AppendLine("</div>");
            html.AppendLine("</details>");

            if (sampleIds.Count > 0)
            {
                string elementIdListLabel = totalIdCount > EvidenceEmbedLimits.MaxElementIdsInHtml
                    ? "View Revit Element IDs (first " + EvidenceEmbedLimits.MaxElementIdsInHtml.ToString(CultureInfo.InvariantCulture) + " of " + totalIdCount.ToString(CultureInfo.InvariantCulture) + ")"
                    : "View All Revit Element IDs (" + totalIdCount.ToString(CultureInfo.InvariantCulture) + ")";
                html.AppendLine("<details class=\"traceability\" style=\"margin-top:10px;\">");
                html.AppendLine("<summary>" + Encode(elementIdListLabel) + "</summary>");
                html.AppendLine("<div style=\"margin-top:8px;color:var(--muted);font-size:12px;\">Paste these IDs into Revit's Select Elements by ID dialog.</div>");
                if (!string.IsNullOrWhiteSpace(elementIdCopyText))
                {
                    html.AppendLine("<button type=\"button\" class=\"copy-ids\" style=\"margin-top:8px;\" data-copy-element-ids=\"" + Encode(elementIdCopyText) + "\">" + Encode(copyButtonLabel) + "</button>");
                }
                html.AppendLine("<div class=\"element-id-scroll\">" + Encode(BuildCappedIdListText(result.MatchedElementIds.Select(id => id.ToString(CultureInfo.InvariantCulture)), totalIdCount, EvidenceEmbedLimits.MaxElementIdsInHtml)) + "</div>");
                html.AppendLine("</details>");
            }

            if (sampleUniqueIds.Count > 0)
            {
                string uniqueIdListLabel = totalUniqueIdCount > EvidenceEmbedLimits.MaxUniqueIdsInHtml
                    ? "View Unique IDs (first " + EvidenceEmbedLimits.MaxUniqueIdsInHtml.ToString(CultureInfo.InvariantCulture) + " of " + totalUniqueIdCount.ToString(CultureInfo.InvariantCulture) + ")"
                    : "View Unique IDs (" + totalUniqueIdCount.ToString(CultureInfo.InvariantCulture) + ")";
                html.AppendLine("<details class=\"traceability\" style=\"margin-top:8px;\">");
                html.AppendLine("<summary>" + Encode(uniqueIdListLabel) + "</summary>");
                html.AppendLine("<div class=\"element-id-scroll\">" + Encode(BuildCappedIdListText(result.MatchedUniqueIds, totalUniqueIdCount, EvidenceEmbedLimits.MaxUniqueIdsInHtml)) + "</div>");
                html.AppendLine("</details>");
            }

            if (result.MatchedElements != null && result.MatchedElements.Count > 0)
            {
                html.AppendLine("<details class=\"traceability no-print\" style=\"margin-top:8px;\">");
                html.AppendLine("<summary>View Matched Elements (" + result.MatchedElements.Count.ToString(CultureInfo.InvariantCulture) + ")</summary>");
                html.AppendLine("<div class=\"matched-elements-scroll\" style=\"margin-top:10px;\">");
                html.AppendLine("<table>");
                html.AppendLine("<thead><tr><th>Element ID</th><th>Unique ID</th><th>Category</th><th>Family</th><th>Type</th><th>Level</th><th>Room/Space</th><th>Matched Parameters</th><th>Missing Parameters</th><th>Evidence Reason</th></tr></thead>");
                html.AppendLine("<tbody>");
                foreach (MatchedElementEvidence element in result.MatchedElements.Take(50))
                {
                    html.AppendLine("<tr>");
                    html.AppendLine("<td>" + Encode(element.ElementId) + "</td>");
                    html.AppendLine("<td style=\"font-size:11px;word-break:break-all;\">" + Encode(element.UniqueId) + "</td>");
                    html.AppendLine("<td>" + Encode(element.Category) + "</td>");
                    html.AppendLine("<td>" + Encode(element.Family) + "</td>");
                    html.AppendLine("<td>" + Encode(element.Type) + "</td>");
                    html.AppendLine("<td>" + Encode(element.Level) + "</td>");
                    html.AppendLine("<td>" + Encode(GetRoomOrSpace(element)) + "</td>");
                    html.AppendLine("<td>" + Encode(FormatList(element.MatchedParameters)) + "</td>");
                    html.AppendLine("<td>" + Encode(FormatList(element.MissingParameters)) + "</td>");
                    html.AppendLine("<td>" + Encode(element.EvidenceReason) + "</td>");
                    html.AppendLine("</tr>");
                }
                if (result.MatchedElements.Count > 50)
                {
                    html.AppendLine("<tr><td colspan=\"10\" style=\"color:var(--text-muted);font-style:italic;\">Showing 50 of " + result.MatchedElements.Count.ToString(CultureInfo.InvariantCulture) + " elements. The full set stays in the check session; rerun a scoped check to narrow this list.</td></tr>");
                }
                html.AppendLine("</tbody></table></div></details>");
            }
            html.AppendLine("</div>");
            return html.ToString();
        }

        /// <summary>
        /// A match is treated as a broad category/keyword sweep when the deterministic
        /// scope flags say so, or when the matched-element count alone makes an
        /// item-specific evidence claim implausible.
        /// </summary>
        private static bool IsBroadCategoryMatch(RequirementCheckResult result)
        {
            if (result == null)
            {
                return false;
            }

            int matchedIdCount = result.MatchedElementIds == null ? 0 : result.MatchedElementIds.Count;
            return result.FullModelFallbackUsed
                || !result.CandidateScopeValid
                || matchedIdCount >= EvidenceEmbedLimits.BroadMatchElementThreshold;
        }

        private static string BuildCappedElementIdCopyText(RequirementCheckResult result, out bool truncated)
        {
            truncated = false;
            if (result == null)
            {
                return string.Empty;
            }

            if (result.MatchedElementIds != null && result.MatchedElementIds.Count > 0)
            {
                truncated = result.MatchedElementIds.Count > EvidenceEmbedLimits.MaxCopyElementIds;
                return string.Join(";", result.MatchedElementIds
                    .Take(EvidenceEmbedLimits.MaxCopyElementIds)
                    .Select(id => id.ToString(CultureInfo.InvariantCulture)));
            }

            string provided = result.ElementIdCopyText;
            if (string.IsNullOrWhiteSpace(provided))
            {
                return string.Empty;
            }

            string[] parts = provided.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            truncated = parts.Length > EvidenceEmbedLimits.MaxCopyElementIds;
            return truncated
                ? string.Join(";", parts.Take(EvidenceEmbedLimits.MaxCopyElementIds))
                : provided;
        }

        private static string BuildJsonElementIdCopyText(RequirementCheckResult result)
        {
            bool truncated;
            return BuildCappedElementIdCopyText(result, out truncated);
        }

        private static string BuildCappedIdListText(IEnumerable<string> ids, int totalCount, int maxCount)
        {
            string joined = string.Join("; ", (ids ?? Enumerable.Empty<string>()).Take(maxCount));
            if (totalCount > maxCount)
            {
                joined += " ... +" + (totalCount - maxCount).ToString(CultureInfo.InvariantCulture) + " more (capped to keep this report openable)";
            }

            return joined;
        }

        private static string GetRoomOrSpace(MatchedElementEvidence element)
        {
            if (element == null || element.ParameterValues == null)
            {
                return "(not captured)";
            }

            foreach (string key in new[] { "Room", "Space", "Room Name", "Space Name", "Room/Space" })
            {
                string value;
                if (element.ParameterValues.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return "(not captured)";
        }

        private static string BuildEvidenceListHtml(IReadOnlyCollection<string> evidence)
        {
            if (evidence == null || evidence.Count == 0)
            {
                return string.Empty;
            }

            return "<ul class=\"evidence-list\">" + string.Join(string.Empty, evidence.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => "<li>" + Encode(item) + "</li>")) + "</ul>";
        }

        private static string ResultPillClass(RequirementCheckStatus status)
        {
            return StatusCssClass(status);
        }

        private static string IssuePillClass(RequirementCheckStatus status)
        {
            return StatusCssClass(status);
        }

        private static string DefaultUrgency(RequirementCheckStatus status)
        {
            switch (status)
            {
                case RequirementCheckStatus.NotMet:
                    return "Critical";
                case RequirementCheckStatus.InsufficientModelData:
                    return "High";
                case RequirementCheckStatus.NeedsHumanReview:
                    return "Needs Review";
                case RequirementCheckStatus.NotApplicable:
                    return "Low";
                default:
                    return "Low";
            }
        }

        private static string StatusLabel(RequirementCheckStatus status)
        {
            switch (status)
            {
                case RequirementCheckStatus.Met:
                    return "Met";
                case RequirementCheckStatus.NotMet:
                    return "Not Met";
                case RequirementCheckStatus.NeedsHumanReview:
                    return "Needs Human Review";
                case RequirementCheckStatus.InsufficientModelData:
                    return "Insufficient Model Data";
                case RequirementCheckStatus.NotApplicable:
                    return "Not Applicable";
                default:
                    return status.ToString();
            }
        }

        private static string StatusCssClass(RequirementCheckStatus status)
        {
            switch (status)
            {
                case RequirementCheckStatus.Met:
                    return "status-met";
                case RequirementCheckStatus.NotMet:
                    return "status-not-met";
                case RequirementCheckStatus.NeedsHumanReview:
                    return "status-needs-review";
                case RequirementCheckStatus.InsufficientModelData:
                    return "status-insufficient-data";
                case RequirementCheckStatus.NotApplicable:
                    return "status-not-applicable";
                default:
                    return "status-not-applicable";
            }
        }

        private static string NormalizeStatusClassName(string cssClass)
        {
            if (string.IsNullOrWhiteSpace(cssClass))
            {
                return "status-not-applicable";
            }

            string normalized = cssClass.Trim().ToLowerInvariant();
            if (normalized.StartsWith("status-", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            switch (normalized)
            {
                case "met":
                    return "status-met";
                case "notmet":
                    return "status-not-met";
                case "review":
                    return "status-needs-review";
                case "bad":
                    return "status-insufficient-data";
                case "na":
                    return "status-not-applicable";
                default:
                    return "status-not-applicable";
            }
        }

        private static string NormalizeLegendDotClass(string cssClass)
        {
            if (string.IsNullOrWhiteSpace(cssClass))
            {
                return "na";
            }

            string normalized = cssClass.Trim().ToLowerInvariant();
            if (normalized.StartsWith("status-", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("status-".Length);
            }

            switch (normalized)
            {
                case "met":
                case "notmet":
                case "review":
                case "bad":
                case "na":
                    return normalized;
                case "not-met":
                    return "notmet";
                case "needs-review":
                    return "review";
                case "insufficient-data":
                    return "bad";
                case "not-applicable":
                    return "na";
                default:
                    return "na";
            }
        }

        private static string ResolveSummaryCardClass(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return "status-not-applicable";
            }

            switch (label.Trim().ToLowerInvariant())
            {
                case "met":
                    return "status-met";
                case "not met":
                    return "status-not-met";
                case "needs human review":
                    return "status-needs-review";
                case "insufficient model data":
                    return "status-insufficient-data";
                case "not applicable":
                    return "status-not-applicable";
                case "key issues":
                    return "status-not-met";
                case "overall score":
                case "readiness score":
                case "discipline score":
                case "total requirements":
                case "applicable requirements":
                case "discipline requirements":
                case "disciplines impacted":
                case "top next actions":
                    return "status-not-applicable";
                default:
                    return "status-not-applicable";
            }
        }

        private static string UrgencyCssClass(string urgency)
        {
            if (string.IsNullOrWhiteSpace(urgency))
            {
                return "urgency-low";
            }

            switch (urgency.Trim().ToLowerInvariant())
            {
                case "critical":
                    return "urgency-critical";
                case "high":
                    return "urgency-high";
                case "medium":
                    return "urgency-medium";
                case "low":
                    return "urgency-low";
                case "needs review":
                case "needs-human-review":
                    return "urgency-needs-review";
                default:
                    return "urgency-low";
            }
        }

        private static string BuildRequirementAnchor(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "req-unknown";
            }

            string identifier = !string.IsNullOrWhiteSpace(result.RequirementId)
                ? result.RequirementId
                : result.Requirement != null && !string.IsNullOrWhiteSpace(result.Requirement.RequirementId)
                    ? result.Requirement.RequirementId
                    : result.SourceRow > 0
                        ? "row-" + result.SourceRow.ToString(CultureInfo.InvariantCulture)
                        : "unknown";

            return "req-" + Slugify(identifier);
        }

        private static string BuildDisciplineAnchor(string discipline)
        {
            string value = string.IsNullOrWhiteSpace(discipline) ? "unknown" : discipline;
            if (string.Equals(value, "Unknown / Needs Classification", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "discipline-unknown";
            }

            return "discipline-" + Slugify(value);
        }

        private static string DisciplineColorKey(string discipline)
        {
            string value = string.IsNullOrWhiteSpace(discipline) ? "unknown" : discipline;
            if (string.Equals(value, "Electrical", StringComparison.OrdinalIgnoreCase))
            {
                return "electrical";
            }

            if (string.Equals(value, "Lighting", StringComparison.OrdinalIgnoreCase))
            {
                return "lighting";
            }

            if (string.Equals(value, "Mechanical", StringComparison.OrdinalIgnoreCase))
            {
                return "mechanical";
            }

            if (string.Equals(value, "Plumbing", StringComparison.OrdinalIgnoreCase))
            {
                return "plumbing";
            }

            if (string.Equals(value, "Technology", StringComparison.OrdinalIgnoreCase))
            {
                return "technology";
            }

            if (string.Equals(value, "Unknown / Needs Classification", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "unknown";
            }

            return "general";
        }

        private static string DisciplineCssClass(string discipline)
        {
            return "discipline-" + DisciplineColorKey(discipline);
        }

        private static string DisciplineBadgeClass(string discipline)
        {
            return "discipline-badge-" + DisciplineColorKey(discipline);
        }

        private static string DisciplineSectionClass(string discipline)
        {
            return "discipline-section-" + DisciplineColorKey(discipline);
        }

        private static string DisciplineCardClass(string discipline)
        {
            return "discipline-card-" + DisciplineColorKey(discipline);
        }

        private static string DisciplinePrimaryColor(string discipline)
        {
            switch (DisciplineColorKey(discipline))
            {
                case "electrical":
                    return "#2563EB";
                case "lighting":
                    return "#D97706";
                case "mechanical":
                    return "#7C3AED";
                case "plumbing":
                    return "#0891B2";
                case "technology":
                    return "#4F46E5";
                case "unknown":
                    return "#64748B";
                default:
                    return "#0F766E";
            }
        }

        private static string DisciplineBackgroundColor(string discipline)
        {
            switch (DisciplineColorKey(discipline))
            {
                case "electrical":
                    return "#EFF6FF";
                case "lighting":
                    return "#FFFBEB";
                case "mechanical":
                    return "#F5F3FF";
                case "plumbing":
                    return "#ECFEFF";
                case "technology":
                    return "#EEF2FF";
                case "unknown":
                    return "#F8FAFC";
                default:
                    return "#F0FDFA";
            }
        }

        private static string DisciplineBorderColor(string discipline)
        {
            switch (DisciplineColorKey(discipline))
            {
                case "electrical":
                    return "#1D4ED8";
                case "lighting":
                    return "#F59E0B";
                case "mechanical":
                    return "#6D28D9";
                case "plumbing":
                    return "#0E7490";
                case "technology":
                    return "#4338CA";
                case "unknown":
                    return "#475569";
                default:
                    return "#0D9488";
            }
        }

        private static string DisciplineTextColor(string discipline)
        {
            switch (DisciplineColorKey(discipline))
            {
                case "electrical":
                    return "#1E3A8A";
                case "lighting":
                    return "#92400E";
                case "mechanical":
                    return "#4C1D95";
                case "plumbing":
                    return "#164E63";
                case "technology":
                    return "#312E81";
                case "unknown":
                    return "#334155";
                default:
                    return "#134E4A";
            }
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            StringBuilder slug = new StringBuilder(value.Length);
            bool previousDash = false;
            foreach (char character in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    slug.Append(character);
                    previousDash = false;
                }
                else if (!previousDash)
                {
                    slug.Append('-');
                    previousDash = true;
                }
            }

            string normalized = slug.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
        }

        private static string FormatList(IEnumerable<string> values)
        {
            if (values == null)
            {
                return "(not captured)";
            }

            List<string> list = values.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
            return list.Count == 0 ? "(not captured)" : string.Join(", ", list);
        }

        private static void AddChip(StringBuilder html, string label, string value)
        {
            html.AppendLine("<div class=\"chip\"><span class=\"label\">" + Encode(label) + "</span><span class=\"value\">" + Encode(value) + "</span></div>");
        }

        private static void AddIdentityCard(StringBuilder html, string label, string value)
        {
            html.AppendLine("<div class=\"identity-card\"><span class=\"label\">" + Encode(label) + "</span><span class=\"value\">" + Encode(value) + "</span></div>");
        }

        private static void AddMetric(StringBuilder html, string label, string value, string detail, string cssClass)
        {
            string statusClass = NormalizeStatusClassName(cssClass);
            html.AppendLine("<div class=\"metric " + Encode(cssClass) + " " + statusClass + "\" data-metric=\"" + Encode(label) + "\" data-css=\"" + Encode(statusClass) + "\"><div class=\"label\">" + Encode(label) + "</div><div class=\"value\">" + Encode(value) + "</div><div class=\"detail\">" + Encode(detail) + "</div></div>");
        }

        private static void AddSmallCard(StringBuilder html, string label, string value)
        {
            string cssClass = ResolveSummaryCardClass(label);
            html.AppendLine("<div class=\"small-card " + cssClass + "\"><span class=\"k\">" + Encode(label) + "</span><span class=\"v\">" + Encode(value) + "</span></div>");
        }

        private static void AddSmallCard(StringBuilder html, string label, string value, string cssClass)
        {
            string resolved = NormalizeStatusClassName(cssClass);
            html.AppendLine("<div class=\"small-card " + resolved + "\"><span class=\"k\">" + Encode(label) + "</span><span class=\"v\">" + Encode(value) + "</span></div>");
        }

        private static void AddField(StringBuilder html, string label, string value)
        {
            html.AppendLine("<div class=\"field\"><span class=\"field-label\">" + Encode(label) + "</span><div class=\"value\">" + Encode(value) + "</div></div>");
        }

        private static void AddField(StringBuilder html, string label, string value, string blockClass)
        {
            string cssClass = string.IsNullOrWhiteSpace(blockClass) ? "field" : "field " + blockClass;
            html.AppendLine("<div class=\"" + cssClass + "\"><span class=\"field-label\">" + Encode(label) + "</span><div class=\"value\">" + Encode(value) + "</div></div>");
        }

        private static void AddFieldHtml(StringBuilder html, string label, string valueHtml)
        {
            html.AppendLine("<div class=\"field\"><span class=\"field-label\">" + Encode(label) + "</span><div class=\"value\">" + (string.IsNullOrWhiteSpace(valueHtml) ? string.Empty : valueHtml) + "</div></div>");
        }

        private static void AddFieldHtml(StringBuilder html, string label, string valueHtml, string blockClass)
        {
            string cssClass = string.IsNullOrWhiteSpace(blockClass) ? "field" : "field " + blockClass;
            html.AppendLine("<div class=\"" + cssClass + "\"><span class=\"field-label\">" + Encode(label) + "</span><div class=\"value\">" + (string.IsNullOrWhiteSpace(valueHtml) ? string.Empty : valueHtml) + "</div></div>");
        }

        private static void AddLegendItem(StringBuilder html, string label, string description, string cssClass, string extraClass = null)
        {
            string statusClass = NormalizeStatusClassName(cssClass);
            string dotClass = NormalizeLegendDotClass(cssClass);
            string classes = "legend-item " + statusClass + (string.IsNullOrWhiteSpace(extraClass) ? string.Empty : " " + extraClass);
            html.AppendLine("<div class=\"" + classes + "\"><div class=\"legend-pill\"><span class=\"dot " + dotClass + "\"></span>" + Encode(label) + "</div><div>" + Encode(description) + "</div></div>");
        }

        private static void AddUrgencyLegendItem(StringBuilder html, string label, string description, string urgencyClass)
        {
            html.AppendLine("<div class=\"legend-item urgency-legend-card " + urgencyClass + "\" style=\"border-left-color:var(--" + (urgencyClass.StartsWith("urgency-") ? urgencyClass.Substring("urgency-".Length) == "needs-review" ? "needs-review" : urgencyClass.Substring("urgency-".Length) : "low") + ");\"><div class=\"legend-pill\"><span class=\"dot\" style=\"background:var(--" + (urgencyClass == "urgency-critical" ? "critical" : urgencyClass == "urgency-high" ? "high" : urgencyClass == "urgency-medium" ? "medium" : urgencyClass == "urgency-low" ? "low" : "needs-review") + ");\"></span>" + Encode(label) + "</div><div>" + Encode(description) + "</div></div>");
        }

        private static void AddTraceabilityChip(StringBuilder html, string label, string value)
        {
            html.AppendLine("<div class=\"chip\"><span class=\"label\">" + Encode(label) + "</span><span class=\"value\">" + Encode(value) + "</span></div>");
        }

        private static int CountDistinctSourceFiles(IEnumerable<RequirementCheckResult> results)
        {
            if (results == null)
            {
                return 0;
            }

            return results
                .Select(result => !string.IsNullOrWhiteSpace(result.SourceFile) ? result.SourceFile : result.Requirement != null ? result.Requirement.SourceFile : string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static int ResultPriority(RequirementCheckResult result)
        {
            if (result == null)
            {
                return 0;
            }

            switch (result.Status)
            {
                case RequirementCheckStatus.NotMet:
                    return 4;
                case RequirementCheckStatus.InsufficientModelData:
                    return 3;
                case RequirementCheckStatus.NeedsHumanReview:
                    return 2;
                case RequirementCheckStatus.NotApplicable:
                    return 1;
                default:
                    return 0;
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Report";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
            }
            return builder.ToString();
        }

        private static string NormalizeUrgency(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Needs Review";
            }

            string trimmed = value.Trim();
            switch (trimmed.ToLowerInvariant())
            {
                case "critical":
                case "blocker":
                    return "Critical";
                case "high":
                    return "High";
                case "medium":
                case "moderate":
                    return "Medium";
                case "low":
                    return "Low";
                case "needs review":
                case "review":
                case "manual":
                case "human review":
                    return "Needs Review";
                case "info":
                case "unknown":
                    return "Low";
                default:
                    return "Low";
            }
        }

        private static string FormatSourceFileName(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return "(not set)";
            }

            try
            {
                return Path.GetFileName(fullPath);
            }
            catch
            {
                return fullPath;
            }
        }

        private static string BuildBuildWarningNote(string value)
        {
            return SafeText(value);
        }

        private static string CountArray(string[] items)
        {
            return items == null ? "0" : items.Length.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class ReportViewState
        {
            public RequirementCheckReport Report { get; set; }
            public List<RequirementCheckResult> AllResults { get; set; } = new List<RequirementCheckResult>();
            public List<RequirementCheckResult> VisibleResults { get; set; } = new List<RequirementCheckResult>();
            public List<KeyIssue> VisibleIssues { get; set; } = new List<KeyIssue>();
            public RequirementCheckSummary VisibleSummary { get; set; } = new RequirementCheckSummary();
            public double VisibleOverallScore { get; set; }
            public double VisibleReadinessScore { get; set; }
            public double VisibleDisciplineScore { get; set; }
            public string ActiveDiscipline { get; set; }
            public string ActiveStatus { get; set; }
            public string ActiveUrgency { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public string DocumentTitle { get; set; }
            public string ExportStem { get; set; }
            public List<string> SuggestedQuestions { get; set; } = new List<string>();
            public List<string> TopNextActions { get; set; } = new List<string>();
            public List<DisciplineSummary> DisciplineSummaries { get; set; } = new List<DisciplineSummary>();
            public int DisciplinesImpacted { get; set; }
            public int ExcludedCount { get; set; }
            public RequirementCoherenceReport Coherence { get; set; } = new RequirementCoherenceReport();
            public bool IsMaster => string.Equals(ActiveDiscipline, "All Disciplines", StringComparison.OrdinalIgnoreCase);
        }
    }
}
