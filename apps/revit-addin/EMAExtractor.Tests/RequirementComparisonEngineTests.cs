using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EMAExtractor.Models;
using EMAExtractor.Requirements;
using Xunit;

namespace EMAExtractor.Tests
{
    public class RequirementComparisonEngineTests
    {
        [Fact]
        public void Evaluate_PopulatesNarrativeFieldsAndSourceMetadata()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-101",
                    SourceSheet = "Electrical Requirements",
                    RowNumber = 7,
                    Discipline = "Electrical",
                    RequirementText = "Provide panel and circuit assignment for lighting fixtures."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    Category = "Lighting Fixtures",
                    Name = "Fixture 1",
                    Family = "Lighting",
                    Type = "Type A",
                    Level = "Level 1"
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(rows, records, RequirementDiscipline.Electrical);

            Assert.Single(results);
            RequirementCheckResult result = results[0];

            Assert.Equal(RequirementCheckStatus.NeedsHumanReview, result.Status);
            Assert.Equal("Panel and circuit assignment", result.IssueTitle);
            Assert.Contains("panel and circuit", result.Reasoning);
            Assert.Contains("review", result.NextBestAction, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("candidate element", result.EvidenceSummary);
            Assert.Equal("Electrical", result.ResponsibleRole);
            Assert.Equal("Electrical Requirements", result.SourceWorksheet);
            Assert.Equal(7, result.SourceRow);
            Assert.Equal(result.Reasoning, result.Reason);
            Assert.Equal(result.NextBestAction, result.SuggestedAction);
        }

