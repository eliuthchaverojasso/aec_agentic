using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EMAExtractor.Reporting;
using EMAExtractor.Requirements;
using EMAExtractor.Requirements.Audit;
using Xunit;

namespace EMAExtractor.Tests
{
    public class RequirementAuditCoherenceTests
    {
        private static RequirementCheckResult MakeResult(
            string id,
            string text,
            string discipline,
            int row,
            string requirementType = "general_owner_requirement",
            RequirementCheckStatus status = RequirementCheckStatus.NeedsHumanReview,
            ValidationType validationType = ValidationType.Hybrid)
        {
            return new RequirementCheckResult
            {
                RequirementId = id,
                RequirementText = text,
                Discipline = discipline,
                SourceWorksheet = discipline,
                SourceFile = "OwnerRequirements.xlsx",
                SourceRow = row,
                RequirementType = requirementType,
                ValidationType = validationType,
                Status = status,
                Confidence = 0.5
            };
        }

        // ----------------------------- Semantic parser -----------------------------

        [Fact]
        public void Parser_ExtractsVoltageCurrentPercentAndBrands()
        {
            RequirementSemanticIr ir = RequirementSemanticParser.Parse(
                "Each MDF rack shall have at least two 208V 30A receptacles with no more than 5% voltage drop. Trane only.");

            Assert.Equal("shall", ir.Modality);
            Assert.Equal(2, ir.MinimumQuantity);
            Assert.Contains(ir.Quantities, q => q.Property == "voltage" && Math.Abs(q.Value - 208) < 0.001 && q.Unit == "V");
            Assert.Contains(ir.Quantities, q => q.Property == "current" && Math.Abs(q.Value - 30) < 0.001 && q.Unit == "A");
            Assert.Contains(ir.Quantities, q => q.Property == "percent" && Math.Abs(q.Value - 5) < 0.001);
            Assert.Contains("rack", ir.SubjectTokens);
            Assert.Contains("receptacle", ir.SubjectTokens);
            Assert.Contains("trane", ir.ManufacturerBrands);
            Assert.True(ir.ManufacturerExclusive);
            Assert.False(string.IsNullOrEmpty(ir.ContentHash));
        }

        [Fact]
        public void Parser_ParsesConduitFractionSizes()
        {
            RequirementSemanticIr ir = RequirementSemanticParser.Parse("Minimum conduit size 3/4-inch.");
            Assert.Contains(ir.Quantities, q => q.Property == "length" && q.Unit == "in" && Math.Abs(q.Value - 0.75) < 0.001);
            Assert.Contains("conduit", ir.SubjectTokens);
        }

        // ----------------------------- Coherence engine -----------------------------

