using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EMAExtractor.Requirements;
using EMAExtractor.Services;
using Xunit;

namespace EMAExtractor.Tests
{
    public class PlumbingRequirementClassifierTests
    {
        // ─── RequirementSemanticClassifier: new plumbing rules ──────────────────

        [Theory]
        [InlineData("SLOAN ROYAL flush valve, wall-mounted")]
        [InlineData("Provide flushometer flush valve on all water closets")]
        [InlineData("Flush valve: diaphragm type, 1.28 GPF")]
        public void Classifier_FlushValveText_ClassifiesAsFlushValveProductSpec(string text)
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(text, string.Empty, RequirementDiscipline.Plumbing);

            Assert.Equal("plumbing_flush_valve_product_spec", profile.RequirementType);
            Assert.Equal(ValidationType.Specification, profile.ValidationType);
            Assert.Contains("Plumbing Fixtures", profile.AllowedCategories);
            Assert.DoesNotContain("Electrical Fixtures", profile.AllowedCategories);
        }

        [Theory]
        [InlineData("Provide water hammer arrestors at quick-closing valves")]
        [InlineData("Water hammer arrestor required at each solenoid valve")]
        [InlineData("Install shock arrestor per ASSE 1019")]
        public void Classifier_WaterHammerText_ClassifiesAsWaterHammerArrestor(string text)
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(text, string.Empty, RequirementDiscipline.Plumbing);