        [Fact]
        public void Evaluate_PlumbingSupportHangerRequirementUsesSpecificHangerActionInsteadOfGenericLevelAction()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-606",
                    SourceSheet = "Plumbing",
                    RowNumber = 606,
                    Discipline = "Plumbing",
                    RequirementText = "Provide P-trap with clevis hanger on Level 1."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    Category = "Pipe Accessories",
                    Name = "Clevis Hanger",
                    Family = "Pipe Hanger",
                    Type = "Clevis Hanger",
                    Level = "",
                    InstanceParameters = new Dictionary<string, ParameterRecord>()
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.Plumbing));

            Assert.Equal("plumbing_support_hanger_requirement", result.RequirementType);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Contains("hanger", result.NextBestAction ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("level values", result.NextBestAction ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_OutletCircuitRequirementBuildsTraceAndRequiresReviewWhenDirectEvidenceIsMissing()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-OUTLET-1",
                    SourceSheet = "Electrical",
                    RowNumber = 22,
                    Discipline = "Electrical",
                    RequirementText = "Provide 120V duplex outlet connected to room general purpose circuit."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 123456,
                    UniqueId = "uuid-123456",
                    Category = "Electrical Fixtures",
                    Name = "Duplex Outlet",
                    Family = "Duplex Receptacle",
                    Type = "120V Duplex",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        { "Voltage", new ParameterRecord { ValueString = "120 V" } }
                    }
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(rows, records, RequirementDiscipline.Electrical);

            RequirementCheckResult result = Assert.Single(results);
            Assert.Equal("Outlet and circuit assignment", result.IssueTitle);
            Assert.Equal("outlet_circuit_assignment", result.RuleApplied);
            Assert.Equal(ValidationType.Hybrid, result.ValidationType);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Equal(EvidenceAlignmentLevel.Weak, result.EvidenceAlignment);
            Assert.NotNull(result.FilterTrace);
            Assert.NotEmpty(result.FilterTrace.CandidateStages);
            Assert.Contains(result.FilterTrace.CandidateStages, stage => string.Equals(stage.StageName, "Parameter completeness check", StringComparison.OrdinalIgnoreCase));
            Assert.NotEmpty(result.ParameterChecks);
            Assert.Contains(result.ParameterChecks, check => string.Equals(check.ParameterName, "Circuit Number", StringComparison.OrdinalIgnoreCase) && !check.IsMatch);
            Assert.Contains(result.ParameterChecks, check => string.Equals(check.ParameterName, "Voltage", StringComparison.OrdinalIgnoreCase) && check.IsMatch);
            Assert.NotEmpty(result.MatchedFamilyTypeSummary);
            Assert.Contains(result.MatchedFamilyTypeSummary, item => item.Contains("Duplex Receptacle", StringComparison.OrdinalIgnoreCase));
            Assert.True(result.HumanReviewNeeded);
            Assert.Contains("confidence", result.ConfidenceReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("room", result.NextBestAction, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_IdentificationManufacturerRequirementDoesNotBecomeMetFromMechanicalEquipmentLevelOnly()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-ID-100",
                    SourceSheet = "Electrical",
                    RowNumber = 100,
                    Discipline = "Electrical",
                    RequirementText = "IDENTIFICATION OF EQUIPMENT Furnish and install items for identification of electrical products installed. Manufacturers include W.H. Brady Co., Carlton Industries, Inc., and Seton Nameplate Co."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 200001,
                    UniqueId = "uuid-200001",
                    Category = "Mechanical Equipment",
                    Name = "Mechanical Equipment 1",
                    Family = "AHU",
                    Type = "AHU-01",
                    Level = "Level 1"
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(rows, records, RequirementDiscipline.Electrical);

            RequirementCheckResult result = Assert.Single(results);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Equal(RequirementCheckStatus.NeedsHumanReview, result.Status);
            Assert.True(result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk || result.EvidenceAlignment == EvidenceAlignmentLevel.Weak);
            Assert.NotNull(result.NextBestAction);
            Assert.True(result.HumanReviewNeeded);
        }

        [Fact]
        public void Evaluate_DemolitionGroundingRequirementDoesNotBecomeMetFromGenericEquipmentEvidence()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-DEMOLITION-1",
                    SourceSheet = "Electrical",
                    RowNumber = 113,
                    Discipline = "Electrical",
                    RequirementText = "Disconnect abandoned outlets, remove devices, and provide blank covers as indicated on the drawings."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 300001,
                    UniqueId = "uuid-300001",
                    Category = "Communication Devices",
                    Name = "Device 1",
                    Family = "Device",
                    Type = "Low Voltage Device",
                    Level = "Level 2"
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(rows, records, RequirementDiscipline.Electrical);

            RequirementCheckResult result = Assert.Single(results);
            Assert.True(result.ValidationType == ValidationType.Manual || result.ValidationType == ValidationType.Hybrid);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.NotNull(result.NextBestAction);
            Assert.True(result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly || result.EvidenceAlignment == EvidenceAlignmentLevel.Weak || result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk);
        }

        [Fact]
        public void Evaluate_DemolitionRequirementDoesNotBecomeMetFromMechanicalSubstringNoise()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-DEMOLITION-112",
                    SourceSheet = "Electrical",
                    RowNumber = 112,
                    Discipline = "Electrical",
                    RequirementText = "Electrical Alterations Project Procedures - DEMOLITION AND EXTENSION OF EXISTING ELECTRICAL WORK. The Contractor shall modify, remove, and/or relocate all materials and items so indicated on the drawings or required by the installation of new facilities."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 400001,
                    UniqueId = "uuid-400001",
                    Category = "Mechanical Equipment",
                    Name = "Mechanical Equipment 1",
                    Family = "AHU",
                    Type = "AHU-01",
                    Level = "Level 1"
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(rows, records, RequirementDiscipline.Electrical);

            RequirementCheckResult result = Assert.Single(results);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.NotEqual("mechanical_equipment_placement", result.RuleApplied);
            Assert.True(result.ValidationType == ValidationType.Manual || result.ValidationType == ValidationType.Hybrid);
            Assert.True(result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly || result.EvidenceAlignment == EvidenceAlignmentLevel.Weak || result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk);
            Assert.True(result.HumanReviewNeeded);
        }

        [Fact]
        public void Evaluate_GroundingRequirementDoesNotBecomeMetFromMechanicalSubstringNoise()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-GROUND-133",
                    SourceSheet = "Electrical",
                    RowNumber = 133,
                    Discipline = "Electrical",
                    RequirementText = "Conductors: An insulated ground conductor shall be installed in all electrical conduit systems."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 500001,
                    UniqueId = "uuid-500001",
                    Category = "Mechanical Equipment",
                    Name = "Mechanical Equipment 1",
                    Family = "AHU",
                    Type = "AHU-01",
                    Level = "Level 1"
                }
            };

            List<RequirementCheckResult> results = engine.Evaluate(rows, records, RequirementDiscipline.Electrical);

            RequirementCheckResult result = Assert.Single(results);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.NotEqual("mechanical_equipment_placement", result.RuleApplied);
            Assert.True(result.ValidationType == ValidationType.Manual || result.ValidationType == ValidationType.Hybrid);
            Assert.True(result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly || result.EvidenceAlignment == EvidenceAlignmentLevel.Weak || result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk);
            Assert.True(result.HumanReviewNeeded);
        }

        [Fact]
        public void Evaluate_ConduitSizeRequirementPrioritizesConduitOverLightingCoverageAndCannotMetFromLevelOnly()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-155",
                    SourceSheet = "Technology",
                    RowNumber = 155,
                    Discipline = "Technology",
                    RequirementText = "Minimum conduit size 3/4-inch; Technology / Voice / Data / Video conduit size 1-inch; flexible metallic conduit tap connections to light fixtures/equipment 1/2-inch, 6-foot max."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 155001,
                    UniqueId = "uuid-155001",
                    Category = "Lighting Fixtures",
                    Name = "Lighting Fixture",
                    Family = "Lighting",
                    Type = "Type A",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal("conduit_raceway_size_requirement", result.RequirementType);
            Assert.NotEqual("lighting_fixture_coverage", result.RuleApplied);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Contains("supporting context only", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.True(result.EvidenceAlignment == EvidenceAlignmentLevel.Weak || result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk || result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly);
        }

        [Fact]
        public void Evaluate_ManufacturerRestrictionPrioritizesBrandRestrictionAndCannotMetFromEquipmentPresence()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-478",
                    SourceSheet = "Mechanical",
                    RowNumber = 478,
                    Discipline = "Mechanical",
                    RequirementText = "RTU BASE BID: Aaon HVAC equipment with terminal strip only. ALT BID: Equivalent HVAC equipment as manufactured by Lennox then Trane. No York/JCI Split Systems: Trane or Lennox ONLY. No York units."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 478001,
                    UniqueId = "uuid-478001",
                    Category = "Mechanical Equipment",
                    Name = "RTU 1",
                    Family = "RTU",
                    Type = "RTU-A",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal("manufacturer_brand_restriction", result.RequirementType);
            Assert.NotEqual("mechanical_equipment_coverage", result.RuleApplied);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Contains("supporting context only", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_RtuPerformanceRequirementDoesNotCollapseIntoCoverage()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-479",
                    SourceSheet = "Mechanical",
                    RowNumber = 479,
                    Discipline = "Mechanical",
                    RequirementText = "RTUs with two-speed compressors, bi-polar ionization without demand control ventilation."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 479001,
                    UniqueId = "uuid-479001",
                    Category = "Mechanical Equipment",
                    Name = "RTU 2",
                    Family = "RTU",
                    Type = "RTU-B",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal("mechanical_performance_feature", result.RequirementType);
            Assert.NotEqual("plumbing_routing_coverage", result.RuleApplied);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Contains("supporting context only", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_ControlAndDdcRequirementClassifiesAsControlsAndNotEquipmentCoverage()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-480",
                    SourceSheet = "Mechanical",
                    RowNumber = 480,
                    Discipline = "Mechanical",
                    RequirementText = "Control small restroom ceiling mounted exhaust fan with lights. Control large gang restroom ceiling mounted exhaust fan with EMCS."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 480001,
                    UniqueId = "uuid-480001",
                    Category = "Mechanical Equipment",
                    Name = "Exhaust Fan",
                    Family = "Fan",
                    Type = "EF-1",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal("mechanical_controls_ddc_emcs", result.RequirementType);
            Assert.NotEqual("mechanical_equipment_coverage", result.RuleApplied);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Contains("supporting context only", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_DdcMeteringRequirementClassifiesAsControlsAndNotPlumbingRouting()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-485",
                    SourceSheet = "Mechanical",
                    RowNumber = 485,
                    Discipline = "Mechanical",
                    RequirementText = "Venturi meters tied to DDC."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 485001,
                    UniqueId = "uuid-485001",
                    Category = "Plumbing Fixtures",
                    Name = "Venturi Meter",
                    Family = "Meter",
                    Type = "VT-1",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal("mechanical_controls_ddc_emcs", result.RequirementType);
            Assert.NotEqual("plumbing_routing_coverage", result.RuleApplied);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Contains("supporting context only", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_RoofPenetrationConstraintClassifiesAsInstallationMethodConstraint()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-491",
                    SourceSheet = "Mechanical",
                    RowNumber = 491,
                    Discipline = "Mechanical",
                    RequirementText = "In-line exhaust fans, attic space, roof penetrations, no hooded penetrations, sealed roof pipe."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 491001,
                    UniqueId = "uuid-491001",
                    Category = "Mechanical Equipment",
                    Name = "Exhaust Fan",
                    Family = "Fan",
                    Type = "EF-2",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal("installation_method_constraint", result.RequirementType);
            Assert.NotEqual("mechanical_equipment_coverage", result.RuleApplied);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Contains("supporting context only", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_UnknownAmbiguousCannotBeMetFromCategoryAndLevel()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-UNK-1",
                    SourceSheet = "General",
                    RowNumber = 700,
                    Discipline = "Mechanical",
                    RequirementText = "Provide coordination and confirm intent."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 700001,
                    UniqueId = "uuid-700001",
                    Category = "Mechanical Equipment",
                    Name = "Generic Equipment",
                    Family = "Equipment",
                    Type = "Type A",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal("unknown_ambiguous", result.RequirementType);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.NotEqual("No action required for this requirement.", result.NextBestAction);
        }

        [Fact]
        public void Evaluate_DrawingSpecValidationCannotBeMetFromCategoryAndLevel()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-DOC-1",
                    SourceSheet = "Mechanical",
                    RowNumber = 701,
                    Discipline = "Mechanical",
                    RequirementText = "Per drawings, specifications, and owner approval, provide the listed equipment."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 701001,
                    UniqueId = "uuid-701001",
                    Category = "Mechanical Equipment",
                    Name = "Generic Equipment",
                    Family = "Equipment",
                    Type = "Type B",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal("drawing_spec_manual_owner_approval", result.RequirementType);
            Assert.NotEqual(RequirementCheckStatus.Met, result.Status);
            Assert.Contains("supporting context only", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_DirectClosingEvidenceIsSeparatedFromSupportingContext()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-PANEL-1",
                    SourceSheet = "Electrical",
                    RowNumber = 702,
                    Discipline = "Electrical",
                    RequirementText = "Provide panel and circuit assignment for all lighting fixtures."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 702001,
                    UniqueId = "uuid-702001",
                    Category = "Lighting Fixtures",
                    Name = "Fixture A",
                    Family = "Lighting",
                    Type = "Fixture Type",
                    Level = "Level 1",
                    InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        { "Panel", new ParameterRecord { ValueString = "DP-1" } },
                        { "Circuit Number", new ParameterRecord { ValueString = "7" } },
                        { "Supply From", new ParameterRecord { ValueString = "DP-1" } },
                        { "Voltage", new ParameterRecord { ValueString = "277" } }
                    }
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.Electrical));

            Assert.Equal(RequirementCheckStatus.Met, result.Status);
            Assert.NotEmpty(result.DirectClosingEvidence);
            Assert.Contains(result.DirectClosingEvidence, item => item.Contains("Panel", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.DirectClosingEvidence, item => item.Contains("Circuit Number", StringComparison.OrdinalIgnoreCase));
            Assert.NotEmpty(result.SupportingContext);
            Assert.Contains(result.SupportingContext, item => item.Contains("Lighting Fixtures", StringComparison.OrdinalIgnoreCase) || item.Contains("Level", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(result.MissingDirectEvidence);
            Assert.DoesNotContain(result.Reasoning ?? string.Empty, "requires review", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Evaluate_NeedsHumanReviewNarrativeUsesSupportingContextOnlyLanguageWithoutContradiction()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>
            {
                new OwnerRequirementRow
                {
                    RequirementId = "REQ-478-REVIEW",
                    SourceSheet = "Mechanical",
                    RowNumber = 703,
                    Discipline = "Mechanical",
                    RequirementText = "RTU BASE BID: Aaon HVAC equipment with terminal strip only. ALT BID: Equivalent HVAC equipment as manufactured by Lennox then Trane. No York/JCI Split Systems: Trane or Lennox ONLY. No York units."
                }
            };

            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord
                {
                    ElementId = 703001,
                    UniqueId = "uuid-703001",
                    Category = "Mechanical Equipment",
                    Name = "RTU 3",
                    Family = "RTU",
                    Type = "RTU-C",
                    Level = "Level 1"
                }
            };

            RequirementCheckResult result = Assert.Single(engine.Evaluate(rows, records, RequirementDiscipline.All));

            Assert.Equal(RequirementCheckStatus.NeedsHumanReview, result.Status);
            Assert.Contains("Model evidence is supporting context only", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("marked as Not Met", result.Reasoning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("No action required", result.NextBestAction ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EvaluateParallel_ProducesDeterministicResults()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = BuildSyntheticRequirements(100);
            List<ExportElementRecord> records = BuildSyntheticElements(500);
            EvidenceIndex index = new EvidenceIndex(records);

            List<RequirementCheckResult> sequential = engine.Evaluate(rows, records, RequirementDiscipline.Electrical);
            List<RequirementCheckResult> parallel = engine.EvaluateParallel(
                rows, index, RequirementDiscipline.Electrical, maxDegreeOfParallelism: 4);

            Assert.Equal(sequential.Count, parallel.Count);
            for (int i = 0; i < sequential.Count; i++)
            {
                Assert.Equal(sequential[i].Status, parallel[i].Status);
                Assert.Equal(sequential[i].SourceRow, parallel[i].SourceRow);
                Assert.Equal(sequential[i].IssueTitle, parallel[i].IssueTitle);
            }
        }

        [Fact]
        public void EvaluateParallel_800RequirementsAnd20000Elements_CompletesReasonably()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = BuildSyntheticRequirements(800);
            List<ExportElementRecord> records = BuildSyntheticElements(20000);
            EvidenceIndex index = new EvidenceIndex(records);

            Stopwatch sw = Stopwatch.StartNew();
            List<RequirementCheckResult> results = engine.EvaluateParallel(
                rows, index, RequirementDiscipline.All,
                maxDegreeOfParallelism: Math.Max(1, Environment.ProcessorCount - 1));
            sw.Stop();

            Assert.Equal(800, results.Count);
            Assert.True(sw.ElapsedMilliseconds < 120000, "Evaluation took too long: " + sw.ElapsedMilliseconds + "ms");
        }

        [Fact]
        public void EvaluateParallel_EmptyEvidence_HandledSafely()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            List<OwnerRequirementRow> rows = BuildSyntheticRequirements(10);
            EvidenceIndex index = new EvidenceIndex(new List<ExportElementRecord>());

            List<RequirementCheckResult> results = engine.EvaluateParallel(
                rows, index, RequirementDiscipline.Electrical);

            Assert.Equal(10, results.Count);
            Assert.All(results, r => Assert.NotNull(r.Reasoning));
        }

        [Fact]
        public void EvaluateParallel_EmptyRequirements_ReturnsEmpty()
        {
            RequirementComparisonEngine engine = new RequirementComparisonEngine();
            EvidenceIndex index = new EvidenceIndex(BuildSyntheticElements(100));

            List<RequirementCheckResult> results = engine.EvaluateParallel(
                new List<OwnerRequirementRow>(), index, RequirementDiscipline.All);

            Assert.Empty(results);
        }

        [Fact]
        public void EvidenceIndex_BuildsCategoryIndex()
        {
            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord { Category = "Lighting Fixtures", Name = "LF1" },
                new ExportElementRecord { Category = "Lighting Fixtures", Name = "LF2" },
                new ExportElementRecord { Category = "Mechanical Equipment", Name = "ME1" }
            };

            EvidenceIndex index = new EvidenceIndex(records);

            Assert.Equal(3, index.ElementCount);
            Assert.True(index.ByCategoryNormalized.ContainsKey("lighting fixtures"));
            Assert.Equal(2, index.ByCategoryNormalized["lighting fixtures"].Count);
            Assert.True(index.ByCategoryNormalized.ContainsKey("mechanical equipment"));
        }

        [Fact]
        public void EvidenceIndex_PreBuildsSearchBlobs()
        {
            List<ExportElementRecord> records = new List<ExportElementRecord>
            {
                new ExportElementRecord { Category = "Lighting Fixtures", Name = "Test Fixture", Level = "Level 1" }
            };

            EvidenceIndex index = new EvidenceIndex(records);

            Assert.True(index.SearchBlobs.ContainsKey(records[0]));
            string blob = index.SearchBlobs[records[0]];
            Assert.Contains("lighting fixtures", blob);
            Assert.Contains("test fixture", blob);
        }

        [Fact]
        public void CoherenceChecker_DetectsAllNotApplicable()
        {
            List<RequirementCheckResult> results = Enumerable.Range(0, 10).Select(i =>
                new RequirementCheckResult
                {
                    Status = RequirementCheckStatus.NotApplicable,
                    SourceRow = i
                }).ToList();

            List<string> warnings = CoherenceChecker.Check(results, 100);

            Assert.Contains(warnings, w => w.Contains("Not Applicable"));
        }

        [Fact]
        public void CoherenceChecker_DetectsEmptyEvidence()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                new RequirementCheckResult { Status = RequirementCheckStatus.InsufficientModelData, SourceRow = 1, Reasoning = "r", NextBestAction = "a" }
            };

            List<string> warnings = CoherenceChecker.Check(results, 0);

            Assert.Contains(warnings, w => w.Contains("element count is zero"));
        }

        [Fact]
        public void CoherenceChecker_NoWarningsForHealthyResults()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                new RequirementCheckResult { Status = RequirementCheckStatus.Met, Confidence = 0.9, SourceRow = 1, Reasoning = "r", NextBestAction = "a" },
                new RequirementCheckResult { Status = RequirementCheckStatus.NotMet, Confidence = 0.8, SourceRow = 2, Reasoning = "r", NextBestAction = "a" },
                new RequirementCheckResult { Status = RequirementCheckStatus.NeedsHumanReview, Confidence = 0.5, SourceRow = 3, Reasoning = "r", NextBestAction = "a" }
            };

            List<string> warnings = CoherenceChecker.Check(results, 100);

            Assert.Empty(warnings);
        }

        private static List<OwnerRequirementRow> BuildSyntheticRequirements(int count)
        {
            string[] templates = new[]
            {
                "Provide panel and circuit assignment for all electrical equipment.",
                "Verify voltage rating matches design intent.",
                "Ensure lighting fixtures are placed on correct levels.",
                "Mechanical equipment must have airflow parameters populated.",
                "Plumbing fixtures must have pipe connections.",
                "Data devices must be connected to communication pathways.",
                "Fire alarm devices must be placed per code.",
                "All elements shall have level assignment.",
                "Coordinate electrical load calculations with engineering.",
                "Review owner standards for fixture selections."
            };

            List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>(count);
            for (int i = 0; i < count; i++)
            {
                rows.Add(new OwnerRequirementRow
                {
                    RequirementId = "REQ-" + (i + 1).ToString(),
                    SourceSheet = "Electrical Requirements",
                    RowNumber = i + 2,
                    Discipline = "Electrical",
                    RequirementText = templates[i % templates.Length]
                });
            }
            return rows;
        }

        private static List<ExportElementRecord> BuildSyntheticElements(int count)
        {
            string[] categories = new[] { "Lighting Fixtures", "Electrical Equipment", "Mechanical Equipment", "Plumbing Fixtures", "Communication Devices" };
            string[] families = new[] { "Panel", "Fixture", "Equipment", "Device", "Pipe" };
            string[] levels = new[] { "Level 1", "Level 2", "Level 3", "" };

            List<ExportElementRecord> records = new List<ExportElementRecord>(count);
            for (int i = 0; i < count; i++)
            {
                ExportElementRecord record = new ExportElementRecord
                {
                    Category = categories[i % categories.Length],
                    Name = "Element_" + i.ToString(),
                    Family = families[i % families.Length],
                    Type = "Type_" + (i % 20).ToString(),
                    Level = levels[i % levels.Length]
                };

                if (i % 3 == 0)
                {
                    record.InstanceParameters = new Dictionary<string, ParameterRecord>
                    {
                        { "Panel", new ParameterRecord { ValueString = "DP-1" } },
                        { "Circuit Number", new ParameterRecord { ValueString = (i % 42 + 1).ToString() } },
                        { "Voltage", new ParameterRecord { ValueString = "277" } }
                    };
                }

                records.Add(record);
            }
            return records;
        }
    }
}
