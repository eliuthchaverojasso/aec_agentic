using System;
using System.IO;
using EMAExtractor.Models;
using EMAExtractor.Services;
using Xunit;

namespace EMAExtractor.Tests
{
    public class ReportRagServiceTests
    {
        [Fact]
        public void LoadFromHtmlFile_LoadsHiddenJsonAndSetsReportDataState()
        {
            string htmlPath = WriteTempReportHtml(
                @"{
                    ""report_metadata"": {
                        ""project_name"": ""MEP-NISD-MIDDLE SCHOOL 8"",
                        ""model_element_count"": 21868,
                        ""total_requirements"": 3
                    },
                    ""summary_counts"": {
                        ""met"": 1,
                        ""not_met"": 1,
                        ""needs_human_review"": 1,
                        ""insufficient_model_data"": 0,
                        ""not_applicable"": 0
                    },
                    ""requirements"": [
                        {""source_row"": 539, ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_flush_valve_product_spec"", ""requirement_text"": ""SLOAN ROYAL flush valve"", ""next_best_action"": ""Review the specification and populate missing manufacturer data."" },
                        {""source_row"": 606, ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_support_hanger_requirement"", ""requirement_text"": ""Provide P-trap with clevis hanger"", ""next_best_action"": ""Review the hanger detail and populate hanger type, spacing, and comments."" },
                        {""source_row"": 548, ""status"": ""NeedsHumanReview"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_accessory_water_supply"", ""requirement_text"": ""Soap dispenser CW"" }
                    ],
                    ""key_issues"": [
                        {""source_row"": 606, ""issue_title"": ""P-trap with clevis hanger"", ""severity"": ""High""}
                    ]
                }");

            try
            {
                var service = new ReportRagService();

                bool loaded = service.LoadFromHtmlFile(htmlPath);

                Assert.True(loaded);
                Assert.True(service.HasReportData);
                Assert.Equal(ReportDataState.ReportDataLoaded, service.DataState);
                Assert.Equal(3, service.RequirementCount);
                Assert.Equal(21868, service.ModelElementCount);
                Assert.Contains("Report data loaded", service.BuildReportDataStatusMessage());
                Assert.DoesNotContain("No report data loaded", service.BuildReportDataStatusMessage());
            }
            finally
            {
                SafeDelete(htmlPath);
            }
        }

        [Fact]
        public void LoadFromHtmlFile_MalformedEmbeddedJsonSetsParseFailedState()
        {
            string htmlPath = WriteTempReportHtml("<html><body><script id=\"ema-ai-report-context\">not json</script></body></html>");

            try
            {
                var service = new ReportRagService();

                bool loaded = service.LoadFromHtmlFile(htmlPath);

                Assert.False(loaded);
                Assert.False(service.HasReportData);
                Assert.True(service.HasParseFailed);
                Assert.Equal(ReportDataState.ReportDataParseFailed, service.DataState);
                Assert.Contains("could not be parsed", service.BuildReportDataStatusMessage(), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                SafeDelete(htmlPath);
            }
        }

        [Fact]
        public void Query_WhatIsNotMet_ReturnsGroundedSummary()
        {
            string htmlPath = WriteTempReportHtml(
                @"{
                    ""requirements"": [
                        {""source_row"": 539, ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_flush_valve_product_spec"", ""requirement_text"": ""SLOAN ROYAL flush valve"", ""missing_direct_evidence"": [""Manufacturer"", ""Model number""], ""next_best_action"": ""Review the specification and populate missing manufacturer data."" },
                        {""source_row"": 606, ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_support_hanger_requirement"", ""requirement_text"": ""Provide P-trap with clevis hanger"", ""missing_direct_evidence"": [""Hanger Type"", ""Spacing""] }
                    ],
                    ""key_issues"": []
                }");

            try
            {
                var service = new ReportRagService();
                Assert.True(service.LoadFromHtmlFile(htmlPath));

                RagQueryResult result = service.Query("What is Not Met?");

                Assert.True(result.Success);
                Assert.Contains("Answer:", result.Answer);
                Assert.Contains("Not Met", result.Answer);
                Assert.Contains("Row 539", result.Answer);
                Assert.Contains("Row 606", result.Answer);
                Assert.Contains("References:", result.Answer);
                Assert.DoesNotContain("{\"requirements\"", result.Answer, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                SafeDelete(htmlPath);
            }
        }

        [Fact]
        public void Query_Row606_ReturnsRowReferenceAndFallbackMetadata()
        {
            string htmlPath = WriteTempReportHtml(
                @"{
                    ""requirements"": [
                        {""source_row"": 606, ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_support_hanger_requirement"", ""requirement_text"": ""Provide P-trap with clevis hanger on Level 1."", ""missing_direct_evidence"": [""Hanger Type"", ""Spacing""] }
                    ],
                    ""key_issues"": []
                }");

            try
            {
                var service = new ReportRagService();
                Assert.True(service.LoadFromHtmlFile(htmlPath));

                RagQueryResult result = service.Query("Tell me about row 606");

                Assert.True(result.Success);
                Assert.Contains("Row 606", result.Answer);
                Assert.Contains("plumbing_support_hanger_requirement", result.Answer);
                Assert.Contains("Requirement Text:", result.Answer);
                Assert.Contains("Missing Evidence:", result.Answer);
                Assert.Contains("References:", result.Answer);
                Assert.Contains("606", string.Join(",", result.SourceRows));
            }
            finally
            {
                SafeDelete(htmlPath);
            }
        }

        [Fact]
        public void Query_PlumbingRequirements_ReturnsPlumbingSummary()
        {
            string htmlPath = WriteTempReportHtml(
                @"{
                    ""requirements"": [
                        {""source_row"": 539, ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_flush_valve_product_spec"", ""requirement_text"": ""SLOAN ROYAL flush valve"" },
                        {""source_row"": 542, ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_water_hammer_arrestor_requirement"", ""requirement_text"": ""Water hammer arrestor"" },
                        {""source_row"": 548, ""status"": ""NeedsHumanReview"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_accessory_water_supply"", ""requirement_text"": ""Soap dispenser CW"" },
                        {""source_row"": 606, ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_support_hanger_requirement"", ""requirement_text"": ""Provide P-trap with clevis hanger"" }
                    ],
                    ""key_issues"": []
                }");

            try
            {
                var service = new ReportRagService();
                Assert.True(service.LoadFromHtmlFile(htmlPath));

                RagQueryResult result = service.Query("Plumbing requirements");

                Assert.True(result.Success);
                Assert.Contains("Plumbing", result.Answer);
                Assert.Contains("Rows:", result.Answer, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Row 539", result.Answer);
                Assert.Contains("Row 542", result.Answer);
                Assert.Contains("Row 548", result.Answer);
                Assert.Contains("Row 606", result.Answer);
            }
            finally
            {
                SafeDelete(htmlPath);
            }
        }

        private static string WriteTempReportHtml(string json)
        {
            string folder = Path.Combine(Path.GetTempPath(), "EMA_AI_Rag_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, "EMA_AI_Requirement_Check_Test_All_20260608_132154.html");
            string html = "<html><body><script id=\"ema-ai-report-context\" type=\"application/json\">" + json + "</script></body></html>";
            File.WriteAllText(path, html);
            return path;
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // Best-effort cleanup for temp test artifacts.
            }
        }
    }
}
