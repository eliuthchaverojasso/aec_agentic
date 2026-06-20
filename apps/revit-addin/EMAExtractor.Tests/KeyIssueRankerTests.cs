using System.Collections.Generic;
using System.Linq;
using EMAExtractor.Requirements;
using Xunit;

namespace EMAExtractor.Tests
{
    public class KeyIssueRankerTests
    {
        [Fact]
        public void RankIssues_ExcludesMetAndNotApplicable()
        {
            List<KeyIssue> issues = KeyIssueRanker.RankIssues(new[]
            {
                Result(RequirementCheckStatus.Met, "panel_circuit_power"),
                Result(RequirementCheckStatus.NotApplicable, "panel_circuit_power"),
                Result(RequirementCheckStatus.NotMet, "panel_circuit_power")
            }, RequirementDiscipline.All, 10);

            Assert.Single(issues);
            Assert.Equal(RequirementCheckStatus.NotMet, issues[0].Status);
        }

        [Fact]
        public void RankIssues_ClampsScoreAndIncludesFactors()
        {
            RequirementCheckResult result = Result(RequirementCheckStatus.NotMet, "grounding_bonding_conductors");
            result.Confidence = 0.99;
            result.MatchedModelElementCount = 250000;
            result.CandidateScopeValid = true;

            KeyIssue issue = Assert.Single(KeyIssueRanker.RankIssues(new[] { result }, RequirementDiscipline.All, 10));

            Assert.InRange(issue.KeyIssueScore, 0.0, 1.0);
            Assert.True(issue.StatusSeverityScore > 0);
            Assert.True(issue.DeliverableImpactScore > 0);
            Assert.True(issue.ActionabilityScore > 0);
            Assert.True(issue.EvidenceGapScore > 0);
            Assert.True(issue.RequirementTypeRiskScore > 0);
            Assert.NotNull(issue.KeyIssueScoreReason);
        }

        [Fact]
        public void RankIssues_SortsDescendingInsideUrgencyGroup()
        {
            RequirementCheckResult lower = Result(RequirementCheckStatus.NeedsHumanReview, "manufacturer_product_spec_submittal");
            lower.SourceRow = 10;
            lower.MatchedModelElementCount = 0;

            RequirementCheckResult higher = Result(RequirementCheckStatus.NotMet, "panel_circuit_power");
            higher.SourceRow = 11;
            higher.MatchedModelElementCount = 10;

            List<KeyIssue> issues = KeyIssueRanker.RankIssues(new[] { lower, higher }, RequirementDiscipline.All, 10);

            Assert.True(issues[0].KeyIssueScore >= issues[1].KeyIssueScore);
        }

        [Fact]
        public void RankIssues_NotMetWithDirectMissingParametersRanksHigh()
        {
            RequirementCheckResult result = Result(RequirementCheckStatus.NotMet, "panel_circuit_power");
            result.MatchedElementIds = new List<long> { 101, 102 };
            result.MissingExpectedParameters = new List<string> { "Panel", "Circuit Number" };

            KeyIssue issue = Assert.Single(KeyIssueRanker.RankIssues(new[] { result }, RequirementDiscipline.All, 10));

            Assert.True(issue.KeyIssueScore >= 0.70);
            Assert.True(issue.ActionabilityScore >= 0.90);
        }

        [Fact]
        public void RankIssues_ManualOnlyNeedsHumanReviewBecomesNeedsReviewUnlessHighRisk()
        {
            RequirementCheckResult result = Result(RequirementCheckStatus.NeedsHumanReview, "manufacturer_product_spec_submittal");
            result.EvidenceAlignment = EvidenceAlignmentLevel.ManualOnly;
            result.MatchedModelElementCount = 0;

            KeyIssue issue = Assert.Single(KeyIssueRanker.RankIssues(new[] { result }, RequirementDiscipline.All, 10));

            Assert.Equal("Needs Review", issue.SeverityLabel);
        }

        [Fact]
        public void RankIssues_FullModelFallbackDoesNotCreateCriticalAndImpactScaleIsZero()
        {
            RequirementCheckResult result = Result(RequirementCheckStatus.NotMet, "grounding_bonding_conductors");
            result.FallbackUsed = true;
            result.FullModelFallbackUsed = true;
            result.CandidateScopeValid = false;
            result.MatchedModelElementCount = 21868;

            KeyIssue issue = Assert.Single(KeyIssueRanker.RankIssues(new[] { result }, RequirementDiscipline.All, 10));

            Assert.NotEqual("Critical", issue.SeverityLabel);
            Assert.Equal(0.0, issue.ImpactScaleScore);
            Assert.False(issue.CandidateScopeValid);
        }

        [Fact]
        public void RankIssues_HighRiskDoesNotBecomeLowBecauseConfidenceIsLow()
        {
            RequirementCheckResult result = Result(RequirementCheckStatus.NeedsHumanReview, "plumbing_hose_bibb_rpz_valves");
            result.Confidence = 0.20;
            result.EvidenceAlignment = EvidenceAlignmentLevel.Weak;

            KeyIssue issue = Assert.Single(KeyIssueRanker.RankIssues(new[] { result }, RequirementDiscipline.All, 10));

            Assert.NotEqual("Low", issue.SeverityLabel);
        }

        [Fact]
        public void RankIssues_LevelCleanupDoesNotDominateHighRiskSemanticIssue()
        {
            RequirementCheckResult level = Result(RequirementCheckStatus.NotMet, "level_location_mounting_placement");
            level.MatchedElementIds = new List<long> { 1, 2, 3, 4, 5 };
            level.MatchedModelElementCount = 5;

            RequirementCheckResult ground = Result(RequirementCheckStatus.NeedsHumanReview, "grounding_bonding_conductors");
            ground.Confidence = 0.25;
            ground.EvidenceAlignment = EvidenceAlignmentLevel.Weak;
            ground.MatchedModelElementCount = 0;

            List<KeyIssue> issues = KeyIssueRanker.RankIssues(new[] { level, ground }, RequirementDiscipline.All, 10);

            Assert.Equal("grounding_bonding_conductors", issues[0].RequirementType);
        }

        private static RequirementCheckResult Result(RequirementCheckStatus status, string requirementType)
        {
            return new RequirementCheckResult
            {
                RequirementId = "REQ-" + requirementType,
                RequirementText = requirementType.Replace('_', ' '),
                SourceWorksheet = "Electrical",
                SourceRow = 1,
                Discipline = "Electrical",
                ResponsibleRole = "Electrical",
                Status = status,
                Confidence = 0.70,
                EvidenceAlignment = EvidenceAlignmentLevel.Strong,
                RequirementType = requirementType,
                CandidateScopeValid = true,
                MatchedModelElementCount = 2,
                MatchedElementIds = new List<long> { 1001 },
                MissingExpectedParameters = new List<string> { "Panel" },
                EvidenceSummary = "2 scoped elements inspected.",
                Reasoning = "Test reasoning.",
                NextBestAction = "Populate the missing parameters."
            };
        }
    }
}
