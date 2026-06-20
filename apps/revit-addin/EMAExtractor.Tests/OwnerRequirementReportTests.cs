using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using EMAExtractor.Models;
using EMAExtractor.Reporting;
using EMAExtractor.Requirements;
using Xunit;

namespace EMAExtractor.Tests
{
    public class OwnerRequirementReportTests
    {
        [Fact]
        public void BuildClipboardSummary_ContainsMasterFilterAndTopActions()
        {
            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));

            string summary = report.BuildClipboardSummary();

            Assert.Contains("EMA AI Owner Requirement Check", summary);
            Assert.Contains("Project: MEP-NISD-MIDDLE SCHOOL 8", summary);
            Assert.Contains("Model: MEP-NISD-MIDDLE SCHOOL 8", summary);
            Assert.Contains("Discipline: All Disciplines", summary);
            Assert.Contains("Active Filter: All Disciplines | Status: All | Urgency: All", summary);
            Assert.Contains("Scope: Entire Model", summary);
            Assert.Contains("Evidence Review Score", summary);
            Assert.Contains("Top Issues:", summary);
            Assert.Contains("Top Next Actions:", summary);
            Assert.Contains("REQ-101", summary);
            Assert.Contains("Assign panel and circuit values", summary);
            Assert.Contains("This report is an AI-assisted first-pass model evidence review.", summary);
        }

        [Fact]
        public void BuildClipboardSummary_ContainsDisciplineFilterAndSummary()
        {
            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.Electrical,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.Electrical),
                BuildFilterContext("Electrical", results, RequirementDiscipline.Electrical));

            string summary = report.BuildClipboardSummary();

            Assert.Contains("Discipline: Electrical", summary);
            Assert.Contains("Active Filter: Electrical | Status: All | Urgency: All", summary);
            Assert.Contains("Evidence Review Score", summary);
            Assert.Contains("Top Issues:", summary);
            Assert.Contains("Top Next Actions:", summary);
        }

        [Fact]
        public void Generate_WritesMasterHtmlReportWithExecutiveSections()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string path = OwnerRequirementHtmlReportGenerator.Generate(report);
            string html = File.ReadAllText(path);

            Assert.True(File.Exists(path));
            Assert.Equal(path, report.ReportPath);
            Assert.Contains("Executive Summary", html);
            Assert.Contains("Master Owner Requirements Review", html);
            Assert.Contains("View All", html);
            Assert.Contains("data-filter-value=\"All Disciplines\"", html);
            Assert.Contains("discipline-general active", html);
            Assert.Contains("Electrical", html);
            Assert.Contains("Lighting", html);
            Assert.Contains("Mechanical", html);
            Assert.Contains("Plumbing", html);
            Assert.Contains("Technology", html);
            Assert.Contains("Unknown / Needs Classification", html);
            Assert.Contains("href=\"#discipline-electrical\"", html);
            Assert.Contains("href=\"#discipline-mechanical\"", html);
            Assert.Contains("href=\"#discipline-unknown\"", html);
            Assert.Contains("discipline-electrical", html);
            Assert.Contains("discipline-lighting", html);
            Assert.Contains("discipline-mechanical", html);
            Assert.Contains("discipline-plumbing", html);
            Assert.Contains("discipline-technology", html);
            Assert.Contains("discipline-unknown", html);
            Assert.Contains("discipline-card", html);
            Assert.Contains("discipline-card-electrical", html);
            Assert.Contains("discipline-grid", html);
            Assert.Contains("View Section", html);
            Assert.Contains("Status and Urgency Legend", html);
            Assert.Contains("Urgency Legend", html);
            Assert.Contains("urgency-legend-card", html);
            Assert.Contains("status-legend-card", html);
            Assert.Contains("Key Issues &amp; Recommended Actions", html);
            Assert.Contains("Issues by Urgency", html);
            Assert.Contains("Discipline Sections", html);
            Assert.Contains("Ask EMA AI", html);
            Assert.Contains("Report Notes / No-Overclaim Boundary", html);
            Assert.Contains("Export Current View to PDF", html);
            Assert.Contains("Copy Current Summary", html);
        }

        [Fact]
        public void Generate_WritesDisciplineFilteredHtmlReport()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.Electrical,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.Electrical),
                BuildFilterContext("Electrical", results, RequirementDiscipline.Electrical));
            report.OutputFolder = tempFolder;

            string path = OwnerRequirementHtmlReportGenerator.Generate(report);
            string html = File.ReadAllText(path);

            Assert.Contains("Electrical Owner Requirements Review", html);
            Assert.Contains("Focused discipline: Electrical", html);
            Assert.Contains("Requirements shown:", html);
            Assert.Contains("Excluded from this view:", html);
            Assert.Contains("What are the top Electrical issues?", html);
            Assert.Contains("Why are these Electrical requirements Not Met?", html);
            Assert.Contains("Discipline Sections", html);
            Assert.Contains("Panel and circuit assignment", html);
            Assert.Contains("Lighting fixture coverage", html);
            Assert.Contains("id=\"discipline-electrical\"", html);
            Assert.Contains("id=\"discipline-unknown\"", html);
            Assert.Contains("id=\"req-req-101\"", html);
            Assert.Contains("status-not-met", html);
            Assert.Contains("urgency-critical", html);
            Assert.Contains("discipline-electrical", html);
            Assert.Contains("Copy Revit Element IDs", html);
            Assert.Contains("123456;123457;123458", html);
        }

        [Fact]
        public void Generate_RequirementCardsRetainEvidenceReasoningAndTraceability()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("What evidence was used?", html);
            Assert.Contains("Why this status?", html);
            Assert.Contains("Next Best Action", html);
            Assert.Contains("Confidence", html);
            Assert.Contains("Source Worksheet", html);
            Assert.Contains("Source Row", html);
            Assert.Contains("View Source &amp; Traceability", html);
            Assert.Contains("Matched Categories", html);
            Assert.Contains("Matched Parameters", html);
            Assert.Contains("Evidence Traceability", html);
            Assert.Contains("matched elements", html);
            Assert.Contains("Copy Revit Element IDs", html);
            Assert.Contains("123456;123457;123458", html);
        }

        [Fact]
        public void Generate_RequirementCardsIncludeFilteringDetailsAndParameterChecks()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("View Filtering Details", html);
            Assert.Contains("View Parameter Checks", html);
            Assert.Contains("Category / Family / Type Examples", html);
            Assert.Contains("What is missing?", html);
            Assert.Contains("Status Reason", html);
            Assert.Contains("Confidence Reason", html);
            Assert.Contains("View filtering stages", html);
            Assert.Contains("No filtering stages were captured", html);
        }

        [Fact]
        public void Generate_ExecutivePolishKeepsAdvancedDataCollapsed()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));
            string visibleBeforeJson = html.Substring(0, html.IndexOf("<script type=\"application/json\" id=\"ema-ai-report-context\">", StringComparison.OrdinalIgnoreCase));

            Assert.Contains("This report is a first-pass evidence review", html);
            Assert.Contains("Decision Summary", html);
            Assert.Contains("Key Parameters Considered", html);
            Assert.Contains("Panel = DP-1", html);
            Assert.Contains("Panel is considered because", html);
            Assert.Contains("Circuit Number is considered because", html);
            Assert.Contains("Present but not populated", html);
            Assert.Contains("View Rule &amp; Decision Logic", html);
            Assert.Contains("View Filtering Details", html);
            Assert.Contains("View Matched Elements", html);
            Assert.Contains("View All Revit Element IDs", html);
            Assert.Contains("View Score Details", html);
            Assert.Contains("View Evidence Behind This Issue", html);
            Assert.Contains("Why this is urgent", html);
            Assert.DoesNotContain("\"schema_version\"", visibleBeforeJson);
            Assert.DoesNotContain("undefined", visibleBeforeJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("null", visibleBeforeJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("123456;123457;123458;123459;123460;123461", visibleBeforeJson);
            Assert.Contains("@media print", html);
            Assert.Contains("break-inside:avoid", html);
            Assert.Contains("print-color-adjust:exact", html);
        }

        [Fact]
        public void Generate_EmbedsValidMachineReadableJsonAndKeepsItHidden()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));
            string lower = html.ToLowerInvariant();
            string json = ExtractMachineReadableJson(html);
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Contains("<script type=\"application/json\" id=\"ema-ai-report-context\">", html);
            Assert.DoesNotContain("\"schema_version\"", html.Substring(0, html.IndexOf("<script type=\"application/json\" id=\"ema-ai-report-context\">", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal("1.0", document.RootElement.GetProperty("schema_version").GetString());
            Assert.True(document.RootElement.GetProperty("report_metadata").TryGetProperty("project_name", out _));
            Assert.True(document.RootElement.GetProperty("summary_counts").TryGetProperty("met", out _));
            Assert.True(document.RootElement.GetProperty("discipline_summaries").GetArrayLength() > 0);
            Assert.True(document.RootElement.GetProperty("key_issues").GetArrayLength() > 0);
            Assert.True(document.RootElement.GetProperty("requirement_results").GetArrayLength() > 0);
            JsonElement firstRequirement = document.RootElement.GetProperty("requirement_results")[0];
            Assert.True(firstRequirement.TryGetProperty("ai_lookup_hints", out JsonElement hints));
            Assert.True(hints.TryGetProperty("suggested_question", out _));
            Assert.True(hints.TryGetProperty("search_terms", out _));
            Assert.True(hints.TryGetProperty("evidence_location", out _));
            Assert.True(hints.TryGetProperty("human_review_needed", out _));
            Assert.True(firstRequirement.TryGetProperty("matched_element_ids", out _));
            Assert.True(firstRequirement.TryGetProperty("matched_unique_ids", out _));
            Assert.True(firstRequirement.TryGetProperty("element_id_copy_text", out _));
            Assert.True(firstRequirement.TryGetProperty("matched_elements", out JsonElement matchedElements));
            Assert.True(matchedElements.GetArrayLength() > 0);
            Assert.True(hints.TryGetProperty("revit_element_ids", out _));
            Assert.True(hints.TryGetProperty("element_id_copy_text", out _));
            Assert.True(document.RootElement.GetProperty("discipline_summaries")[0].TryGetProperty("display", out JsonElement display));
            Assert.True(display.TryGetProperty("primary_color", out _));
            Assert.True(display.TryGetProperty("background_color", out _));
            Assert.True(display.TryGetProperty("border_color", out _));
            Assert.True(display.TryGetProperty("text_color", out _));
            Assert.Contains("id=\"discipline-electrical\"", html);
            Assert.Contains("href=\"#discipline-electrical\"", html);
            Assert.Contains("id=\"req-req-101\"", html);

            Assert.DoesNotContain("\"projectname\":", lower);
            Assert.DoesNotContain("\"filteredresults\":", lower);
            Assert.DoesNotContain("certified", lower);
            Assert.DoesNotContain("approved", lower);
            Assert.DoesNotContain("guaranteed", lower);
            Assert.DoesNotContain("legally compliant", lower);
            Assert.DoesNotContain("final compliance", lower);
            Assert.DoesNotContain("docker", lower);
            Assert.DoesNotContain("backend", lower);
        }

        [Fact]
        public void Generate_HiddenJsonIncludesTraceAndExplainabilityFields()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));
            string json = ExtractMachineReadableJson(html);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement firstRequirement = document.RootElement.GetProperty("requirement_results")[0];
            Assert.True(firstRequirement.TryGetProperty("filter_trace", out JsonElement filterTrace));
            Assert.True(filterTrace.TryGetProperty("candidate_stages", out JsonElement candidateStages));
            Assert.True(candidateStages.ValueKind == JsonValueKind.Array);
            Assert.True(filterTrace.TryGetProperty("direct_closing_evidence", out JsonElement directClosingEvidence));
            Assert.True(filterTrace.TryGetProperty("supporting_context", out JsonElement supportingContext));
            Assert.True(filterTrace.TryGetProperty("missing_direct_evidence", out JsonElement missingDirectEvidence));
            Assert.True(directClosingEvidence.ValueKind == JsonValueKind.Array);
            Assert.True(supportingContext.ValueKind == JsonValueKind.Array);
            Assert.True(missingDirectEvidence.ValueKind == JsonValueKind.Array);
            Assert.True(firstRequirement.TryGetProperty("direct_closing_evidence", out JsonElement topLevelDirectEvidence));
            Assert.True(firstRequirement.TryGetProperty("supporting_context", out JsonElement topLevelSupportingContext));
            Assert.True(firstRequirement.TryGetProperty("missing_direct_evidence", out JsonElement topLevelMissingDirectEvidence));
            Assert.True(topLevelDirectEvidence.ValueKind == JsonValueKind.Array);
            Assert.True(topLevelSupportingContext.ValueKind == JsonValueKind.Array);
            Assert.True(topLevelMissingDirectEvidence.ValueKind == JsonValueKind.Array);
        }

        [Fact]
        public void Generate_UsesHighContrastStatusAndUrgencyClasses()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("status-met", html);
            Assert.Contains("status-not-met", html);
            Assert.Contains("status-needs-review", html);
            Assert.Contains("status-insufficient-data", html);
            Assert.Contains("status-not-applicable", html);
            Assert.Contains("urgency-critical", html);
            Assert.Contains("urgency-high", html);
            Assert.Contains("urgency-medium", html);
            Assert.Contains("urgency-low", html);
            Assert.Contains("urgency-needs-review", html);
            Assert.Contains("border-left:6px solid", html);
            Assert.Contains("border-top:4px solid", html);
            Assert.Contains("@media print", html);
            Assert.Contains("print-color-adjust:exact", html);
            Assert.Contains("-webkit-print-color-adjust:exact", html);
            Assert.Contains("discipline-electrical", html);
            Assert.Contains("discipline-lighting", html);
            Assert.Contains("discipline-mechanical", html);
            Assert.Contains("discipline-plumbing", html);
            Assert.Contains("discipline-technology", html);
            Assert.Contains("discipline-unknown", html);
        }

        [Fact]
        public void Generate_AskEmaAiQuestionsChangeByDiscipline()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.Electrical,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.Electrical),
                BuildFilterContext("Electrical", results, RequirementDiscipline.Electrical));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("What are the top Electrical issues?", html);
            Assert.Contains("Which Electrical model data is missing?", html);
            Assert.Contains("Ask EMA AI", html);
        }

        private static RequirementCheckReport BuildReport(
            RequirementDiscipline discipline,
            List<RequirementCheckResult> results,
            List<DisciplineSummary> disciplineSummaries,
            List<KeyIssue> keyIssues,
            ReportFilterContext filterContext)
        {
            return new RequirementCheckReport
            {
                ProjectName = "MEP-NISD-MIDDLE SCHOOL 8",
                ModelName = "MEP-NISD-MIDDLE SCHOOL 8",
                RequirementsFileName = "NISD_Owner_Requirements.xlsx",
                Discipline = discipline,
                Scope = RequirementModelScope.EntireModel,
                GeneratedAt = new DateTime(2026, 6, 4, 9, 30, 0, DateTimeKind.Local),
                ModelElementCount = 21868,
                Results = results,
                Summary = RequirementCheckSummary.FromResults(results),
                OverallScore = ScoreCalculator.CalculateOverallScore(results),
                ReadinessScore = ScoreCalculator.CalculateReadiness(results, DateTime.Now).OverallScore,
                ReadinessLabel = ReadinessLabel.OnTrack,
                DisciplineSummaries = disciplineSummaries,
                KeyIssues = keyIssues,
                FilterContext = filterContext,
                RequirementsFilePath = @"C:\Users\Public\Documents\NORTHWEST ISD 06.02.2025.xlsx",
                ReportPath = @"C:\Users\Public\Documents\EMA_AI_Requirement_Check_Master_20260604_093000.html"
            };
        }

        private static ReportFilterContext BuildFilterContext(string activeDiscipline, List<RequirementCheckResult> results, RequirementDiscipline discipline)
        {
            List<RequirementCheckResult> filtered = discipline == RequirementDiscipline.All
                ? results.ToList()
                : results.Where(result => string.Equals(GetDisciplineLabel(result), discipline.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();

            List<KeyIssue> filteredIssues = BuildKeyIssues(results, discipline);

            return new ReportFilterContext
            {
                ActiveDiscipline = activeDiscipline,
                ActiveStatus = "All",
                ActiveUrgency = "All",
                FilteredResults = filtered,
                FilteredKeyIssues = filteredIssues,
                FilteredCounts = RequirementCheckSummary.FromResults(filtered),
                FilteredScores = new ReportFilterScores
                {
                    OverallScore = ScoreCalculator.CalculateOverallScore(filtered),
                    ReadinessScore = ScoreCalculator.CalculateReadiness(filtered, DateTime.Now).OverallScore,
                    DisciplineScore = discipline == RequirementDiscipline.All
                        ? ScoreCalculator.CalculateOverallScore(filtered)
                        : ScoreCalculator.CalculateDisciplineScore(filtered, discipline),
                    ApplicableCount = filtered.Count,
                    TotalCount = results.Count,
                    KeyIssueCount = filteredIssues.Count
                },
                SuggestedQuestions = BuildSuggestedQuestions(activeDiscipline)
            };
        }

        private static List<DisciplineSummary> BuildDisciplineSummaries(IReadOnlyCollection<RequirementCheckResult> results)
        {
            string[] disciplines =
            {
                "Electrical",
                "Lighting",
                "Mechanical",
                "Plumbing",
                "Technology",
                "Unknown / Needs Classification"
            };

            List<DisciplineSummary> summaries = new List<DisciplineSummary>();
            foreach (string discipline in disciplines)
            {
                List<RequirementCheckResult> group = results.Where(result => string.Equals(GetDisciplineLabel(result), discipline, StringComparison.OrdinalIgnoreCase)).ToList();
                if (group.Count == 0)
                {
                    continue;
                }

                summaries.Add(new DisciplineSummary
                {
                    Discipline = discipline,
                    Total = group.Count,
                    Applicable = group.Count(item => item.Status != RequirementCheckStatus.NotApplicable),
                    Met = group.Count(item => item.Status == RequirementCheckStatus.Met),
                    NotMet = group.Count(item => item.Status == RequirementCheckStatus.NotMet),
                    NeedsHumanReview = group.Count(item => item.Status == RequirementCheckStatus.NeedsHumanReview),
                    InsufficientModelData = group.Count(item => item.Status == RequirementCheckStatus.InsufficientModelData),
                    NotApplicable = group.Count(item => item.Status == RequirementCheckStatus.NotApplicable),
                    DisciplineScore = discipline == "Unknown / Needs Classification"
                        ? ScoreCalculator.CalculateOverallScore(group)
                        : ScoreCalculator.CalculateDisciplineScore(group, RequirementDisciplineNormalizer.Parse(discipline, RequirementDiscipline.All)),
                    KeyIssueCount = group.Count(item => item.Status != RequirementCheckStatus.Met && item.Status != RequirementCheckStatus.NotApplicable),
                    ResponsibleRole = discipline,
                    TopNextActions = group
                        .Where(item => item.Status != RequirementCheckStatus.Met && item.Status != RequirementCheckStatus.NotApplicable)
                        .Select(item => item.NextBestAction)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3)
                        .ToList()
                });
            }

            return summaries;
        }

        private static List<KeyIssue> BuildKeyIssues(IReadOnlyCollection<RequirementCheckResult> results, RequirementDiscipline discipline)
        {
            return KeyIssueRanker.RankIssues(results, discipline, 10);
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

            return new List<string>
            {
                "What are the top project-level issues?",
                "Which discipline has the most risk?",
                "What should be fixed first?",
                "Which requirements need human review?",
                "Which model data is missing?"
            };
        }

        private static List<RequirementCheckResult> BuildSampleResults()
        {
            return new List<RequirementCheckResult>
            {
                new RequirementCheckResult
                {
                    Requirement = new OwnerRequirementRow
                    {
                        RequirementId = "REQ-101",
                        SourceSheet = "Electrical",
                        RowNumber = 12,
                        RequirementText = "Provide panel and circuit assignment for lighting fixtures.",
                        Discipline = "Electrical",
                        SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                    },
                    RequirementId = "REQ-101",
                    RequirementText = "Provide panel and circuit assignment for lighting fixtures.",
                    Discipline = "Electrical",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx",
                    Status = RequirementCheckStatus.NotMet,
                    Confidence = 0.86,
                    IssueTitle = "Panel and circuit assignment",
                    Reasoning = "The model contains lighting fixtures, but the panel and circuit values are missing.",
                    NextBestAction = "Assign panel and circuit values to the listed elements before the next deliverable.",
                    ResponsibleRole = "Electrical",
                    EvidenceSummary = "1 candidate element inspected. 0 element(s) already contain the required parameter data.",
                    SourceWorksheet = "Electrical",
                    SourceRow = 12,
                    Evidence = new List<string>
                    {
                        "1 candidate element inspected.",
                        "0 element(s) already contain the required parameter data."
                    },
                    MatchedElementIds = new List<long> { 123456, 123457, 123458 },
                    MatchedUniqueIds = new List<string> { "0f2a1f1e-0000-1111-2222-abcdef123456", "0f2a1f1e-0000-1111-2222-abcdef123457", "0f2a1f1e-0000-1111-2222-abcdef123458" },
                    MatchedElementSummaries = new List<string>
                    {
                        "ElementId 123456 | Lighting Fixtures | LGT-01",
                        "ElementId 123457 | Lighting Fixtures | LGT-02",
                        "ElementId 123458 | Lighting Fixtures | LGT-03"
                    },
                    MatchedCategories = new List<string> { "Lighting Fixtures" },
                    MatchedFamilies = new List<string> { "Fixture Family" },
                    MatchedTypes = new List<string> { "Type A" },
                    MatchedParameters = new List<string> { "Panel", "Circuit Number" },
                    MissingEvidence = new List<string> { "Circuit Number missing on 12 of 23 fixtures" },
                    MatchedElements = new List<MatchedElementEvidence>
                    {
                        new MatchedElementEvidence
                        {
                            ElementId = "123456",
                            UniqueId = "0f2a1f1e-0000-1111-2222-abcdef123456",
                            Category = "Lighting Fixtures",
                            Family = "Fixture Family",
                            Type = "Type A",
                            Level = "Level 1",
                            MatchedParameters = new List<string> { "Panel" },
                            MissingParameters = new List<string> { "Circuit Number" },
                            EvidenceReason = "Panel populated but circuit number missing.",
                            ParameterValues = new Dictionary<string, string> { { "Room", "Classroom 101" } }
                        },
                        new MatchedElementEvidence
                        {
                            ElementId = "123457",
                            UniqueId = "0f2a1f1e-0000-1111-2222-abcdef123457",
                            Category = "Lighting Fixtures",
                            Family = "Fixture Family",
                            Type = "Type A",
                            Level = "Level 1",
                            MatchedParameters = new List<string> { "Panel" },
                            MissingParameters = new List<string> { "Circuit Number" },
                            EvidenceReason = "Panel populated but circuit number missing."
                        },
                        new MatchedElementEvidence
                        {
                            ElementId = "123458",
                            UniqueId = "0f2a1f1e-0000-1111-2222-abcdef123458",
                            Category = "Lighting Fixtures",
                            Family = "Fixture Family",
                            Type = "Type A",
                            Level = "Level 1",
                            MatchedParameters = new List<string> { "Panel" },
                            MissingParameters = new List<string> { "Circuit Number" },
                            EvidenceReason = "Panel populated but circuit number missing."
                        }
                    },
                    ElementIdCopyText = "123456;123457;123458",
                    Urgency = "Critical",
                    IsKeyIssue = true,
                    MatchedModelElementCount = 3,
                    RequirementType = "panel_circuit_power",
                    RequirementTypeReason = "Requirement is about panel and circuit assignment.",
                    ValidationType = ValidationType.Model,
                    ValidationTypeReason = "Panel and circuit assignment is directly model-checkable when the electrical parameters are exported.",
                    RuleApplied = "panel_circuit_assignment",
                    RuleFamily = "electrical_connection",
                    RuleTriggerKeywords = new List<string> { "panel", "circuit" },
                    RuleExpectedEvidence = "Panel, Circuit Number, or Supply From parameters on electrical elements",
                    ExpectedEvidenceSources = new List<string> { "Revit electrical fixture/device parameters", "Panel metadata" },
                    ExpectedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures" },
                    AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures" },
                    ExcludedCategories = new List<string> { "Mechanical Equipment", "Plumbing Fixtures" },
                    CandidateScopeReason = "Scope to electrical categories that can carry panel/circuit metadata.",
                    FallbackAllowed = false,
                    ModelEvidenceSufficiency = "Met requires direct electrical connection parameters.",
                    WhyNotModelCloseable = "Generic category presence is not enough to prove panel/circuit assignment.",
                    ExpectedFamilyTypeHints = new List<string> { "panel", "receptacle", "fixture", "equipment" },
                    ExpectedParameters = new List<string> { "Panel", "Circuit Number", "Supply From" },
                    ActualMatchedCategories = new List<string> { "Lighting Fixtures" },
                    ActualMatchedParameters = new List<string> { "Panel", "Circuit Number" },
                    ActualParameterValueExamples = new List<string> { "Panel = DP-1", "Circuit Number = 12" },
                    MissingExpectedParameters = new List<string> { "Supply From" },
                    MatchedFamilyTypeSummary = new List<string> { "Fixture Family | Type A: 3 element(s)" },
                    ParameterChecks = new List<ParameterCheckResult>
                    {
                        new ParameterCheckResult
                        {
                            ParameterName = "Panel",
                            ExpectedMeaning = "Connected panel",
                            ExpectedValuePattern = "non-empty panel name",
                            ActualValue = "DP-1",
                            Source = "instance parameter",
                            IsPresent = true,
                            IsEmpty = false,
                            IsMatch = true,
                            IsRequired = true,
                            FailureReason = "Panel satisfies the requirement."
                        },
                        new ParameterCheckResult
                        {
                            ParameterName = "Circuit Number",
                            ExpectedMeaning = "Connected circuit number",
                            ExpectedValuePattern = "non-empty circuit number",
                            ActualValue = "",
                            Source = "instance parameter",
                            IsPresent = true,
                            IsEmpty = true,
                            IsMatch = false,
                            IsRequired = true,
                            FailureReason = "Circuit Number is present but not populated."
                        }
                    },
                    FilterTrace = new RequirementFilterTrace
                    {
                        DisciplineFilter = "Electrical",
                        ScopeFilter = "Entire Model",
                        StatusFilter = "All",
                        RequirementIntent = "Provide panel and circuit assignment for lighting fixtures.",
                        ValidationType = "Model",
                        ValidationTypeReason = "Panel and circuit assignment is directly model-checkable when the electrical parameters are exported.",
                        RuleApplied = "panel_circuit_assignment",
                        RuleFamily = "electrical_connection",
                        TriggerKeywords = new List<string> { "panel", "circuit" },
                        ExpectedEvidenceSources = new List<string> { "Revit electrical fixture/device parameters", "Panel metadata" },
                        ExpectedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures" },
                        ExpectedFamilyTypeHints = new List<string> { "panel", "receptacle", "fixture", "equipment" },
                        ExpectedParameters = new List<string> { "Panel", "Circuit Number", "Supply From" },
                        CandidateStages = new List<FilterStageTrace>
                        {
                            new FilterStageTrace
                            {
                                StageName = "Model snapshot loaded",
                                Description = "All synced Revit elements were loaded into the deterministic evidence index.",
                                InputCount = 21868,
                                OutputCount = 21868,
                                Criteria = "Loaded model snapshot",
                                ExampleMatchedValues = new List<string> { "ElementId 123456 | Lighting Fixtures | Fixture Family | Type A" }
                            }
                        }
                    },
                    StatusReason = "The model contains lighting fixtures, but the panel and circuit values are missing.",
                    ConfidenceReason = "Evidence alignment: Weak Status: Not Met Confidence was reduced because the evidence was broad, mismatched, or manual-only.",
                    ModelEvidenceLimitations = "Direct parameters were missing: Supply From The matched evidence is weak.",
                    HumanReviewNeeded = false
                },
                new RequirementCheckResult
                {
                    Requirement = new OwnerRequirementRow
                    {
                        RequirementId = "REQ-202",
                        SourceSheet = "Lighting",
                        RowNumber = 18,
                        RequirementText = "Confirm lighting fixture documentation and coordinate with the specification.",
                        Discipline = "Lighting",
                        SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                    },
                    RequirementId = "REQ-202",
                    RequirementText = "Confirm lighting fixture documentation and coordinate with the specification.",
                    Discipline = "Lighting",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx",
                    Status = RequirementCheckStatus.NeedsHumanReview,
                    Confidence = 0.66,
                    IssueTitle = "Lighting fixture coverage",
                    Reasoning = "The requirement references documentation or specification evidence that is not available in the Revit model.",
                    NextBestAction = "Review this item manually because the requirement depends on non-model evidence.",
                    ResponsibleRole = "Lighting",
                    EvidenceSummary = "The requirement text did not match a deterministic rule.",
                    SourceWorksheet = "Lighting",
                    SourceRow = 18,
                    Evidence = new List<string> { "The requirement text did not match a deterministic rule." },
                    Urgency = "Needs Review",
                    IsKeyIssue = true,
                    MatchedModelElementCount = 0,
                    RequirementType = "drawing_spec_manual_owner_approval",
                    RequirementTypeReason = "Requirement depends on documentation and specification review.",
                    ValidationType = ValidationType.Drawing
                },
                new RequirementCheckResult
                {
                    Requirement = new OwnerRequirementRow
                    {
                        RequirementId = "REQ-303",
                        SourceSheet = "Mechanical",
                        RowNumber = 27,
                        RequirementText = "Provide mechanical equipment level placement.",
                        Discipline = "Mechanical",
                        SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                    },
                    RequirementId = "REQ-303",
                    RequirementText = "Provide mechanical equipment level placement.",
                    Discipline = "Mechanical",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx",
                    Status = RequirementCheckStatus.Met,
                    Confidence = 0.94,
                    IssueTitle = "Mechanical equipment placement",
                    Reasoning = "The deterministic check found matching evidence, so it is marked as Met.",
                    NextBestAction = "No action required for this requirement.",
                    ResponsibleRole = "Mechanical",
                    EvidenceSummary = "All candidate elements satisfied the selected parameter check.",
                    SourceWorksheet = "Mechanical",
                    SourceRow = 27,
                    Evidence = new List<string> { "All candidate elements satisfied the selected parameter check." },
                    Urgency = "Low",
                    IsKeyIssue = false,
                    MatchedModelElementCount = 3,
                    RequirementType = "mechanical_equipment_coverage",
                    RequirementTypeReason = "Requirement is about mechanical equipment placement.",
                    ValidationType = ValidationType.Model
                },
                new RequirementCheckResult
                {
                    Requirement = new OwnerRequirementRow
                    {
                        RequirementId = "REQ-404",
                        SourceSheet = "Technology",
                        RowNumber = 34,
                        RequirementText = "Identify technology devices but note missing model data.",
                        Discipline = "Technology",
                        SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                    },
                    RequirementId = "REQ-404",
                    RequirementText = "Identify technology devices but note missing model data.",
                    Discipline = "Technology",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx",
                    Status = RequirementCheckStatus.InsufficientModelData,
                    Confidence = 0.20,
                    IssueTitle = "Technology / low-voltage coverage",
                    Reasoning = "The model snapshot did not contain enough evidence to verify the item.",
                    NextBestAction = "Review this requirement against the relevant specification because the model does not contain enough evidence.",
                    ResponsibleRole = "Technology",
                    EvidenceSummary = "No matching model elements were available for this check.",
                    SourceWorksheet = "Technology",
                    SourceRow = 34,
                    Evidence = new List<string> { "No matching model elements were available for this check." },
                    Urgency = "High",
                    IsKeyIssue = true,
                    MatchedModelElementCount = 0,
                    RequirementType = "technology_low_voltage_security_fire_alarm",
                    RequirementTypeReason = "Requirement is about technology device identification.",
                    ValidationType = ValidationType.Manual
                },
                new RequirementCheckResult
                {
                    Requirement = new OwnerRequirementRow
                    {
                        RequirementId = "REQ-505",
                        SourceSheet = "General",
                        RowNumber = 40,
                        RequirementText = "Coordinate cross-disciplinary owner requirements with review notes.",
                        Discipline = "",
                        SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                    },
                    RequirementId = "REQ-505",
                    RequirementText = "Coordinate cross-disciplinary owner requirements with review notes.",
                    Discipline = "",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx",
                    Status = RequirementCheckStatus.NeedsHumanReview,
                    Confidence = 0.42,
                    IssueTitle = "Cross-disciplinary coordination",
                    Reasoning = "The requirement depends on non-model coordination and owner interpretation.",
                    NextBestAction = "Confirm the owner interpretation and supporting documentation before closing this item.",
                    ResponsibleRole = "Unknown / Needs Classification",
                    EvidenceSummary = "Model evidence is not specific enough for deterministic confirmation.",
                    SourceWorksheet = "General",
                    SourceRow = 40,
                    Evidence = new List<string> { "Model evidence is not specific enough for deterministic confirmation." },
                    Urgency = "Needs Review",
                    IsKeyIssue = true,
                    MatchedModelElementCount = 0,
                    RequirementType = "unknown_ambiguous",
                    RequirementTypeReason = "Cross-disciplinary owner requirement with no stable semantic family.",
                    ValidationType = ValidationType.Hybrid
                }
            };
        }

        private static string GetDisciplineLabel(RequirementCheckResult result)
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

        [Fact]
        public void Generate_VisibleReportDoesNotShowReadinessOrOverallScore()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            string visibleHtml = html;
            int jsonStart = visibleHtml.IndexOf("<script type=\"application/json\" id=\"ema-ai-report-context\">", StringComparison.OrdinalIgnoreCase);
            if (jsonStart >= 0)
            {
                int jsonEnd = visibleHtml.IndexOf("</script>", jsonStart, StringComparison.OrdinalIgnoreCase);
                if (jsonEnd > jsonStart)
                {
                    visibleHtml = visibleHtml.Substring(0, jsonStart) + visibleHtml.Substring(jsonEnd + "</script>".Length);
                }
            }

            int jsStart = visibleHtml.IndexOf("<script>", StringComparison.OrdinalIgnoreCase);
            if (jsStart >= 0)
            {
                int jsEnd = visibleHtml.IndexOf("</script>", jsStart, StringComparison.OrdinalIgnoreCase);
                if (jsEnd > jsStart)
                {
                    visibleHtml = visibleHtml.Substring(0, jsStart) + visibleHtml.Substring(jsEnd + "</script>".Length);
                }
            }

            Assert.Contains("Evidence Review Score", visibleHtml);
            Assert.DoesNotContain(">Readiness Score<", visibleHtml);
            Assert.DoesNotContain(">Overall Score<", visibleHtml);
            Assert.Contains("first-pass evidence review", visibleHtml);
            Assert.Contains("does not certify compliance", visibleHtml);
        }

        [Fact]
        public void Generate_ReportDoesNotContainUndefinedOrVisibleNull()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            string visibleHtml = html;
            int jsonStart = visibleHtml.IndexOf("<script type=\"application/json\" id=\"ema-ai-report-context\">", StringComparison.OrdinalIgnoreCase);
            if (jsonStart >= 0)
            {
                int jsonEnd = visibleHtml.IndexOf("</script>", jsonStart, StringComparison.OrdinalIgnoreCase);
                if (jsonEnd > jsonStart)
                {
                    visibleHtml = visibleHtml.Substring(0, jsonStart) + visibleHtml.Substring(jsonEnd + "</script>".Length);
                }
            }

            Assert.DoesNotContain(">undefined<", visibleHtml);
            Assert.DoesNotContain(">null<", visibleHtml);
            Assert.DoesNotContain("undefined - ", visibleHtml);
            Assert.DoesNotContain("null - ", visibleHtml);
        }

        [Fact]
        public void Generate_UrgencyLabelsAreNormalized()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            string[] allowedUrgencies = { "Critical", "High", "Medium", "Low", "Needs Review" };
            foreach (var match in Regex.Matches(html, @"data-urgency=""([^""]*)""").Cast<Match>())
            {
                string urgency = match.Groups[1].Value;
                Assert.Contains(urgency, allowedUrgencies);
            }
        }

        [Fact]
        public void Generate_DisciplineAllocationUsesCardGrid()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("discipline-grid", html);
            Assert.Contains("discipline-card", html);
            Assert.Contains("discipline-card-electrical", html);
            Assert.Contains("discipline-card-lighting", html);
            Assert.Contains("discipline-card-mechanical", html);
            Assert.Contains("discipline-card-technology", html);
            Assert.Contains("discipline-card-header", html);
            Assert.Contains("discipline-card-identity", html);
            Assert.Contains("discipline-card-swatch", html);
            Assert.Contains("discipline-card-name", html);
            Assert.Contains("discipline-card-score", html);
            Assert.Contains("discipline-card-counts", html);
            Assert.Contains("style=\"font-size:12px;padding:6px 12px;\"", html);
            Assert.Contains("View Section", html);
        }

        [Fact]
        public void Generate_RequirementCardsUseBlockClasses()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("requirement-card", html);
            Assert.Contains("evidence-block", html);
            Assert.Contains("reasoning-block", html);
            Assert.Contains("next-action-block", html);
            Assert.Contains("element-id-box", html);
            Assert.Contains("traceability-block", html);
        }

        [Fact]
        public void Generate_FilterPanelUsesChipClasses()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("filter-panel", html);
            Assert.Contains("filter-chip", html);
            Assert.Contains("filter-chip-row", html);
            Assert.Contains("action-chip", html);
            Assert.Contains("appearance:none", html);
        }

        [Fact]
        public void Generate_IssueGridNotCrampedFourColumns()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("minmax(440px", html);
            Assert.Contains("issue-card", html);
        }

        [Fact]
        public void Generate_UrgencyGroupsUseStructuredLayout()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("urgency-group", html);
            Assert.Contains("urgency-group-header", html);
        }

        [Fact]
        public void Generate_PrintCssHidesFilterPanel()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains(".filter-panel", html);
            Assert.Contains("display:none !important", html);
        }

        [Fact]
        public void Generate_ReportUsesDesignSystemVariables()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("--page-bg:#EEF3F8", html);
            Assert.Contains("--card-bg:#FFFFFF", html);
            Assert.Contains("--navy:#0B1F3A", html);
            Assert.Contains("--blue:#2563EB", html);
            Assert.Contains("--radius-lg:20px", html);
            Assert.Contains("report-section", html);
            Assert.Contains("identity-grid", html);
            Assert.Contains("identity-card", html);
        }

        [Fact]
        public void Generate_NoVisibleBackendDebugLanguage()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));
            string lower = html.ToLowerInvariant();

            Assert.DoesNotContain("docker", lower);
            Assert.DoesNotContain("backend", lower);
            Assert.DoesNotContain("api key", lower);
            Assert.DoesNotContain("azure", lower);
        }

        [Fact]
        public void Generate_FocusVisibleStylesExist()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("focus-visible", html);
        }

        [Fact]
        public void Generate_FilterChipsHaveDisciplineColorClasses()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("filter-chip discipline-electrical", html);
            Assert.Contains("filter-chip discipline-lighting", html);
            Assert.Contains("filter-chip discipline-mechanical", html);
            Assert.Contains("filter-chip discipline-plumbing", html);
            Assert.Contains("filter-chip discipline-technology", html);
            Assert.Contains("filter-chip discipline-unknown", html);
            Assert.Contains("filter-chip discipline-general active", html);
        }

        [Fact]
        public void Generate_SourceFilePathsShowFilenameOnly()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            string visibleHtml = html;
            int jsonStart = visibleHtml.IndexOf("<script type=\"application/json\"", StringComparison.OrdinalIgnoreCase);
            if (jsonStart >= 0)
            {
                int jsonEnd = visibleHtml.IndexOf("</script>", jsonStart, StringComparison.OrdinalIgnoreCase);
                if (jsonEnd > jsonStart)
                {
                    visibleHtml = visibleHtml.Substring(0, jsonStart) + visibleHtml.Substring(jsonEnd + "</script>".Length);
                }
            }

            Assert.Contains("NORTHWEST ISD 06.02.2025.xlsx", visibleHtml);
        }

        [Fact]
        public void Generate_PrintCssIncludesBreakAvoidAndColorAdjust()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("print-color-adjust:exact", html);
            Assert.Contains("-webkit-print-color-adjust:exact", html);
            Assert.Contains("break-inside:avoid", html);
            Assert.Contains("page-break-inside:avoid", html);
        }

        [Fact]
        public void Generate_FilterContextBannerExistsForMasterView()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("filter-context-banner", html);
            Assert.Contains("Master View", html);
            Assert.Contains("Showing all", html);
        }

        [Fact]
        public void Generate_FilterContextBannerExistsForDisciplineView()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.Electrical,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.Electrical),
                BuildFilterContext("Electrical", results, RequirementDiscipline.Electrical));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("filter-context-banner", html);
            Assert.Contains("Active Filtered View", html);
            Assert.Contains("Discipline: Electrical", html);
            Assert.Contains("Counts and scores below reflect this filtered view.", html);
        }

        [Fact]
        public void Generate_TraceabilityCollapsedByDefault()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("View Evidence Summary", html);
            Assert.Contains("id-preview", html);
            Assert.Contains("Copy Revit Element IDs", html);
            Assert.Contains("View All Revit Element IDs", html);
            Assert.Contains("View Unique IDs", html);
            Assert.Contains("View Matched Elements", html);
            Assert.Contains("element-id-scroll", html);
            Assert.Contains("matched-elements-scroll", html);
        }

        [Fact]
        public void Generate_ElementIdPreviewShowsCountAndFirstIds()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("<strong>3</strong> matched elements", html);
            Assert.Contains("123456; 123457; 123458", html);
        }

        [Fact]
        public void Generate_NextActionsCalloutHasId()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("id=\"top-next-actions\"", html);
            Assert.Contains("next-actions-callout", html);
        }

        [Fact]
        public void Generate_CopySummaryJsIncludesFilterContext()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("Requirements shown", html);
            Assert.Contains("Key issues in view", html);
            Assert.Contains("Review items in view", html);
            Assert.Contains("Filtered evidence review score", html);
            Assert.Contains("Master View", html);
            Assert.Contains("Active Filtered View", html);
        }

        [Fact]
        public void Generate_FilterBannerJsUpdatesOnFilterChange()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("updateFilterBanner", html);
            Assert.Contains("updateNextActions", html);
            Assert.Contains("No immediate corrective actions in this filtered Met view", html);
            Assert.Contains("Human review items may require drawings or specifications", html);
        }

        [Fact]
        public void Generate_EmptyFilterStateClassExists()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("empty-filter-state", html);
            Assert.Contains("No requirements match the current filter", html);
        }

        [Fact]
        public void Generate_KeyIssueTileCountsOnlyRankedIssueCards()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            int rankedMarkers = Regex.Matches(html, "data-issue-rank-card=\"true\"").Count;
            int rankedArticles = Regex.Matches(html, "<article class=\"issue-card").Count;
            Assert.True(rankedMarkers > 0, "Master report must render ranked key-issue cards.");
            Assert.Equal(rankedArticles, rankedMarkers);

            // Compact urgency rows duplicate ranked issues per requirement and must not carry the ranked marker,
            // otherwise the Key Issues tile double-counts after the client-side refresh.
            foreach (Match row in Regex.Matches(html, "<div class=\"compact-row issue-card[^>]*>"))
            {
                Assert.DoesNotContain("data-issue-rank-card", row.Value);
            }

            Assert.Contains("card.dataset.issueRankCard === 'true'", html);
        }

        [Fact]
        public void Generate_FilteredViewJsRefreshesMasterScoreTiles()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            // When the master report is filtered to a discipline, the master-grid score tiles must
            // refresh instead of silently keeping the unfiltered values next to the
            // "Counts and scores below reflect this filtered view" banner.
            Assert.Contains("Recomputed for the active filtered view.", html);
            Assert.Contains("Disciplines with requirements in the active filtered view.", html);
        }

        [Fact]
        public void Generate_HiddenJsonRemainsValidAfterTraceabilityRestructure()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));
            string json = ExtractMachineReadableJson(html);
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.True(document.RootElement.GetProperty("requirement_results").GetArrayLength() > 0);
            JsonElement first = document.RootElement.GetProperty("requirement_results")[0];
            Assert.True(first.TryGetProperty("matched_element_ids", out JsonElement ids));
            Assert.True(ids.GetArrayLength() > 0);
            Assert.True(first.TryGetProperty("matched_unique_ids", out _));
            Assert.True(first.TryGetProperty("matched_elements", out JsonElement elements));
            Assert.True(elements.GetArrayLength() > 0);
        }

        [Fact]
        public void Generate_NoHorizontalOverflowCssExists()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("overflow-wrap:anywhere", html);
            Assert.Contains("element-id-scroll", html);
            Assert.Contains("max-height:200px", html);
            Assert.Contains("matched-elements-scroll", html);
            Assert.Contains("max-height:400px", html);
            Assert.Contains("text-overflow:ellipsis", html);
        }

        [Fact]
        public void Generate_ReportAvoidsBannedOverclaimWords()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));
            string lower = html.ToLowerInvariant();

            Assert.DoesNotContain("certified", lower);
            Assert.DoesNotContain("approved", lower);
            Assert.DoesNotContain("guaranteed", lower);
            Assert.DoesNotContain("legally compliant", lower);
            Assert.DoesNotContain("final compliance", lower);
            Assert.DoesNotContain("docker", lower);
            Assert.DoesNotContain("backend", lower);
        }

        [Fact]
        public void SemanticClassifier_PrioritizesGroundingOverTechnologyAndMechanical()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Technology/Data/Voice/CATV/CCTV/MATV Equipment Grounding",
                "Communication Devices",
                RequirementDiscipline.Technology);

            Assert.Equal("grounding_bonding_conductors", profile.RequirementType);
            Assert.Contains("ground", string.Join(" ", profile.TriggerKeywords));
            Assert.NotEqual("technology_low_voltage_security_fire_alarm", profile.RequirementType);
        }

        [Fact]
        public void SemanticClassifier_PrioritizesHoseBibbRpzOverLevel()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "RPZ at roof hose bibbs per roof zone",
                "Plumbing Fixtures",
                RequirementDiscipline.Plumbing);

            Assert.Equal("plumbing_hose_bibb_rpz_valves", profile.RequirementType);
            Assert.False(profile.FallbackAllowed);
            Assert.Contains("Plumbing", string.Join(" ", profile.AllowedCategories));
        }

        [Fact]
        public void SemanticClassifier_PrioritizesIdentificationAndSpecOverCategoryPresence()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Provide identification, label, and manufacturer submittal data",
                "Electrical Equipment",
                RequirementDiscipline.Electrical);

            Assert.Equal("manufacturer_brand_restriction", profile.RequirementType);
            Assert.Contains("manufacturer", string.Join(" ", profile.TriggerKeywords));
        }

        [Fact]
        public void SemanticClassifier_PrioritizesDemolitionOverOutletPresence()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Abandoned outlets to remain protected and removed per field notes",
                "Electrical Fixtures",
                RequirementDiscipline.Electrical);

            Assert.Equal("field_execution_demolition_protection", profile.RequirementType);
            Assert.False(profile.FallbackAllowed);
        }

        [Fact]
        public void Engine_UsesSemanticPriorityAndAvoidsFullModelFallbackForHighRiskRows()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> requirements = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-142",
                    RowNumber = 142,
                    SourceSheet = "Technology",
                    Discipline = "Technology",
                    RequirementText = "DATA / VOICE COMMUNICATIONS CLOSET GROUND BAR",
                    Category = "Communication Devices",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-600",
                    RowNumber = 600,
                    SourceSheet = "Plumbing",
                    Discipline = "Plumbing",
                    RequirementText = "RPZ at roof hose bibbs per roof zone",
                    Category = "Plumbing Fixtures",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-601",
                    RowNumber = 601,
                    SourceSheet = "Plumbing",
                    Discipline = "Plumbing",
                    RequirementText = "Provide shut off valve for hose bibbs",
                    Category = "Plumbing Fixtures",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-149",
                    RowNumber = 149,
                    SourceSheet = "Electrical",
                    Discipline = "Electrical",
                    RequirementText = "GROUNDING AND BONDING / do not fasten / underground connectors",
                    Category = "Electrical Equipment",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                }
            };

            List<ExportElementRecord> modelRecords = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 1001,
                    UniqueId = "tech-1001",
                    Category = "Communication Devices",
                    Family = "Tech Device",
                    Type = "Low Voltage Device",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Level 1" }
                    }
                },
                new ExportElementRecord
                {
                    ElementId = 2001,
                    UniqueId = "plumb-2001",
                    Category = "Plumbing Fixtures",
                    Family = "Roof Mount Hose Bibb",
                    Type = "Hose Bibb",
                    Level = "Roof",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Roof" }
                    }
                },
                new ExportElementRecord
                {
                    ElementId = 3001,
                    UniqueId = "mech-3001",
                    Category = "Mechanical Equipment",
                    Family = "RTU",
                    Type = "RTU Type",
                    Level = "Roof",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Roof" }
                    }
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(requirements, modelRecords, RequirementDiscipline.All);

            RequirementCheckResult grounding = results.First(result => result.RequirementId == "REQ-142");
            RequirementCheckResult plumbing = results.First(result => result.RequirementId == "REQ-600");
            RequirementCheckResult valve = results.First(result => result.RequirementId == "REQ-601");
            RequirementCheckResult bond = results.First(result => result.RequirementId == "REQ-149");

            Assert.Equal("grounding_bonding_conductors", grounding.RequirementType);
            Assert.Equal("plumbing_hose_bibb_rpz_valves", plumbing.RequirementType);
            Assert.Equal("plumbing_hose_bibb_rpz_valves", valve.RequirementType);
            Assert.Equal("grounding_bonding_conductors", bond.RequirementType);
            Assert.NotEqual(RequirementCheckStatus.Met, grounding.Status);
            Assert.NotEqual(RequirementCheckStatus.Met, plumbing.Status);
            Assert.NotEqual(RequirementCheckStatus.Met, valve.Status);
            Assert.NotEqual(RequirementCheckStatus.Met, bond.Status);
            Assert.True(grounding.FilterTrace != null && grounding.FilterTrace.FallbackAllowed == false);
            Assert.True(plumbing.FilterTrace != null && plumbing.FilterTrace.FallbackAllowed == false);
            Assert.True(valve.FilterTrace != null && valve.FilterTrace.FallbackAllowed == false);
            Assert.True(bond.FilterTrace != null && bond.FilterTrace.FallbackAllowed == false);
            Assert.True(grounding.FilterTrace != null && grounding.FilterTrace.CandidateStages.Count > 0);
            Assert.True(plumbing.FilterTrace != null && plumbing.FilterTrace.CandidateStages.Count > 0);
            Assert.True(valve.FilterTrace != null && valve.FilterTrace.CandidateStages.Count > 0);
            Assert.True(bond.FilterTrace != null && bond.FilterTrace.CandidateStages.Count > 0);
            Assert.DoesNotContain("marked as Met", grounding.Reasoning ?? string.Empty);
            Assert.DoesNotContain("No action required", grounding.NextBestAction ?? string.Empty);
            Assert.DoesNotContain("marked as Met", plumbing.Reasoning ?? string.Empty);
            Assert.DoesNotContain("No action required", plumbing.NextBestAction ?? string.Empty);
            Assert.DoesNotContain("marked as Met", valve.Reasoning ?? string.Empty);
            Assert.DoesNotContain("No action required", valve.NextBestAction ?? string.Empty);
            Assert.DoesNotContain("marked as Met", bond.Reasoning ?? string.Empty);
            Assert.DoesNotContain("No action required", bond.NextBestAction ?? string.Empty);
        }

        [Fact]
        public void Guardrail_DowngradesWeakMetAndRewritesNarrative()
        {
            RequirementCheckResult result = new RequirementCheckResult
            {
                Status = RequirementCheckStatus.Met,
                EvidenceAlignment = EvidenceAlignmentLevel.Weak,
                Confidence = 1.0,
                Reasoning = "The deterministic check found matching evidence, so it is marked as Met.",
                NextBestAction = "No action required for this requirement.",
                ModelEvidenceLimitations = "No major limitations were detected in the current pass.",
                ExpectedParameters = new List<string> { "Ground Wire" },
                Evidence = new List<string> { "generic category + level only" },
                IssueTitle = "Grounding and bonding conductors"
            };

            typeof(RequirementComparisonEngine)
                .GetMethod("ApplySemanticGuardrail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { result, "Technology/Data/Voice/CATV/CCTV/MATV Equipment Grounding", null, null });

            Assert.Equal(RequirementCheckStatus.NeedsHumanReview, result.Status);
            Assert.True(result.Confidence < 1.0);
            Assert.DoesNotContain("marked as Met", result.Reasoning ?? string.Empty);
            Assert.DoesNotContain("No action required", result.NextBestAction ?? string.Empty);
            Assert.DoesNotContain("No major limitations", result.ModelEvidenceLimitations ?? string.Empty);
        }

        [Theory]
        [InlineData(EvidenceAlignmentLevel.Weak)]
        [InlineData(EvidenceAlignmentLevel.MismatchRisk)]
        [InlineData(EvidenceAlignmentLevel.ManualOnly)]
        public void Guardrail_PreventsMetWhenEvidenceIsWeakOrManual(EvidenceAlignmentLevel alignment)
        {
            RequirementCheckResult result = new RequirementCheckResult
            {
                Status = RequirementCheckStatus.Met,
                EvidenceAlignment = alignment,
                Confidence = 0.99,
                Reasoning = "The deterministic check found matching evidence, so it is marked as Met.",
                NextBestAction = "No action required for this requirement.",
                IssueTitle = "Identification and labeling"
            };

            typeof(RequirementComparisonEngine)
                .GetMethod("ApplySemanticGuardrail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { result, "Provide identification, label, and manufacturer submittal data", null, null });

            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.True(result.Confidence < 1.0);
            Assert.DoesNotContain("marked as Met", result.Reasoning ?? string.Empty);
        }

        [Fact]
        public void Generate_HiddenJsonIncludesRequirementTypeAndCandidateScopeFields()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));
            string json = ExtractMachineReadableJson(html);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement first = document.RootElement.GetProperty("requirement_results")[0];
            Assert.True(first.TryGetProperty("requirement_type", out _));
            Assert.True(first.TryGetProperty("requirement_type_reason", out _));
            Assert.True(first.TryGetProperty("candidate_scope", out _));
            Assert.True(first.TryGetProperty("allowed_categories", out _));
            Assert.True(first.TryGetProperty("excluded_categories", out _));
            Assert.True(first.TryGetProperty("candidate_scope_reason", out _));
            Assert.True(first.TryGetProperty("fallback_used", out _));
            Assert.True(first.TryGetProperty("fallback_allowed", out _));
            Assert.True(first.TryGetProperty("model_evidence_sufficiency", out _));
            Assert.True(first.TryGetProperty("why_not_model_closeable", out _));
            Assert.True(first.TryGetProperty("filter_trace", out JsonElement filterTrace));
            Assert.True(filterTrace.TryGetProperty("requirement_type", out _));
            Assert.True(filterTrace.TryGetProperty("candidate_scope_reason", out _));
            Assert.True(filterTrace.TryGetProperty("fallback_allowed", out _));

            JsonElement keyIssue = document.RootElement.GetProperty("key_issues")[0];
            Assert.True(keyIssue.TryGetProperty("key_issue_score", out _));
            Assert.True(keyIssue.TryGetProperty("urgency", out _));
            Assert.True(keyIssue.TryGetProperty("urgency_reason", out _));
            Assert.True(keyIssue.TryGetProperty("score_factors", out JsonElement scoreFactors));
            Assert.True(scoreFactors.TryGetProperty("status_severity_score", out _));
            Assert.True(scoreFactors.TryGetProperty("deliverable_impact_score", out _));
            Assert.True(scoreFactors.TryGetProperty("actionability_score", out _));
            Assert.True(scoreFactors.TryGetProperty("evidence_gap_score", out _));
            Assert.True(scoreFactors.TryGetProperty("requirement_type_risk_score", out _));
            Assert.True(scoreFactors.TryGetProperty("impact_scale_score", out _));
            Assert.True(keyIssue.TryGetProperty("candidate_scope_valid", out _));
            Assert.True(keyIssue.TryGetProperty("full_model_fallback_used", out _));
        }

        [Fact]
        public void Generate_DoesNotRenderMetContradictionsForNonMetStatuses()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-GUARD",
                    SourceSheet = "Electrical",
                    RowNumber = 149,
                    Discipline = "Electrical",
                    RequirementText = "GROUNDING AND BONDING / do not fasten / underground connectors",
                    Category = "Electrical Equipment"
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(rows, new List<ExportElementRecord>(), RequirementDiscipline.All);
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));
            string visible = System.Text.RegularExpressions.Regex.Replace(html, "<script[\\s\\S]*?</script>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            Assert.DoesNotContain("marked as Met", visible);
            Assert.DoesNotContain("No action required for this requirement", visible);
            Assert.DoesNotContain("No major limitations detected", visible);
        }

        private static string ExtractMachineReadableJson(string html)
        {
            const string startMarker = "<script type=\"application/json\" id=\"ema-ai-report-context\">";
            const string endMarker = "</script>";
            int start = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
            Assert.True(start >= 0, "Expected hidden machine-readable JSON block.");
            start += startMarker.Length;
            int end = html.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
            Assert.True(end > start, "Expected closing script tag for machine-readable JSON block.");
            return html.Substring(start, end - start);
        }

        [Fact]
        public void SemanticClassifier_ClassifiesDdcEmcsAsControlsType()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Venturi meters tied to DDC",
                "Mechanical Equipment",
                RequirementDiscipline.Mechanical);

            Assert.Equal("mechanical_controls_ddc_emcs", profile.RequirementType);
            Assert.False(profile.AllowsModelOnlyMet);
        }

        [Fact]
        public void SemanticClassifier_ClassifiesEmcsControlAsControlsType()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Control small restroom exhaust fan with lights. Control large gang restroom exhaust fan with EMCS.",
                "Mechanical Equipment",
                RequirementDiscipline.Mechanical);

            Assert.Equal("mechanical_controls_ddc_emcs", profile.RequirementType);
            Assert.False(profile.AllowsModelOnlyMet);
        }

        [Fact]
        public void SemanticClassifier_ClassifiesLightingControlAsLightingControlScheme()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Provide occupancy sensor in each classroom with switchpack control",
                "Electrical Fixtures",
                RequirementDiscipline.Lighting);

            Assert.Equal("lighting_control_scheme", profile.RequirementType);
            Assert.False(profile.AllowsModelOnlyMet);
        }

        [Fact]
        public void SemanticClassifier_ClassifiesOmManualAsOperationMaintenanceManual()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "O&M Manual sections: content for each unit with wiring diagrams",
                "Technology",
                RequirementDiscipline.Technology);

            Assert.Equal("operation_maintenance_manual", profile.RequirementType);
            Assert.False(profile.AllowsModelOnlyMet);
        }

        [Fact]
        public void SemanticClassifier_ClassifiesAtticStockAsAtticStockType()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Keyed switches attic stock zero",
                "Electrical Fixtures",
                RequirementDiscipline.Electrical);

            Assert.Equal("attic_stock_spare_parts", profile.RequirementType);
            Assert.False(profile.AllowsModelOnlyMet);
        }

        [Fact]
        public void SemanticClassifier_ClassifiesBackfillAsFieldExecution()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Backfill with stabilized sand compacting to 95%",
                "Plumbing Fixtures",
                RequirementDiscipline.Plumbing);

            Assert.Equal("field_execution_demolition_protection", profile.RequirementType);
            Assert.False(profile.AllowsModelOnlyMet);
        }

        [Fact]
        public void Engine_NonModelCloseableTypesNeverGetMetStatus()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> requirements = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-DDC",
                    RowNumber = 485,
                    SourceSheet = "Mechanical",
                    Discipline = "Mechanical",
                    RequirementText = "Venturi meters tied to DDC",
                    Category = "Mechanical Equipment"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-EMCS",
                    RowNumber = 480,
                    SourceSheet = "Mechanical",
                    Discipline = "Mechanical",
                    RequirementText = "Control small restroom exhaust fan with lights. Control large gang restroom exhaust fan with EMCS.",
                    Category = "Mechanical Equipment"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-LTGCTRL",
                    RowNumber = 403,
                    SourceSheet = "Lighting",
                    Discipline = "Lighting",
                    RequirementText = "Provide occupancy sensor in each classroom with switchpack for lighting control",
                    Category = "Lighting Fixtures"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-OM",
                    RowNumber = 72,
                    SourceSheet = "Technology",
                    Discipline = "Technology",
                    RequirementText = "O&M Manual sections: content for each unit with wiring diagrams",
                    Category = "Technology"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-ATTIC",
                    RowNumber = 58,
                    SourceSheet = "Electrical",
                    Discipline = "Electrical",
                    RequirementText = "Keyed switches attic stock zero",
                    Category = "Electrical Fixtures"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-BACKFILL",
                    RowNumber = 608,
                    SourceSheet = "Plumbing",
                    Discipline = "Plumbing",
                    RequirementText = "Backfill with stabilized sand compacting to 95%",
                    Category = "Plumbing Fixtures"
                }
            };

            List<ExportElementRecord> modelRecords = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 5001,
                    UniqueId = "mech-5001",
                    Category = "Mechanical Equipment",
                    Family = "Exhaust Fan",
                    Type = "Exhaust Fan Type",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Level 1" }
                    }
                },
                new ExportElementRecord
                {
                    ElementId = 5002,
                    UniqueId = "ltg-5002",
                    Category = "Lighting Fixtures",
                    Family = "LED 2x4",
                    Type = "LED Fixture",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Level 1" }
                    }
                },
                new ExportElementRecord
                {
                    ElementId = 5003,
                    UniqueId = "elec-5003",
                    Category = "Electrical Fixtures",
                    Family = "Switch",
                    Type = "Keyed Switch",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Level 1" }
                    }
                },
                new ExportElementRecord
                {
                    ElementId = 5004,
                    UniqueId = "plumb-5004",
                    Category = "Plumbing Fixtures",
                    Family = "Floor Drain",
                    Type = "FD-1",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Level 1" }
                    }
                },
                new ExportElementRecord
                {
                    ElementId = 5005,
                    UniqueId = "comm-5005",
                    Category = "Communication Devices",
                    Family = "Data Outlet",
                    Type = "Data Device",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Level 1" }
                    }
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(requirements, modelRecords, RequirementDiscipline.All);

            foreach (RequirementCheckResult result in results)
            {
                Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
                Assert.DoesNotContain("No action required", result.NextBestAction ?? string.Empty);
            }
        }

        [Fact]
        public void Engine_ManufacturerRequirementsNeverGetMetFromCategoryAlone()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> requirements = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-MFG",
                    RowNumber = 478,
                    SourceSheet = "Mechanical",
                    Discipline = "Mechanical",
                    RequirementText = "Acceptable manufacturers: Aaon HVAC equipment. ALT BID: Lennox then Trane.",
                    Category = "Manufacturers"
                }
            };

            List<ExportElementRecord> modelRecords = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 6001,
                    UniqueId = "mech-6001",
                    Category = "Mechanical Equipment",
                    Family = "Aaon RTU",
                    Type = "RTU 3 Ton",
                    Level = "Roof",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Roof" }
                    }
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(requirements, modelRecords, RequirementDiscipline.All);
            RequirementCheckResult result = results[0];

            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Equal("manufacturer_brand_restriction", result.RequirementType);
        }

        [Fact]
        public void Guardrail_CapsEvidenceAtPartialWhenModelOnlyMetBlocked()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> requirements = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-DDC-GUARD",
                    RowNumber = 485,
                    SourceSheet = "Mechanical",
                    Discipline = "Mechanical",
                    RequirementText = "Venturi meters tied to DDC",
                    Category = "Mechanical Equipment"
                }
            };

            List<ExportElementRecord> modelRecords = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 8001,
                    UniqueId = "mech-8001",
                    Category = "Mechanical Equipment",
                    Family = "Venturi Meter",
                    Type = "VM-1",
                    Level = "Roof",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Roof" }
                    }
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(requirements, modelRecords, RequirementDiscipline.All);
            RequirementCheckResult result = results[0];

            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Equal("mechanical_controls_ddc_emcs", result.RequirementType);
            Assert.NotEqual(EvidenceAlignmentLevel.Strong, result.EvidenceAlignment);
        }

        [Fact]
        public void Engine_CanaryRowsRemainCorrectAfterSemanticHardening()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> canaryRows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-22",
                    RowNumber = 22,
                    SourceSheet = "Electrical",
                    Discipline = "Electrical",
                    RequirementText = "Provide dedicated circuit for soap dispenser outlet",
                    Category = "Electrical Fixtures"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-133",
                    RowNumber = 133,
                    SourceSheet = "Electrical",
                    Discipline = "Electrical",
                    RequirementText = "Insulated ground conductor in all conduit systems",
                    Category = "Electrical Equipment"
                },
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-600",
                    RowNumber = 600,
                    SourceSheet = "Plumbing",
                    Discipline = "Plumbing",
                    RequirementText = "RPZ at roof hose bibbs per roof zone within 200-foot radius",
                    Category = "Plumbing Fixtures"
                }
            };

            List<ExportElementRecord> modelRecords = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 7001,
                    UniqueId = "elec-7001",
                    Category = "Electrical Fixtures",
                    Family = "Duplex Receptacle",
                    Type = "GP-120V",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Level 1" },
                        ["Voltage"] = new ParameterRecord { ValueString = "120V" }
                    }
                },
                new ExportElementRecord
                {
                    ElementId = 7002,
                    UniqueId = "plumb-7002",
                    Category = "Plumbing Fixtures",
                    Family = "Roof Mount Hose Bibb",
                    Type = "Hose Bibb",
                    Level = "Roof",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        ["Level"] = new ParameterRecord { ValueString = "Roof" }
                    }
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(canaryRows, modelRecords, RequirementDiscipline.All);

            foreach (RequirementCheckResult result in results)
            {
                Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
                Assert.DoesNotContain("No action required", result.NextBestAction ?? string.Empty);
            }
        }

        // ----------------------------------------------------------------
        // Schedule Console + Ask EMA AI tab structure tests
        // ----------------------------------------------------------------

        [Fact]
        public void Generate_HasConsoleTabNavigation()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("console-tab-nav", html);
            Assert.Contains("data-tab=\"summary\"", html);
            Assert.Contains("data-tab=\"requirements\"", html);
            Assert.Contains("data-tab=\"evidence\"", html);
            Assert.Contains("data-tab=\"elements\"", html);
            Assert.Contains("data-tab=\"rules\"", html);
            Assert.Contains("data-tab=\"exports\"", html);
            Assert.Contains("data-tab=\"ask\"", html);
        }

        [Fact]
        public void Generate_HasRequirementsScheduleGrid()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("requirements-schedule-table", html);
            Assert.Contains("schedule-tbody", html);
            Assert.Contains("schedule-search", html);
            Assert.Contains("schedule-group-by", html);
            Assert.Contains("schedule-discipline", html);
            Assert.Contains("schedule-status", html);
            Assert.Contains("schedule-urgency", html);
            Assert.Contains("schedule-pagination", html);
        }

        [Fact]
        public void Generate_HasAskEmaAiPanelWithModelSelector()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("ask-model-select", html);
            Assert.Contains("ask-context-scope", html);
            Assert.Contains("ask-input", html);
            Assert.Contains("ask-btn", html);
            Assert.Contains("ask-answer-area", html);
            Assert.Contains("deterministic", html);
            Assert.Contains("ollama/qwen3.6:35b", html);
            Assert.Contains("openrouter/anthropic/claude-sonnet-4", html);
        }

        [Fact]
        public void Generate_HasAllSixTabPanels()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("id=\"tab-summary\"", html);
            Assert.Contains("id=\"tab-requirements\"", html);
            Assert.Contains("id=\"tab-evidence\"", html);
            Assert.Contains("id=\"tab-elements\"", html);
            Assert.Contains("id=\"tab-rules\"", html);
            Assert.Contains("id=\"tab-exports\"", html);
            Assert.Contains("id=\"tab-ask\"", html);
        }

        [Fact]
        public void Generate_HasExportCenterCards()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("export-grid", html);
            Assert.Contains("export-card", html);
            Assert.Contains("csv-requirements", html);
            Assert.Contains("csv-notmet", html);
            Assert.Contains("csv-missing", html);
            Assert.Contains("csv-elements", html);
        }

        [Fact]
        public void Generate_HasEvidenceBrowserPanel()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("evidence-view-btn", html);
            Assert.Contains("evidence-table", html);
            Assert.Contains("evidence-tbody", html);
            Assert.Contains("By Parameter", html);
            Assert.Contains("By Rule Type", html);
        }

        [Fact]
        public void Generate_HasAskEmaAiSuggestedQuestions()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("ask-suggested-btn", html);
            Assert.Contains("Why is Row 478 Not Met?", html);
            Assert.Contains("Summarize Mechanical Not Met requirements", html);
            Assert.Contains("Can this be closed from model data?", html);
        }

        [Fact]
        public void Generate_HasConsoleHeaderAndToolbar()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("console-header", html);
            Assert.Contains("console-toolbar", html);
            Assert.Contains("toolbar-btn", html);
            Assert.Contains("console-shell", html);
            Assert.Contains("EMA AI", html);
        }

        [Fact]
        public void Generate_HeaderMetadataIsReadableAndTabsArePresent()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("<span class=\"ch-label\">Project:</span>", html);
            Assert.Contains("<span class=\"ch-label\">Generated:</span>", html);
            Assert.Contains("<span class=\"ch-label\">Requirements:</span>", html);
            Assert.Contains("<span class=\"ch-label\">Elements:</span>", html);
            Assert.Contains("console-identity-chips", html);
            Assert.Contains("tab-btn tab-btn-ai", html);
            Assert.Contains("Ask EMA AI", html);
            Assert.Contains("ask-references", html);
            Assert.Contains("ask-selected-context", html);
            Assert.Contains("ask-model-select", html);
        }

        [Fact]
        public void Generate_DeterministicRagScaffoldInScript()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests");
            Directory.CreateDirectory(tempFolder);

            List<RequirementCheckResult> results = BuildSampleResults();
            RequirementCheckReport report = BuildReport(
                RequirementDiscipline.All,
                results,
                BuildDisciplineSummaries(results),
                BuildKeyIssues(results, RequirementDiscipline.All),
                BuildFilterContext("All Disciplines", results, RequirementDiscipline.All));
            report.OutputFolder = tempFolder;

            string html = File.ReadAllText(OwnerRequirementHtmlReportGenerator.Generate(report));

            Assert.Contains("deterministicAnswer", html);
            Assert.Contains("buildIndex", html);
            Assert.Contains("retrieveRows", html);
            Assert.Contains("emaAiSendCommand", html);
            Assert.Contains("first-pass evidence review", html);
        }
    }
}