            Assert.Equal("plumbing_water_hammer_arrestor_requirement", profile.RequirementType);
            Assert.Equal(ValidationType.Model, profile.ValidationType);
            Assert.DoesNotContain("Electrical Equipment", profile.AllowedCategories);
            Assert.DoesNotContain("Lighting Fixtures", profile.AllowedCategories);
        }

        [Theory]
        [InlineData("Soap dispenser ½\" CW line, provide cold water supply connection")]
        [InlineData("Eye wash station, ½\" cold water supply required")]
        [InlineData("Drinking fountain with ½\" CW line connection")]
        public void Classifier_SoapDispenserCwLineText_ClassifiesAsPlumbingAccessoryWaterSupply(string text)
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(text, string.Empty, RequirementDiscipline.Plumbing);

            Assert.Equal("plumbing_accessory_water_supply", profile.RequirementType);
            Assert.Equal(ValidationType.Model, profile.ValidationType);
            Assert.DoesNotContain("Electrical Equipment", profile.AllowedCategories);
        }

        [Theory]
        [InlineData("P-trap with clevis hanger at each floor drain")]
        [InlineData("Provide clevis hanger at 48\" o.c. for horizontal drain")]
        [InlineData("Pipe hanger: pipe support every 4 feet per plumbing spec")]
        [InlineData("Seismic hanger required for all plumbing mains")]
        public void Classifier_PTrapClevisHangerText_ClassifiesAsPlumbingSupportHanger(string text)
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(text, string.Empty, RequirementDiscipline.Plumbing);

            Assert.Equal("plumbing_support_hanger_requirement", profile.RequirementType);
            Assert.Equal(ValidationType.Drawing, profile.ValidationType);
        }

        [Fact]
        public void Classifier_PTrapWithLevel_DoesNotFallToLevelLocationPlacement()
        {
            // Row 606 regression: P-trap with clevis hanger must NOT fall to level_location_mounting_placement
            string text = "P-trap with clevis hanger - Level 1 corridor drain";
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(text, string.Empty, RequirementDiscipline.Plumbing);

            Assert.NotEqual("level_location_mounting_placement", profile.RequirementType);
            Assert.Equal("plumbing_support_hanger_requirement", profile.RequirementType);
        }

        [Fact]
        public void Classifier_SupportHanger_ExcludesElectricalCategories()
        {
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "P-trap with clevis hanger", string.Empty, RequirementDiscipline.Plumbing);

            Assert.Contains("Electrical Fixtures", profile.ExcludedCategories);
            Assert.Contains("Lighting Fixtures", profile.ExcludedCategories);
            Assert.Contains("Communication Devices", profile.ExcludedCategories);
            Assert.Contains("Conduits", profile.ExcludedCategories);
        }

        [Fact]
        public void Classifier_FlushValve_NotUnknownAmbiguous()
        {
            // Row 539 regression: SLOAN ROYAL must not be unknown_ambiguous
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "SLOAN ROYAL flush valve, 1.0 GPF, for wall-mounted water closet", string.Empty, RequirementDiscipline.Plumbing);

            Assert.NotEqual("unknown_ambiguous", profile.RequirementType);
            Assert.Equal("plumbing_flush_valve_product_spec", profile.RequirementType);
        }

        [Fact]
        public void Classifier_WaterHammerArrestor_NotUnknownAmbiguous()
        {
            // Row 542 regression
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Water hammer arrestor required", string.Empty, RequirementDiscipline.Plumbing);

            Assert.NotEqual("unknown_ambiguous", profile.RequirementType);
            Assert.Equal("plumbing_water_hammer_arrestor_requirement", profile.RequirementType);
        }

        [Fact]
        public void Classifier_SoapDispenserCwLine_NotUnknownAmbiguous()
        {
            // Row 548 regression
            RequirementSemanticProfile profile = RequirementSemanticClassifier.Classify(
                "Soap dispenser ½\" CW line", string.Empty, RequirementDiscipline.Plumbing);

            Assert.NotEqual("unknown_ambiguous", profile.RequirementType);
            Assert.Equal("plumbing_accessory_water_supply", profile.RequirementType);
        }

        [Fact]
        public void Classifier_PlumbingHangerPriority_HigherThanLevelLocationPlacement()
        {
            // Verify plumbing_support_hanger_requirement (93) beats level_location_mounting_placement (10)
            RequirementSemanticProfile hanger = RequirementSemanticClassifier.Classify(
                "P-trap with clevis hanger", string.Empty, RequirementDiscipline.All);
            RequirementSemanticProfile level = RequirementSemanticClassifier.Classify(
                "Equipment located at Level 3", string.Empty, RequirementDiscipline.All);

            Assert.Equal("plumbing_support_hanger_requirement", hanger.RequirementType);
            Assert.Equal("level_location_mounting_placement", level.RequirementType);
        }

        // ─── DeterministicFallbackProvider ────────────────────────────────────

        [Fact]
        public async Task DeterministicFallback_IsAlwaysAvailable()
        {
            var provider = new DeterministicFallbackProvider();
            Assert.True(provider.IsAvailable);
            Assert.Equal("Deterministic", provider.ProviderName);

            AiCompletionResult result = await provider.CompleteAsync("system", "user", 100, CancellationToken.None);
            Assert.True(result.Success);
            Assert.True(result.UsedFallback);
            Assert.False(string.IsNullOrWhiteSpace(result.Content));
        }

        [Theory]
        [InlineData("classify this: flush valve requirement", "plumbing_flush_valve_product_spec")]
        [InlineData("classify this: water hammer arrestor requirement", "plumbing_water_hammer_arrestor_requirement")]
        [InlineData("classify this: clevis hanger requirement", "plumbing_support_hanger_requirement")]
        [InlineData("classify this: grounding electrode conductor requirement", "grounding_bonding_conductors")]
        public async Task DeterministicFallback_ReturnsRelevantAnswerForClassifyQuery(string prompt, string expectedKeyword)
        {
            var provider = new DeterministicFallbackProvider();
            AiCompletionResult result = await provider.CompleteAsync("system", prompt, 200);

            Assert.True(result.Success);
            Assert.Contains(expectedKeyword, result.Content);
        }

        // ─── RequirementAiClassifierService ──────────────────────────────────

        [Fact]
        public async Task AiClassifier_DeterministicPath_WinsForKnownType()
        {
            var service = new RequirementAiClassifierService(new DeterministicFallbackProvider());
            var row = new OwnerRequirementRow
            {
                RequirementText = "P-trap with clevis hanger at all floor drains",
                Discipline = "Plumbing"
            };

            AiClassificationResult result = await service.ClassifyAsync(row);

            Assert.Equal("plumbing_support_hanger_requirement", result.RequirementType);
            Assert.False(result.UsedAi);
            Assert.Equal("Deterministic", result.ProviderName);
            Assert.InRange(result.Confidence, 0.8, 1.0);
        }

        [Fact]
        public async Task AiClassifier_FallbackProvider_HandlesUnknownAmbiguous()
        {
            var service = new RequirementAiClassifierService(new DeterministicFallbackProvider());
            var row = new OwnerRequirementRow
            {
                RequirementText = "Xyzzy frob the wibble",
                Discipline = "Unknown"
            };

            AiClassificationResult result = await service.ClassifyAsync(row);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.RequirementType));
        }

        // ─── RequirementMatrixAuditService ───────────────────────────────────

        [Fact]
        public void MatrixAudit_CountsUnknownAmbiguous()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("plumbing_flush_valve_product_spec"),
                MakeResult("unknown_ambiguous"),
                MakeResult("unknown_ambiguous"),
                MakeResult("grounding_bonding_conductors")
            };

            MatrixAuditReport report = RequirementMatrixAuditService.Audit(results);

            Assert.Equal(4, report.TotalRequirements);
            Assert.Equal(2, report.UnknownAmbiguousCount);
            Assert.Equal(2, report.KnownTypeCount);
        }

        [Fact]
        public void MatrixAudit_FlagsScopeWarningForLevelLocationFallback()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("level_location_mounting_placement"),
                MakeResult("plumbing_support_hanger_requirement")
            };

            MatrixAuditReport report = RequirementMatrixAuditService.Audit(results);

            Assert.Equal(1, report.ScopeWarningCount);
            MatrixAuditEntry warn = report.ScopeWarningEntries[0];
            Assert.Contains("level_location_mounting_placement", warn.AssignedType);
            Assert.False(string.IsNullOrWhiteSpace(warn.ScopeWarningReason));
        }

        [Fact]
        public void MatrixAudit_TaxonomyCoveragePercent_CorrectForAllKnown()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("plumbing_flush_valve_product_spec"),
                MakeResult("grounding_bonding_conductors"),
                MakeResult("panel_circuit_power")
            };

            MatrixAuditReport report = RequirementMatrixAuditService.Audit(results);

            Assert.Equal(100.0, report.TaxonomyCoveragePercent);
        }

        [Fact]
        public void MatrixAudit_TypeDistribution_CountsEachType()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("plumbing_flush_valve_product_spec"),
                MakeResult("plumbing_flush_valve_product_spec"),
                MakeResult("grounding_bonding_conductors")
            };

            MatrixAuditReport report = RequirementMatrixAuditService.Audit(results);

            Assert.Equal(2, report.TypeDistribution["plumbing_flush_valve_product_spec"]);
            Assert.Equal(1, report.TypeDistribution["grounding_bonding_conductors"]);
        }

        // ─── ReportRagService ─────────────────────────────────────────────────

        [Fact]
        public void RagService_UnloadedState_ReturnsErrorAnswer()
        {
            var service = new ReportRagService();
            RagQueryResult result = service.Query("how many requirements?");

            Assert.False(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.Answer));
        }

        [Fact]
        public void RagService_LoadFromJson_PopulatesData()
        {
            string json = @"{
                ""requirements"": [
                    {""requirementId"": ""539"", ""status"": ""NeedsHumanReview"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_flush_valve_product_spec"", ""requirementText"": ""SLOAN ROYAL""},
                    {""requirementId"": ""542"", ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_water_hammer_arrestor_requirement"", ""requirementText"": ""Water hammer arrestor""},
                    {""requirementId"": ""548"", ""status"": ""NeedsHumanReview"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_accessory_water_supply"", ""requirementText"": ""Soap dispenser CW""}
                ],
                ""keyIssues"": []
            }";

            var service = new ReportRagService();
            bool loaded = service.LoadFromJson(json);

            Assert.True(loaded);
        }

        [Fact]
        public void RagService_SummaryQuery_ReturnsCount()
        {
            string json = @"{
                ""requirements"": [
                    {""requirementId"": ""1"", ""status"": ""Met"", ""discipline"": ""Electrical"", ""requirementType"": ""grounding_bonding_conductors"", ""requirementText"": ""Grounding""},
                    {""requirementId"": ""2"", ""status"": ""NotMet"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_flush_valve_product_spec"", ""requirementText"": ""Flush valve""}
                ],
                ""keyIssues"": []
            }";

            var service = new ReportRagService();
            service.LoadFromJson(json);

            RagQueryResult result = service.Query("give me a summary");

            Assert.True(result.Success);
            Assert.Contains("2", result.Answer);
        }

        [Fact]
        public void RagService_RowQuery_ReturnsRowText()
        {
            string json = @"{
                ""requirements"": [
                    {""requirementId"": ""606"", ""status"": ""NeedsHumanReview"", ""discipline"": ""Plumbing"", ""requirementType"": ""plumbing_support_hanger_requirement"", ""requirementText"": ""P-trap with clevis hanger"", ""reasoning"": ""Scoped to plumbing support elements.""}
                ],
                ""keyIssues"": []
            }";

            var service = new ReportRagService();
            service.LoadFromJson(json);

            RagQueryResult result = service.Query("tell me about row 606");

            Assert.True(result.Success);
            Assert.Contains("606", result.Answer);
            Assert.Contains("P-trap", result.Answer);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static RequirementCheckResult MakeResult(string requirementType)
        {
            return new RequirementCheckResult
            {
                RequirementType = requirementType,
                Status = RequirementCheckStatus.NeedsHumanReview,
                Confidence = 0.5,
                Requirement = new OwnerRequirementRow
                {
                    RequirementText = "Sample requirement for " + requirementType,
                    Discipline = "Unknown"
                }
            };
        }
    }
}