        [Fact]
        public void Coherence_DetectsExactDuplicate()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "All devices shall have a level assignment.", "Electrical", 10),
                MakeResult("R2", "All devices shall have a level assignment.", "Electrical", 20)
            };

            RequirementCoherenceReport report = RequirementCoherenceEngine.Analyze(results);

            Assert.Equal(1, report.ExactDuplicateCount);
            Assert.Contains(report.Findings, f => f.FindingType == CoherenceFindingType.ExactDuplicate);
        }

        [Fact]
        public void Coherence_DetectsSemanticDuplicate()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "Provide ground conductor in all electrical conduit systems for the building.", "Electrical", 11),
                MakeResult("R2", "Provide ground conductor in all electrical conduit systems throughout the building.", "Electrical", 12)
            };

            RequirementCoherenceReport report = RequirementCoherenceEngine.Analyze(results);

            Assert.Contains(report.Findings, f => f.FindingType == CoherenceFindingType.SemanticDuplicate);
            Assert.Equal(0, report.ExactDuplicateCount);
        }

        [Fact]
        public void Coherence_DetectsNumericConflictForSameSubject()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "Provide panel rated for 208V service.", "Electrical", 30),
                MakeResult("R2", "Panel shall be rated 240V.", "Electrical", 31)
            };

            RequirementCoherenceReport report = RequirementCoherenceEngine.Analyze(results);

            Assert.Equal(1, report.NumericConflictCount);
            CoherenceFinding finding = Assert.Single(report.Findings, f => f.FindingType == CoherenceFindingType.NumericConflict);
            Assert.Equal(CoherenceSeverity.High, finding.Severity);
            Assert.Equal("Conflicts Found", report.CoherenceGrade);
        }

        [Fact]
        public void Coherence_DetectsQuantityConflictForSameSubject()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "Each MDF rack shall have at least two receptacles.", "Technology", 40),
                MakeResult("R2", "Each MDF rack shall have at least four receptacles.", "Technology", 41)
            };

            RequirementCoherenceReport report = RequirementCoherenceEngine.Analyze(results);

            Assert.Equal(1, report.QuantityConflictCount);
            Assert.Contains(report.Findings, f => f.FindingType == CoherenceFindingType.QuantityConflict);
        }

        [Fact]
        public void Coherence_DetectsManufacturerConflictForSameSubject()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "RTU shall be Trane only. No York units.", "Mechanical", 50),
                MakeResult("R2", "RTU may be a York rooftop unit.", "Mechanical", 51)
            };

            RequirementCoherenceReport report = RequirementCoherenceEngine.Analyze(results);

            Assert.Equal(1, report.ManufacturerConflictCount);
            Assert.Contains(report.Findings, f => f.FindingType == CoherenceFindingType.ManufacturerConflict);
        }

        [Fact]
        public void Coherence_DifferentSubjectsDoNotFalselyConflict()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "Provide panel rated for 208V.", "Electrical", 60),
                MakeResult("R2", "Provide receptacle rated for 240V.", "Electrical", 61)
            };

            RequirementCoherenceReport report = RequirementCoherenceEngine.Analyze(results);

            Assert.Equal(0, report.NumericConflictCount);
            Assert.DoesNotContain(report.Findings, f => f.FindingType == CoherenceFindingType.NumericConflict);
        }

        [Fact]
        public void Coherence_ProducesPerRequirementTypeSummaries()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "All devices shall have a level assignment.", "Electrical", 70, requirementType: "level_assignment"),
                MakeResult("R2", "All devices shall have a level assignment.", "Electrical", 71, requirementType: "level_assignment"),
                MakeResult("R3", "Coordinate exhaust fan controls with owner.", "Mechanical", 72, requirementType: "manual_review_coordination")
            };

            RequirementCoherenceReport report = RequirementCoherenceEngine.Analyze(results);

            Assert.True(report.RequirementTypesAnalyzed >= 2);
            RequirementTypeCoherenceSummary levelType = Assert.Single(report.TypeSummaries, s => s.RequirementType == "level_assignment");
            Assert.Equal(2, levelType.RequirementCount);
            Assert.True(levelType.FindingCount >= 1);
            Assert.False(levelType.IsCoherent);

            RequirementTypeCoherenceSummary manualType = Assert.Single(report.TypeSummaries, s => s.RequirementType == "manual_review_coordination");
            Assert.True(manualType.IsCoherent);
        }

        [Fact]
        public void Coherence_DoesNotMutateRequirementStatuses()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "Provide panel rated for 208V.", "Electrical", 80, status: RequirementCheckStatus.NotMet),
                MakeResult("R2", "Panel shall be rated 240V.", "Electrical", 81, status: RequirementCheckStatus.Met)
            };

            RequirementCoherenceEngine.Analyze(results);

            Assert.Equal(RequirementCheckStatus.NotMet, results[0].Status);
            Assert.Equal(RequirementCheckStatus.Met, results[1].Status);
        }

        [Fact]
        public void Coherence_IsDeterministicAcrossRuns()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "Provide panel rated for 208V service.", "Electrical", 90),
                MakeResult("R2", "Panel shall be rated 240V.", "Electrical", 91),
                MakeResult("R3", "Each MDF rack shall have at least two receptacles.", "Technology", 92),
                MakeResult("R4", "Each MDF rack shall have at least four receptacles.", "Technology", 93)
            };

            string first = string.Join(";", RequirementCoherenceEngine.Analyze(results).Findings.Select(f => f.Id));
            string second = string.Join(";", RequirementCoherenceEngine.Analyze(results).Findings.Select(f => f.Id));

            Assert.Equal(first, second);
        }

        // ----------------------------- Audit record -----------------------------

        [Fact]
        public void AuditRecord_ProjectsStatusAndProvenanceWithoutRecomputing()
        {
            RequirementCheckResult result = MakeResult("R1", "Provide panel and circuit assignment.", "Electrical", 5,
                status: RequirementCheckStatus.Met, validationType: ValidationType.Model);

            RequirementAuditRecord record = RequirementAuditRecordBuilder.Build(result, null);

            Assert.Equal(AuditDecisionStatus.Compliant, record.DecisionStatus);
            Assert.False(string.IsNullOrEmpty(record.Source.RequirementContentHash));
            Assert.False(string.IsNullOrEmpty(record.SemanticIr.NormalizedText));
            Assert.True(record.Source.TraceabilityComplete);
            Assert.Equal(EvidencePolicyOperator.All, record.EvidencePolicy.Operator);
            Assert.False(record.EvidencePolicy.ClosureRequiresHumanReview);
            Assert.False(string.IsNullOrEmpty(record.RecordHash));
        }

        [Fact]
        public void AuditRecord_AbsenceOfEvidenceIsNeverNonCompliant()
        {
            RequirementCheckResult insufficient = MakeResult("R1", "Provide panel.", "Electrical", 6,
                status: RequirementCheckStatus.InsufficientModelData, validationType: ValidationType.Model);
            RequirementCheckResult review = MakeResult("R2", "Coordinate with owner.", "Electrical", 7,
                status: RequirementCheckStatus.NeedsHumanReview, validationType: ValidationType.Manual);

            Assert.Equal(AuditDecisionStatus.InsufficientData, RequirementAuditRecordBuilder.Build(insufficient, null).DecisionStatus);

            RequirementAuditRecord reviewRecord = RequirementAuditRecordBuilder.Build(review, null);
            Assert.Equal(AuditDecisionStatus.NeedsReview, reviewRecord.DecisionStatus);
            Assert.Equal(EvidencePolicyOperator.ManualRequired, reviewRecord.EvidencePolicy.Operator);
            Assert.True(reviewRecord.EvidencePolicy.ClosureRequiresHumanReview);
        }

        [Fact]
        public void AuditRecord_LinksCoherenceFindingsToRequirement()
        {
            List<RequirementCheckResult> results = new List<RequirementCheckResult>
            {
                MakeResult("R1", "Provide panel rated for 208V service.", "Electrical", 100),
                MakeResult("R2", "Panel shall be rated 240V.", "Electrical", 101)
            };
            RequirementCoherenceReport coherence = RequirementCoherenceEngine.Analyze(results);

            List<RequirementAuditRecord> records = RequirementAuditRecordBuilder.BuildAll(results, coherence);

            Assert.All(records, r => Assert.NotEmpty(r.CoherenceFindingIds));
        }

        // ----------------------------- Evaluation bundle -----------------------------

        private static RequirementCheckReport MakeReport()
        {
            return new RequirementCheckReport
            {
                ProjectName = "Northwest ISD",
                ModelName = "NWISD-Model",
                RequirementsFileName = "OwnerRequirements.xlsx"
            };
        }

        private static List<RequirementCheckResult> MakeBundleResults()
        {
            return new List<RequirementCheckResult>
            {
                MakeResult("R1", "Provide panel and circuit assignment.", "Electrical", 1, status: RequirementCheckStatus.Met, validationType: ValidationType.Model),
                MakeResult("R2", "RTU shall be Trane only. No York units.", "Mechanical", 2, status: RequirementCheckStatus.NotMet, validationType: ValidationType.Specification),
                MakeResult("R3", "Coordinate exhaust fan controls with owner.", "Mechanical", 3, status: RequirementCheckStatus.NeedsHumanReview, validationType: ValidationType.Manual)
            };
        }

        [Fact]
        public void Bundle_SameInputsVersionsAndAsOfProduceSameHashes()
        {
            DateTime asOf = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

            EvaluationBundle a = EvaluationBundleWriter.Create(MakeReport(), MakeBundleResults(), asOf);
            EvaluationBundle b = EvaluationBundleWriter.Create(MakeReport(), MakeBundleResults(), asOf);

            Assert.Equal(a.Manifest.InputHash, b.Manifest.InputHash);
            Assert.Equal(a.Manifest.OutputHash, b.Manifest.OutputHash);
            Assert.Equal(a.Manifest.EvaluationRunId, b.Manifest.EvaluationRunId);
            Assert.Equal(3, a.Manifest.RequirementsTotal);
            Assert.Equal(1, a.Manifest.StatusCounts["met"]);
        }

        [Fact]
        public void Bundle_DifferentAsOfChangesRunIdButNotOutputHash()
        {
            EvaluationBundle a = EvaluationBundleWriter.Create(MakeReport(), MakeBundleResults(),
                new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc));
            EvaluationBundle b = EvaluationBundleWriter.Create(MakeReport(), MakeBundleResults(),
                new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc));

            Assert.NotEqual(a.Manifest.EvaluationRunId, b.Manifest.EvaluationRunId);
            Assert.Equal(a.Manifest.OutputHash, b.Manifest.OutputHash);
        }

        [Fact]
        public void Bundle_DifferentDecisionChangesOutputHash()
        {
            DateTime asOf = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

            List<RequirementCheckResult> changed = MakeBundleResults();
            changed[0].Status = RequirementCheckStatus.NotMet;

            EvaluationBundle baseline = EvaluationBundleWriter.Create(MakeReport(), MakeBundleResults(), asOf);
            EvaluationBundle mutated = EvaluationBundleWriter.Create(MakeReport(), changed, asOf);

            Assert.NotEqual(baseline.Manifest.OutputHash, mutated.Manifest.OutputHash);
        }

        // ----------------------------- Report integration -----------------------------

        [Fact]
        public void Report_RendersCoherenceAuditSectionAndFindings()
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "ema_coherence_report_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                List<RequirementCheckResult> results = new List<RequirementCheckResult>
                {
                    MakeResult("R1", "All devices shall have a level assignment.", "Electrical", 1, requirementType: "level_assignment"),
                    MakeResult("R2", "All devices shall have a level assignment.", "Electrical", 2, requirementType: "level_assignment"),
                    MakeResult("R3", "Provide panel rated for 208V service.", "Electrical", 3, requirementType: "parameter_performance"),
                    MakeResult("R4", "Panel shall be rated 240V.", "Electrical", 4, requirementType: "parameter_performance")
                };

                RequirementCheckReport report = new RequirementCheckReport
                {
                    ProjectName = "Northwest ISD",
                    ModelName = "NWISD-Model",
                    RequirementsFileName = "OwnerRequirements.xlsx",
                    Discipline = RequirementDiscipline.All,
                    Scope = RequirementModelScope.EntireModel,
                    GeneratedAt = DateTime.Now,
                    OutputFolder = tempFolder,
                    Results = results,
                    Summary = RequirementCheckSummary.FromResults(results)
                };

                string path = OwnerRequirementHtmlReportGenerator.Generate(report);
                string html = File.ReadAllText(path);

                Assert.Contains("Requirement Coherence Audit", html);
                Assert.Contains("Coherence by requirement type", html);
                Assert.Contains("Exact Duplicate", html);
                Assert.Contains("Numeric Conflict", html);
                Assert.Contains("coherence_audit", html);
                Assert.Contains("Conflicts Found", html);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }

        [Fact]
        public void Bundle_WritesAllArtifactFiles()
        {
            DateTime asOf = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            string tempRoot = Path.Combine(Path.GetTempPath(), "ema_bundle_test_" + Guid.NewGuid().ToString("N"));

            try
            {
                string bundleFolder = EvaluationBundleWriter.CreateAndWrite(MakeReport(), MakeBundleResults(), asOf, tempRoot);

                Assert.True(File.Exists(Path.Combine(bundleFolder, "evaluation_manifest.json")));
                Assert.True(File.Exists(Path.Combine(bundleFolder, "requirement_audits.json")));
                Assert.True(File.Exists(Path.Combine(bundleFolder, "coherence_findings.json")));
                Assert.True(File.Exists(Path.Combine(bundleFolder, "evaluation_summary.json")));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }
    }
}
