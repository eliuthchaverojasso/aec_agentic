using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EMAExtractor.Requirements
{
    public enum RequirementCheckStatus
    {
        Met,
        NotMet,
        NeedsHumanReview,
        NotApplicable,
        InsufficientModelData
    }

    public enum EvidenceAlignmentLevel
    {
        Strong,
        Partial,
        Weak,
        MismatchRisk,
        ManualOnly
    }

    public enum MissingEvidenceReason
    {
        NotCaptured,
        EmptyValue,
        NotInExport
    }

    public class MissingEvidenceDetail
    {
        public string ParameterName { get; set; }
        public MissingEvidenceReason Reason { get; set; }

        public string ReasonLabel => Reason switch
        {
            MissingEvidenceReason.NotCaptured => "Parameter not captured in model export",
            MissingEvidenceReason.EmptyValue => "Parameter exists but has no value assigned",
            MissingEvidenceReason.NotInExport => "Parameter not included in export schema",
            _ => "Unknown"
        };
    }

    public class ParameterCheckResult
    {
        public string ParameterName { get; set; }
        public string ExpectedMeaning { get; set; }
        public string ExpectedValuePattern { get; set; }
        public string ActualValue { get; set; }
        public string Source { get; set; }
        public bool IsPresent { get; set; }
        public bool IsEmpty { get; set; }
        public bool IsMatch { get; set; }
        public bool IsRequired { get; set; }
        public string FailureReason { get; set; }
    }

    public class FilterStageTrace
    {
        public string StageName { get; set; }
        public string Description { get; set; }
        public int InputCount { get; set; }
        public int OutputCount { get; set; }
        public string Criteria { get; set; }
        public List<string> ExampleMatchedValues { get; set; } = new List<string>();
    }

    public class RequirementFilterTrace
    {
        public string DisciplineFilter { get; set; }
        public string ScopeFilter { get; set; }
        public string StatusFilter { get; set; }
        public string RequirementType { get; set; }
        public string RequirementTypeReason { get; set; }
        public string RequirementIntent { get; set; }
        public string ValidationType { get; set; }
        public string ValidationTypeReason { get; set; }
        public string RuleApplied { get; set; }
        public string RuleFamily { get; set; }
        public List<string> TriggerKeywords { get; set; } = new List<string>();
        public List<string> ExpectedEvidenceSources { get; set; } = new List<string>();
        public List<string> ExpectedCategories { get; set; } = new List<string>();
        public List<string> ExpectedFamilyTypeHints { get; set; } = new List<string>();
        public List<string> ExpectedParameters { get; set; } = new List<string>();
        public List<string> AllowedCategories { get; set; } = new List<string>();
        public List<string> ExcludedCategories { get; set; } = new List<string>();
        public List<string> DirectClosingEvidence { get; set; } = new List<string>();
        public List<string> SupportingContext { get; set; } = new List<string>();
        public List<string> MissingDirectEvidence { get; set; } = new List<string>();
        public string CandidateScopeReason { get; set; }
        public bool FallbackUsed { get; set; }
        public bool FallbackAllowed { get; set; }
        public string ModelEvidenceSufficiency { get; set; }
        public string WhyNotModelCloseable { get; set; }
        public bool CandidateScopeValid { get; set; } = true;
        public bool FullModelFallbackUsed { get; set; }
        public List<FilterStageTrace> CandidateStages { get; set; } = new List<FilterStageTrace>();
    }

    public class OwnerRequirementRow
    {
        public int RowNumber { get; set; }
        public string SourceFile { get; set; }
        public string SourceSheet { get; set; }
        public string Discipline { get; set; }
        public string RequirementId { get; set; }
        public string RequirementText { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public Dictionary<string, string> Columns { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class RequirementCheckResult
    {
        public OwnerRequirementRow Requirement { get; set; }
        public RequirementCheckStatus Status { get; set; }
        public double Confidence { get; set; }
        public List<string> Evidence { get; set; } = new List<string>();
        public string IssueTitle { get; set; }
        public string Reasoning { get; set; }
        public string NextBestAction { get; set; }
        public string ResponsibleRole { get; set; }
        public string EvidenceSummary { get; set; }
        public string RequirementId { get; set; }
        public string RequirementText { get; set; }
        public string Discipline { get; set; }
        public string SourceFile { get; set; }
        public string SourceWorksheet { get; set; }
        public int SourceRow { get; set; }
        public string RequirementType { get; set; }
        public string RequirementTypeReason { get; set; }
        public ValidationType ValidationType { get; set; }
        public List<TaxonomyLabel> TaxonomyLabels { get; set; } = new List<TaxonomyLabel>();
        public string ExpectedEvidence { get; set; }
        public double KeyIssueScore { get; set; }
        public string Urgency { get; set; }
        public bool IsKeyIssue { get; set; }
        public int MatchedModelElementCount { get; set; }
        public List<long> MatchedElementIds { get; set; } = new List<long>();
        public List<string> MatchedUniqueIds { get; set; } = new List<string>();
        public List<string> MatchedElementSummaries { get; set; } = new List<string>();
        public List<string> MatchedCategories { get; set; } = new List<string>();
        public List<string> MatchedFamilies { get; set; } = new List<string>();
        public List<string> MatchedTypes { get; set; } = new List<string>();
        public List<string> MatchedParameters { get; set; } = new List<string>();
        public List<string> MissingEvidence { get; set; } = new List<string>();
        public List<MatchedElementEvidence> MatchedElements { get; set; } = new List<MatchedElementEvidence>();
        public string ElementIdCopyText { get; set; }
        public RequirementFilterTrace FilterTrace { get; set; } = new RequirementFilterTrace();
        public List<ParameterCheckResult> ParameterChecks { get; set; } = new List<ParameterCheckResult>();
        public List<string> ExpectedEvidenceSources { get; set; } = new List<string>();
        public List<string> ExpectedCategories { get; set; } = new List<string>();
        public List<string> ExpectedFamilyTypeHints { get; set; } = new List<string>();
        public List<string> ExpectedParameters { get; set; } = new List<string>();
        public List<string> ActualMatchedCategories { get; set; } = new List<string>();
        public List<string> ActualMatchedParameters { get; set; } = new List<string>();
        public List<string> ActualParameterValueExamples { get; set; } = new List<string>();
        public List<string> MissingExpectedParameters { get; set; } = new List<string>();
        public List<string> MatchedFamilyTypeSummary { get; set; } = new List<string>();
        public string StatusReason { get; set; }
        public string ConfidenceReason { get; set; }
        public bool HumanReviewNeeded { get; set; }
        public string ModelEvidenceLimitations { get; set; }
        public string UrgencyReason { get; set; }
        public string KeyIssueScoreReason { get; set; }
        public double StatusSeverityScore { get; set; }
        public double DeliverableImpactScore { get; set; }
        public double ActionabilityScore { get; set; }
        public double EvidenceGapScore { get; set; }
        public double RequirementTypeRiskScore { get; set; }
        public double ImpactScaleScore { get; set; }
        public bool CandidateScopeValid { get; set; } = true;
        public bool FullModelFallbackUsed { get; set; }

        public EvidenceAlignmentLevel EvidenceAlignment { get; set; }
        public string EvidenceAlignmentReason { get; set; }
        public string RuleApplied { get; set; }
        public string RuleFamily { get; set; }
        public List<string> RuleTriggerKeywords { get; set; } = new List<string>();
        public string RuleExpectedEvidence { get; set; }
        public string ValidationTypeReason { get; set; }
        public List<string> AllowedCategories { get; set; } = new List<string>();
        public List<string> ExcludedCategories { get; set; } = new List<string>();
        public List<string> DirectClosingEvidence { get; set; } = new List<string>();
        public List<string> SupportingContext { get; set; } = new List<string>();
        public List<string> MissingDirectEvidence { get; set; } = new List<string>();
        public string CandidateScopeReason { get; set; }
        public bool FallbackUsed { get; set; }
        public bool FallbackAllowed { get; set; }
        public string ModelEvidenceSufficiency { get; set; }
        public string WhyNotModelCloseable { get; set; }
        public List<string> ParameterValueExamples { get; set; } = new List<string>();
        public List<MissingEvidenceDetail> MissingEvidenceDetails { get; set; } = new List<MissingEvidenceDetail>();

        public string EvidenceAlignmentLabel => EvidenceAlignment switch
        {
            EvidenceAlignmentLevel.Strong => "Strong",
            EvidenceAlignmentLevel.Partial => "Partial",
            EvidenceAlignmentLevel.Weak => "Weak",
            EvidenceAlignmentLevel.MismatchRisk => "Mismatch Risk",
            EvidenceAlignmentLevel.ManualOnly => "Manual Only",
            _ => "Unknown"
        };

        public string Reason
        {
            get => Reasoning;
            set => Reasoning = value;
        }

        public string SuggestedAction
        {
            get => NextBestAction;
            set => NextBestAction = value;
        }

        public string StatusLabel => Status switch
        {
            RequirementCheckStatus.Met => "Met",
            RequirementCheckStatus.NotMet => "Not Met",
            RequirementCheckStatus.NeedsHumanReview => "Needs Human Review",
            RequirementCheckStatus.NotApplicable => "Not Applicable",
            RequirementCheckStatus.InsufficientModelData => "Insufficient Model Data",
            _ => Status.ToString()
        };
    }

    public class RequirementCheckSummary
    {
        public int TotalRequirements { get; set; }
        public int MetCount { get; set; }
        public int NotMetCount { get; set; }
        public int NeedsHumanReviewCount { get; set; }
        public int NotApplicableCount { get; set; }
        public int InsufficientModelDataCount { get; set; }
        public int ConsideredCount => MetCount + NotMetCount + NeedsHumanReviewCount + InsufficientModelDataCount;
        public double MatchScore => ConsideredCount == 0
            ? 0
            : Math.Round((double)MetCount / ConsideredCount * 100.0, 1);

        public static RequirementCheckSummary FromResults(IReadOnlyCollection<RequirementCheckResult> results)
        {
            RequirementCheckSummary summary = new RequirementCheckSummary
            {
                TotalRequirements = results == null ? 0 : results.Count
            };

            if (results == null)
            {
                return summary;
            }

            foreach (RequirementCheckResult result in results)
            {
                if (result == null)
                {
                    continue;
                }

                switch (result.Status)
                {
                    case RequirementCheckStatus.Met:
                        summary.MetCount++;
                        break;
                    case RequirementCheckStatus.NotMet:
                        summary.NotMetCount++;
                        break;
                    case RequirementCheckStatus.NeedsHumanReview:
                        summary.NeedsHumanReviewCount++;
                        break;
                    case RequirementCheckStatus.NotApplicable:
                        summary.NotApplicableCount++;
                        break;
                    case RequirementCheckStatus.InsufficientModelData:
                        summary.InsufficientModelDataCount++;
                        break;
                }
            }

            return summary;
        }
    }

    public class DisciplineSummary
    {
        public string Discipline { get; set; }
        public int Total { get; set; }
        public int Applicable { get; set; }
        public int Met { get; set; }
        public int NotMet { get; set; }
        public int NeedsHumanReview { get; set; }
        public int InsufficientModelData { get; set; }
        public int NotApplicable { get; set; }
        public double DisciplineScore { get; set; }
        public int KeyIssueCount { get; set; }
        public string ResponsibleRole { get; set; }
        public string KeyIssues { get; set; }
        public List<string> TopNextActions { get; set; } = new List<string>();
    }

    public class MatchedElementEvidence
    {
        public string ElementId { get; set; }
        public string UniqueId { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string Level { get; set; }
        public List<string> MatchedParameters { get; set; } = new List<string>();
        public List<string> MissingParameters { get; set; } = new List<string>();
        public List<string> ParameterValueExamples { get; set; } = new List<string>();
        public List<ParameterCheckResult> ParameterChecks { get; set; } = new List<ParameterCheckResult>();
        public string EvidenceReason { get; set; }
        public Dictionary<string, string> ParameterValues { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class ReportFilterScores
    {
        public double OverallScore { get; set; }
        public double ReadinessScore { get; set; }
        public double DisciplineScore { get; set; }
        public int ApplicableCount { get; set; }
        public int TotalCount { get; set; }
        public int KeyIssueCount { get; set; }
    }

    public class ReportFilterContext
    {
        public string ActiveDiscipline { get; set; }
        public string ActiveStatus { get; set; }
        public string ActiveUrgency { get; set; }
        public string RequirementIntent { get; set; }
        public string ValidationType { get; set; }
        public string ValidationTypeReason { get; set; }
        public string RuleApplied { get; set; }
        public string RuleFamily { get; set; }
        public List<string> TriggerKeywords { get; set; } = new List<string>();
        public List<string> ExpectedEvidenceSources { get; set; } = new List<string>();
        public List<string> ExpectedCategories { get; set; } = new List<string>();
        public List<string> ExpectedFamilyTypeHints { get; set; } = new List<string>();
        public List<string> ExpectedParameters { get; set; } = new List<string>();
        public List<FilterStageTrace> CandidateStages { get; set; } = new List<FilterStageTrace>();
        public List<RequirementCheckResult> FilteredResults { get; set; } = new List<RequirementCheckResult>();
        public List<KeyIssue> FilteredKeyIssues { get; set; } = new List<KeyIssue>();
        public RequirementCheckSummary FilteredCounts { get; set; } = new RequirementCheckSummary();
        public ReportFilterScores FilteredScores { get; set; } = new ReportFilterScores();
        public List<string> SuggestedQuestions { get; set; } = new List<string>();
    }

    public class RequirementCheckReport
    {
        public string ProjectName { get; set; }
        public string ModelName { get; set; }
        public string RequirementsFileName { get; set; }
        public string RequirementsFilePath { get; set; }
        public RequirementDiscipline Discipline { get; set; }
        public RequirementModelScope Scope { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string OutputFolder { get; set; }
        public string ReportPath { get; set; }
        public string CaptureNote { get; set; }
        public int ModelElementCount { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<RequirementCheckResult> Results { get; set; } = new List<RequirementCheckResult>();
        public RequirementCheckSummary Summary { get; set; } = new RequirementCheckSummary();
        public double OverallScore { get; set; }
        public double ReadinessScore { get; set; }
        public ReadinessLabel ReadinessLabel { get; set; }
        public List<KeyIssue> KeyIssues { get; set; } = new List<KeyIssue>();
        public List<DisciplineSummary> DisciplineSummaries { get; set; } = new List<DisciplineSummary>();
        public ReportFilterContext FilterContext { get; set; } = new ReportFilterContext();
        public DateTime LastModelSyncTime { get; set; }

        public string BuildSummaryText()
        {
            return BuildClipboardSummary();
        }

        public string BuildClipboardSummary(ReportFilterContext filterContext = null, int topIssueCount = 5)
        {
            ReportFilterContext context = filterContext ?? FilterContext ?? new ReportFilterContext();
            List<RequirementCheckResult> sourceResults = context.FilteredResults != null && context.FilteredResults.Count > 0
                ? context.FilteredResults
                : Results ?? new List<RequirementCheckResult>();

            List<RequirementCheckResult> actionable = sourceResults
                .Where(result => result != null &&
                    result.Status != RequirementCheckStatus.Met &&
                    result.Status != RequirementCheckStatus.NotApplicable)
                .OrderByDescending(ResultPriority)
                .ThenByDescending(result => result.Confidence)
                .ThenBy(result => SafeLine(result.IssueTitle))
                .ToList();

            List<KeyIssue> issueSource = context.FilteredKeyIssues != null && context.FilteredKeyIssues.Count > 0
                ? context.FilteredKeyIssues
                : KeyIssues ?? new List<KeyIssue>();

            List<string> issueLines = new List<string>();
            List<string> actionLines = new List<string>();

            foreach (RequirementCheckResult result in actionable.Take(Math.Max(topIssueCount, 0)))
            {
                string requirementId = BuildRequirementReference(result);
                string issueTitle = string.IsNullOrWhiteSpace(result.IssueTitle)
                    ? SafeLine(result.RequirementText)
                    : result.IssueTitle;
                string nextAction = string.IsNullOrWhiteSpace(result.NextBestAction)
                    ? "Review manually."
                    : result.NextBestAction;

                issueLines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}. [{1}] {2} - {3}",
                    issueLines.Count + 1,
                    result.StatusLabel,
                    requirementId + " " + issueTitle,
                    nextAction));

                if (!string.IsNullOrWhiteSpace(nextAction) &&
                    !actionLines.Any(item => string.Equals(item, nextAction, StringComparison.OrdinalIgnoreCase)))
                {
                    actionLines.Add(nextAction);
                }
            }

            if (actionLines.Count == 0)
            {
                foreach (KeyIssue issue in issueSource.Take(Math.Max(topIssueCount, 0)))
                {
                    if (!string.IsNullOrWhiteSpace(issue.NextBestAction) &&
                        !actionLines.Any(item => string.Equals(item, issue.NextBestAction, StringComparison.OrdinalIgnoreCase)))
                    {
                        actionLines.Add(issue.NextBestAction);
                    }
                }
            }

            string activeFilter = FormatActiveFilter(context);
            RequirementCheckSummary counts = context.FilteredCounts ?? Summary ?? new RequirementCheckSummary();
            ReportFilterScores scores = context.FilteredScores ?? new ReportFilterScores
            {
                OverallScore = OverallScore,
                ReadinessScore = ReadinessScore,
                DisciplineScore = OverallScore,
                ApplicableCount = counts.ConsideredCount,
                TotalCount = counts.TotalRequirements,
                KeyIssueCount = issueSource.Count
            };

            List<string> lines = new List<string>
            {
                "EMA AI Owner Requirement Check",
                "Project: " + SafeLine(ProjectName),
                "Model: " + SafeLine(ModelName),
                "Requirements File: " + SafeLine(RequirementsFileName),
                "Discipline: " + SafeLine(context.ActiveDiscipline),
                "Active Filter: " + activeFilter,
                "Scope: " + FormatScope(Scope),
                "Generated: " + GeneratedAt.ToString("O"),
                "Evidence Review Score: " + Math.Round(scores.OverallScore, 1).ToString(CultureInfo.InvariantCulture),
                "Met: " + counts.MetCount,
                "Not Met: " + counts.NotMetCount,
                "Needs Human Review: " + counts.NeedsHumanReviewCount,
                "Insufficient Model Data: " + counts.InsufficientModelDataCount,
                "Not Applicable: " + counts.NotApplicableCount,
                "Model elements reviewed: " + ModelElementCount,
                string.IsNullOrWhiteSpace(CaptureNote) ? null : "Note: " + CaptureNote
            }.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

            if (!string.IsNullOrWhiteSpace(ReportPath))
            {
                lines.Add("Report: " + ReportPath);
            }

            lines.Add(string.Empty);
            lines.Add("Summary:");
            lines.Add("Top Issues:");

            if (issueLines.Count == 0)
            {
                lines.Add("1. No actionable issues were identified for this view.");
            }
            else
            {
                lines.AddRange(issueLines);
            }

            lines.Add(string.Empty);
            lines.Add("Top Next Actions:");

            if (actionLines.Count == 0)
            {
                lines.Add("1. No next action identified.");
            }
            else
            {
                for (int index = 0; index < actionLines.Count; index++)
                {
                    lines.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}. {1}",
                        index + 1,
                        actionLines[index]));
                }
            }

            lines.Add(string.Empty);
            lines.Add("This report is an AI-assisted first-pass model evidence review. Final validation remains subject to engineering review, drawings, specifications, and owner acceptance.");

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatActiveFilter(ReportFilterContext context)
        {
            if (context == null)
            {
                return "All Disciplines";
            }

            string discipline = string.IsNullOrWhiteSpace(context.ActiveDiscipline) ? "All Disciplines" : context.ActiveDiscipline;
            string status = string.IsNullOrWhiteSpace(context.ActiveStatus) ? "All" : context.ActiveStatus;
            string urgency = string.IsNullOrWhiteSpace(context.ActiveUrgency) ? "All" : context.ActiveUrgency;

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} | Status: {1} | Urgency: {2}",
                discipline,
                status,
                urgency);
        }

        private static string SafeLine(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(not set)" : value;
        }

        private static string BuildRequirementReference(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "(unknown requirement)";
            }

            string requirementId = result.Requirement != null && !string.IsNullOrWhiteSpace(result.Requirement.RequirementId)
                ? result.Requirement.RequirementId
                : "(no id)";

            string rowLabel = result.SourceRow > 0
                ? "Row " + result.SourceRow.ToString(CultureInfo.InvariantCulture)
                : result.Requirement != null && result.Requirement.RowNumber > 0
                    ? "Row " + result.Requirement.RowNumber.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;

            if (!string.IsNullOrWhiteSpace(rowLabel))
            {
                return requirementId + " " + rowLabel + " -";
            }

            return requirementId + " -";
        }

        private static string FormatDiscipline(RequirementDiscipline discipline)
        {
            return discipline == RequirementDiscipline.All
                ? "All Disciplines"
                : discipline.ToString();
        }

        private static string FormatScope(RequirementModelScope scope)
        {
            return scope == RequirementModelScope.CurrentView ? "Current View" : "Entire Model";
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
    }
}
