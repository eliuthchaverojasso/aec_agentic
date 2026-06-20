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
    /// <summary>
    /// Guards against the 516 MB report regression: full element ID lists were embedded
    /// up to nine times per requirement (HTML scroll lists, copy buttons, hidden JSON,
    /// AI lookup hints), and identical category-wide sweeps were presented as
    /// requirement-specific evidence. These tests pin the embed caps, the honesty
    /// metadata (totals + truncated flags), and the broad-match supporting-context label.
    /// </summary>
    public class ReportEvidenceEmbedCapTests
    {
        [Fact]
        public void Generate_CapsEmbeddedElementIdLists_ForBroadMatches()
        {
            RequirementCheckResult result = BuildResult("REQ-900", idCount: 5000, uniqueIdCount: 3000);
            string html = GenerateHtml(new List<RequirementCheckResult> { result });
            JsonElement requirement = GetFirstRequirement(html);

            Assert.Equal(EvidenceEmbedLimits.MaxElementIdsInJson, requirement.GetProperty("matched_element_ids").GetArrayLength());
            Assert.Equal(5000, requirement.GetProperty("matched_element_id_total").GetInt32());
            Assert.True(requirement.GetProperty("matched_element_ids_truncated").GetBoolean());

            Assert.Equal(EvidenceEmbedLimits.MaxUniqueIdsInJson, requirement.GetProperty("matched_unique_ids").GetArrayLength());
            Assert.Equal(3000, requirement.GetProperty("matched_unique_id_total").GetInt32());
            Assert.True(requirement.GetProperty("matched_unique_ids_truncated").GetBoolean());

            Assert.True(requirement.GetProperty("broad_category_match").GetBoolean());

            JsonElement hints = requirement.GetProperty("ai_lookup_hints");
            Assert.Equal(EvidenceEmbedLimits.MaxElementIdsInJson, hints.GetProperty("revit_element_ids").GetArrayLength());

            string copyText = requirement.GetProperty("element_id_copy_text").GetString();
            Assert.Equal(EvidenceEmbedLimits.MaxCopyElementIds, copyText.Split(';').Length);

            // The duplicate top-level filter_stages block (same data as filter_trace.candidate_stages)
            // must stay removed.
            Assert.False(requirement.TryGetProperty("filter_stages", out _));
            Assert.True(requirement.TryGetProperty("filter_trace", out _));
        }

        [Fact]
        public void Generate_RendersHonestTruncationLabels_InHtml()
        {
            RequirementCheckResult result = BuildResult("REQ-901", idCount: 5000, uniqueIdCount: 3000);
            string html = GenerateHtml(new List<RequirementCheckResult> { result });

            Assert.Contains("Broad category match: 5,000 elements", html);
            Assert.Contains("supporting context for review", html);
            Assert.Contains("Copy Revit Element IDs (first 500 of 5000)", html);
            Assert.Contains("View Revit Element IDs (first 100 of 5000)", html);
            Assert.Contains("+4900 more (capped to keep this report openable)", html);
            Assert.Contains("View Unique IDs (first 25 of 3000)", html);

            foreach (Match buttonMatch in Regex.Matches(html, "data-copy-element-ids=\"([^\"]*)\""))
            {
                int idCount = buttonMatch.Groups[1].Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.True(idCount <= EvidenceEmbedLimits.MaxCopyElementIds,
                    "Copy button embeds " + idCount + " IDs, above the cap of " + EvidenceEmbedLimits.MaxCopyElementIds + ".");
            }
        }

        [Fact]
        public void Generate_DoesNotTruncateOrFlag_SmallEvidenceLists()
        {
            RequirementCheckResult result = BuildResult("REQ-902", idCount: 3, uniqueIdCount: 3);
            string html = GenerateHtml(new List<RequirementCheckResult> { result });
            JsonElement requirement = GetFirstRequirement(html);

            Assert.Equal(3, requirement.GetProperty("matched_element_ids").GetArrayLength());
            Assert.Equal(3, requirement.GetProperty("matched_element_id_total").GetInt32());
            Assert.False(requirement.GetProperty("matched_element_ids_truncated").GetBoolean());
            Assert.False(requirement.GetProperty("matched_unique_ids_truncated").GetBoolean());
            Assert.False(requirement.GetProperty("broad_category_match").GetBoolean());

            Assert.DoesNotContain("Broad category match", html);
            Assert.Contains("View All Revit Element IDs (3)", html);
            Assert.DoesNotContain("capped to keep this report openable", html);
        }

        [Fact]
        public void Generate_MarksScopeInvalidMatch_AsSupportingContext_EvenBelowThreshold()
        {
            RequirementCheckResult result = BuildResult("REQ-903", idCount: 30, uniqueIdCount: 0);
            result.CandidateScopeValid = false;
            string html = GenerateHtml(new List<RequirementCheckResult> { result });

            Assert.Contains("Broad category match", html);
            Assert.Contains("not item-specific accepted evidence", html);
            Assert.True(GetFirstRequirement(html).GetProperty("broad_category_match").GetBoolean());
        }

        [Fact]
        public void Generate_ReportSizeStaysBounded_WhenManyRequirementsShareBroadMatches()
        {
            // Before the embed caps, 68 requirements sharing one ~19k-element category sweep
            // produced a 516 MB report. 100 requirements sharing a 5k-element list must now
            // stay far below that.
            List<long> sharedIds = Enumerable.Range(1, 5000).Select(i => 700000L + i).ToList();
            List<string> sharedUniqueIds = Enumerable.Range(1, 5000)
                .Select(i => "7cba2812-0ff4-4ec8-89d4-e5a9892eb19a-" + i.ToString("x8", CultureInfo.InvariantCulture))
                .ToList();

            List<RequirementCheckResult> results = Enumerable.Range(1, 100)
                .Select(i =>
                {
                    RequirementCheckResult result = BuildResult("REQ-B" + i.ToString(CultureInfo.InvariantCulture), idCount: 0, uniqueIdCount: 0);
                    result.MatchedElementIds = sharedIds;
                    result.MatchedUniqueIds = sharedUniqueIds;
                    result.MatchedModelElementCount = sharedIds.Count;
                    result.ElementIdCopyText = string.Join(";", sharedIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
                    return result;
                })
                .ToList();

            string path = GeneratePath(results);
            long sizeBytes = new FileInfo(path).Length;

            Assert.True(sizeBytes < 10L * 1024 * 1024,
                "Report is " + (sizeBytes / 1024.0 / 1024.0).ToString("F1", CultureInfo.InvariantCulture) +
                " MB for 100 broad-match requirements; the embed caps must keep it under 10 MB.");
        }

        private static string GenerateHtml(List<RequirementCheckResult> results)
        {
            return File.ReadAllText(GeneratePath(results));
        }

        private static string GeneratePath(List<RequirementCheckResult> results)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Cap_Tests");
            Directory.CreateDirectory(tempFolder);

            RequirementCheckReport report = new RequirementCheckReport
            {
                ProjectName = "MEP-NISD-MIDDLE SCHOOL 8",
                ModelName = "MEP-NISD-MIDDLE SCHOOL 8",
                RequirementsFileName = "NISD_Owner_Requirements.xlsx",
                Discipline = RequirementDiscipline.All,
                Scope = RequirementModelScope.EntireModel,
                GeneratedAt = new DateTime(2026, 6, 9, 9, 30, 0, DateTimeKind.Local),
                ModelElementCount = 21868,
                Results = results,
                Summary = RequirementCheckSummary.FromResults(results),
                OverallScore = ScoreCalculator.CalculateOverallScore(results),
                ReadinessScore = ScoreCalculator.CalculateReadiness(results, DateTime.Now).OverallScore,
                ReadinessLabel = ReadinessLabel.OnTrack,
                DisciplineSummaries = new List<DisciplineSummary>(),
                KeyIssues = KeyIssueRanker.RankIssues(results, RequirementDiscipline.All, 10),
                FilterContext = new ReportFilterContext
                {
                    ActiveDiscipline = "All Disciplines",
                    ActiveStatus = "All",
                    ActiveUrgency = "All",
                    FilteredResults = results,
                    FilteredKeyIssues = KeyIssueRanker.RankIssues(results, RequirementDiscipline.All, 10),
                    FilteredCounts = RequirementCheckSummary.FromResults(results),
                    FilteredScores = new ReportFilterScores
                    {
                        OverallScore = ScoreCalculator.CalculateOverallScore(results),
                        ReadinessScore = ScoreCalculator.CalculateReadiness(results, DateTime.Now).OverallScore,
                        DisciplineScore = ScoreCalculator.CalculateOverallScore(results),
                        ApplicableCount = results.Count,
                        TotalCount = results.Count,
                        KeyIssueCount = 0
                    },
                    SuggestedQuestions = new List<string> { "What are the top project-level issues?" }
                },
                OutputFolder = tempFolder
            };

            return OwnerRequirementHtmlReportGenerator.Generate(report);
        }

        private static JsonElement GetFirstRequirement(string html)
        {
            Match match = Regex.Match(
                html,
                "<script type=\"application/json\" id=\"ema-ai-report-context\">(.*?)</script>",
                RegexOptions.Singleline);
            Assert.True(match.Success, "Hidden machine-readable JSON block not found.");

            using JsonDocument document = JsonDocument.Parse(match.Groups[1].Value);
            return document.RootElement.GetProperty("requirement_results")[0].Clone();
        }

        private static RequirementCheckResult BuildResult(string requirementId, int idCount, int uniqueIdCount)
        {
            List<long> ids = Enumerable.Range(1, idCount).Select(i => 500000L + i).ToList();
            List<string> uniqueIds = Enumerable.Range(1, uniqueIdCount)
                .Select(i => "0f2a1f1e-0000-1111-2222-" + i.ToString("d12", CultureInfo.InvariantCulture))
                .ToList();

            return new RequirementCheckResult
            {
                Requirement = new OwnerRequirementRow
                {
                    RequirementId = requirementId,
                    SourceSheet = "Electrical",
                    RowNumber = 12,
                    RequirementText = "Provide panel and circuit assignment for lighting fixtures.",
                    Discipline = "Electrical",
                    SourceFile = "NORTHWEST ISD 06.02.2025.xlsx"
                },
                RequirementId = requirementId,
                RequirementText = "Provide panel and circuit assignment for lighting fixtures.",
                Discipline = "Electrical",
                SourceFile = "NORTHWEST ISD 06.02.2025.xlsx",
                Status = RequirementCheckStatus.NeedsHumanReview,
                Confidence = 0.55,
                IssueTitle = "Panel and circuit assignment",
                Reasoning = "Category-level evidence found; item-specific values still need review.",
                NextBestAction = "Assign panel and circuit values to the listed elements before the next deliverable.",
                ResponsibleRole = "Electrical",
                EvidenceSummary = ids.Count.ToString(CultureInfo.InvariantCulture) + " candidate element(s) inspected.",
                SourceWorksheet = "Electrical",
                SourceRow = 12,
                Evidence = new List<string> { ids.Count.ToString(CultureInfo.InvariantCulture) + " candidate element(s) inspected." },
                MatchedElementIds = ids,
                MatchedUniqueIds = uniqueIds,
                MatchedCategories = new List<string> { "Lighting Fixtures" },
                MatchedParameters = new List<string> { "Panel" },
                MissingEvidence = new List<string> { "Circuit Number" },
                ElementIdCopyText = string.Join(";", ids.Select(id => id.ToString(CultureInfo.InvariantCulture))),
                Urgency = "Needs Review",
                IsKeyIssue = true,
                MatchedModelElementCount = ids.Count,
                RequirementType = "panel_circuit_power",
                ValidationType = ValidationType.Model,
                HumanReviewNeeded = true
            };
        }
    }
}
