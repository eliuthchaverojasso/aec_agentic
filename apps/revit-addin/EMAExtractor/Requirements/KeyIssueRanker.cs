using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EMAExtractor.Requirements
{
    public enum IssueSeverity
    {
        Critical,
        High,
        Medium,
        Low,
        Info,
        NeedsReview
    }

    public class KeyIssue
    {
        public int Rank { get; set; }
        public string IssueTitle { get; set; }
        public RequirementCheckStatus Status { get; set; }
        public string Discipline { get; set; }
        public string ResponsibleRole { get; set; }
        public string RequirementId { get; set; }
        public string SourceFile { get; set; }
        public string SourceWorksheet { get; set; }
        public int SourceRow { get; set; }
        public string EvidenceSummary { get; set; }
        public string Reasoning { get; set; }
        public string NextBestAction { get; set; }
        public double Confidence { get; set; }
        public double KeyIssueScore { get; set; }
        public IssueSeverity Severity { get; set; }
        public string RequirementType { get; set; }
        public string UrgencyReason { get; set; }
        public string KeyIssueScoreReason { get; set; }
        public double StatusSeverityScore { get; set; }
        public double DeliverableImpactScore { get; set; }
        public double ActionabilityScore { get; set; }
        public double EvidenceGapScore { get; set; }
        public double RequirementTypeRiskScore { get; set; }
        public double ImpactScaleScore { get; set; }
        public bool CandidateScopeValid { get; set; }
        public bool FullModelFallbackUsed { get; set; }
        public int AffectedScopedElements { get; set; }
        public bool HumanReviewNeeded { get; set; }
        public bool ModelEvidenceSufficient { get; set; }
        public string EvidenceGap { get; set; }
        public string Actionability { get; set; }

        public string SeverityLabel => Severity switch
        {
            IssueSeverity.Critical => "Critical",
            IssueSeverity.High => "High",
            IssueSeverity.Medium => "Medium",
            IssueSeverity.Low => "Low",
            IssueSeverity.NeedsReview => "Needs Review",
            IssueSeverity.Info => "Low",
            _ => "Unknown"
        };
    }

    public static class KeyIssueRanker
    {
        public static List<KeyIssue> RankIssues(
            IEnumerable<RequirementCheckResult> results,
            RequirementDiscipline selectedDiscipline,
            int topIssueCount = 10)
        {
            if (results == null)
            {
                return new List<KeyIssue>();
            }

            var issues = new List<KeyIssue>();

            foreach (var result in results)
            {
                if (result == null)
                {
                    continue;
                }

                if (result.Status == RequirementCheckStatus.Met ||
                    result.Status == RequirementCheckStatus.NotApplicable)
                {
                    continue;
                }

                KeyIssueScoreBreakdown breakdown = CalculateKeyIssueScore(result);

                if (breakdown.Score > 0.0)
                {
                    var issue = new KeyIssue
                    {
                        IssueTitle = result.IssueTitle ?? result.Requirement?.RequirementText ?? "(unknown)",
                        Status = result.Status,
                        Discipline = NormalizeDisciplineLabel(!string.IsNullOrWhiteSpace(result.Discipline) ? result.Discipline : result.Requirement?.Discipline),
                        ResponsibleRole = result.ResponsibleRole ?? "TBD",
                        RequirementId = !string.IsNullOrWhiteSpace(result.RequirementId) ? result.RequirementId : result.Requirement?.RequirementId ?? "N/A",
                        SourceFile = !string.IsNullOrWhiteSpace(result.SourceFile) ? result.SourceFile : result.Requirement?.SourceFile ?? string.Empty,
                        SourceWorksheet = result.SourceWorksheet ?? result.Requirement?.SourceSheet ?? "Unknown",
                        SourceRow = result.SourceRow > 0 ? result.SourceRow : result.Requirement?.RowNumber ?? 0,
                        EvidenceSummary = result.EvidenceSummary ?? "No evidence found",
                        Reasoning = result.Reasoning ?? "Status was assigned based on available model data and requirement classification.",
                        NextBestAction = result.NextBestAction ?? "Review requirement manually.",
                        Confidence = result.Confidence,
                        KeyIssueScore = breakdown.Score,
                        Severity = DetermineSeverity(result, breakdown),
                        RequirementType = result.RequirementType ?? "unknown_ambiguous",
                        UrgencyReason = BuildUrgencyReason(result, breakdown),
                        KeyIssueScoreReason = breakdown.Reason,
                        StatusSeverityScore = breakdown.StatusSeverity,
                        DeliverableImpactScore = breakdown.DeliverableImpact,
                        ActionabilityScore = breakdown.Actionability,
                        EvidenceGapScore = breakdown.EvidenceGap,
                        RequirementTypeRiskScore = breakdown.RequirementTypeRisk,
                        ImpactScaleScore = breakdown.ImpactScale,
                        CandidateScopeValid = breakdown.CandidateScopeValid,
                        FullModelFallbackUsed = breakdown.FullModelFallbackUsed,
                        AffectedScopedElements = breakdown.AffectedScopedElements,
                        HumanReviewNeeded = result.HumanReviewNeeded || result.Status == RequirementCheckStatus.NeedsHumanReview,
                        ModelEvidenceSufficient = result.EvidenceAlignment == EvidenceAlignmentLevel.Strong,
                        EvidenceGap = DescribeEvidenceGap(breakdown.EvidenceGap),
                        Actionability = DescribeActionability(breakdown.Actionability)
                    };

                    result.KeyIssueScore = issue.KeyIssueScore;
                    result.Urgency = issue.SeverityLabel;
                    result.IsKeyIssue = true;
                    result.UrgencyReason = issue.UrgencyReason;
                    result.KeyIssueScoreReason = issue.KeyIssueScoreReason;
                    result.StatusSeverityScore = issue.StatusSeverityScore;
                    result.DeliverableImpactScore = issue.DeliverableImpactScore;
                    result.ActionabilityScore = issue.ActionabilityScore;
                    result.EvidenceGapScore = issue.EvidenceGapScore;
                    result.RequirementTypeRiskScore = issue.RequirementTypeRiskScore;
                    result.ImpactScaleScore = issue.ImpactScaleScore;
                    result.CandidateScopeValid = issue.CandidateScopeValid;
                    result.FullModelFallbackUsed = issue.FullModelFallbackUsed;

                    issues.Add(issue);
                }
            }

            issues = issues
                .OrderBy(issue => SeveritySortOrder(issue.Severity))
                .ThenByDescending(issue => issue.KeyIssueScore)
                .ThenBy(issue => issue.SourceRow)
                .ToList();

            for (int i = 0; i < issues.Count && i < topIssueCount; i++)
            {
                issues[i].Rank = i + 1;
            }

            return issues.Take(topIssueCount).ToList();
        }

        private sealed class KeyIssueScoreBreakdown
        {
            public double Score { get; set; }
            public double StatusSeverity { get; set; }
            public double DeliverableImpact { get; set; }
            public double Actionability { get; set; }
            public double EvidenceGap { get; set; }
            public double RequirementTypeRisk { get; set; }
            public double ImpactScale { get; set; }
            public bool CandidateScopeValid { get; set; }
            public bool FullModelFallbackUsed { get; set; }
            public int AffectedScopedElements { get; set; }
            public string Reason { get; set; }
        }

        private static KeyIssueScoreBreakdown CalculateKeyIssueScore(RequirementCheckResult result)
        {
            var breakdown = new KeyIssueScoreBreakdown
            {
                StatusSeverity = CalculateStatusSeverity(result),
                DeliverableImpact = CalculateDeliverableImpact(result),
                Actionability = CalculateActionability(result),
                EvidenceGap = CalculateEvidenceGap(result),
                RequirementTypeRisk = CalculateRequirementTypeRisk(result),
                CandidateScopeValid = IsCandidateScopeValid(result),
                FullModelFallbackUsed = result != null && (result.FullModelFallbackUsed || result.FallbackUsed),
                AffectedScopedElements = result == null ? 0 : Math.Max(0, result.MatchedModelElementCount)
            };

            breakdown.ImpactScale = CalculateImpactScale(breakdown);
            double score =
                0.25 * breakdown.StatusSeverity
                + 0.20 * breakdown.DeliverableImpact
                + 0.20 * breakdown.Actionability
                + 0.15 * breakdown.EvidenceGap
                + 0.10 * breakdown.RequirementTypeRisk
                + 0.10 * breakdown.ImpactScale;

            if (result != null &&
                result.Confidence > 0.85 &&
                result.Status == RequirementCheckStatus.NotMet &&
                breakdown.CandidateScopeValid)
            {
                score *= 1.05;
            }

            if (result != null && result.Confidence < 0.35 && breakdown.RequirementTypeRisk < 0.45)
            {
                score *= 0.90;
            }

            breakdown.Score = Clamp(score);
            breakdown.Reason = string.Format(
                CultureInfo.InvariantCulture,
                "Score = 0.25*{0:0.00} status severity + 0.20*{1:0.00} deliverable impact + 0.20*{2:0.00} actionability + 0.15*{3:0.00} evidence gap + 0.10*{4:0.00} requirement type risk + 0.10*{5:0.00} impact scale.",
                breakdown.StatusSeverity,
                breakdown.DeliverableImpact,
                breakdown.Actionability,
                breakdown.EvidenceGap,
                breakdown.RequirementTypeRisk,
                breakdown.ImpactScale);
            return breakdown;
        }

        private static double CalculateStatusSeverity(RequirementCheckResult result)
        {
            if (result == null)
            {
                return 0.0;
            }

            switch (result.Status)
            {
                case RequirementCheckStatus.NotMet:
                    return 1.0;
                case RequirementCheckStatus.InsufficientModelData:
                    return 0.78;
                case RequirementCheckStatus.NeedsHumanReview:
                    return Math.Max(0.50, 0.45 + (CalculateRequirementTypeRisk(result) * 0.35) + (CalculateEvidenceGap(result) * 0.20));
                default:
                    return 0.0;
            }
        }

        private static double CalculateDisciplineRelevance(
            RequirementCheckResult result,
            RequirementDiscipline selectedDiscipline)
        {
            string reqDiscipline = result.Requirement?.Discipline ?? "Unknown";
            string selectedName = selectedDiscipline.ToString();

            if (string.Equals(reqDiscipline, selectedName, StringComparison.OrdinalIgnoreCase))
            {
                return 1.0;
            }

            if (selectedDiscipline == RequirementDiscipline.All)
            {
                return 0.75;
            }

            if (IsDisciplineRelated(reqDiscipline, selectedName))
            {
                return 0.40;
            }

            return 0.0;
        }

        private static bool IsDisciplineRelated(string discipline1, string discipline2)
        {
            var relatedMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Electrical", new List<string> { "Lighting", "Technology" } },
                { "Lighting", new List<string> { "Electrical" } },
                { "Technology", new List<string> { "Electrical" } },
                { "Mechanical", new List<string> { "Plumbing" } },
                { "Plumbing", new List<string> { "Mechanical" } }
            };

            if (relatedMap.ContainsKey(discipline1))
            {
                return relatedMap[discipline1].Any(d => string.Equals(d, discipline2, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        private static string NormalizeDisciplineLabel(string discipline)
        {
            RequirementDiscipline parsed = RequirementDisciplineNormalizer.Parse(discipline, RequirementDiscipline.All);
            return parsed == RequirementDiscipline.All ? "Unknown / Needs Classification" : parsed.ToString();
        }

        private static double CalculateDeliverableImpact(RequirementCheckResult result)
        {
            if (result == null) return 0.0;

            double risk = CalculateRequirementTypeRisk(result);
            if (result.Status == RequirementCheckStatus.NotMet)
            {
                return Math.Max(0.75, risk);
            }

            if (result.Status == RequirementCheckStatus.NeedsHumanReview)
            {
                return Math.Max(0.55, risk * 0.9);
            }

            if (result.Status == RequirementCheckStatus.InsufficientModelData)
            {
                return Math.Max(0.65, risk * 0.85);
            }

            return 0.0;
        }

        private static double CalculateEvidenceGap(RequirementCheckResult result)
        {
            if (result == null) return 0.0;

            if (result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly ||
                result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk ||
                result.MissingExpectedParameters?.Count > 0)
            {
                return 1.0;
            }

            if (result.EvidenceAlignment == EvidenceAlignmentLevel.Weak ||
                result.Status == RequirementCheckStatus.InsufficientModelData)
            {
                return 0.85;
            }

            if (result.EvidenceAlignment == EvidenceAlignmentLevel.Partial)
            {
                return 0.55;
            }

            return 0.20;
        }

        private static double CalculateActionability(RequirementCheckResult result)
        {
            if (result == null)
            {
                return 0.0;
            }

            string action = result.NextBestAction ?? string.Empty;
            bool directElements = result.MatchedElementIds != null && result.MatchedElementIds.Count > 0;
            bool missingParameters = result.MissingExpectedParameters != null && result.MissingExpectedParameters.Count > 0;

            if (directElements && missingParameters)
            {
                return 1.0;
            }

            if (directElements || missingParameters ||
                action.IndexOf("Assign", StringComparison.OrdinalIgnoreCase) >= 0 ||
                action.IndexOf("Populate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                action.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 0.75;
            }

            if (action.IndexOf("Review", StringComparison.OrdinalIgnoreCase) >= 0 ||
                action.IndexOf("Verify", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 0.55;
            }

            return 0.25;
        }

        private static double CalculateRequirementTypeRisk(RequirementCheckResult result)
        {
            string type = result?.RequirementType ?? string.Empty;
            string text = ((result?.RequirementText ?? string.Empty) + " " + (result?.IssueTitle ?? string.Empty)).ToLowerInvariant();

            if (EqualsType(type, "grounding_bonding_conductors") ||
                EqualsType(type, "field_execution_demolition_protection") ||
                EqualsType(type, "panel_circuit_power") ||
                text.Contains("fire alarm") ||
                text.Contains("life-safety") ||
                text.Contains("security"))
            {
                return 1.0;
            }

            if (EqualsType(type, "plumbing_hose_bibb_rpz_valves") ||
                EqualsType(type, "plumbing_flush_valve_product_spec") ||
                EqualsType(type, "plumbing_water_hammer_arrestor_requirement") ||
                EqualsType(type, "plumbing_support_hanger_requirement") ||
                EqualsType(type, "outlets_receptacles_devices") ||
                EqualsType(type, "conduit_raceway") ||
                text.Contains("power connection"))
            {
                return 0.85;
            }

            if (EqualsType(type, "plumbing_accessory_water_supply"))
            {
                return 0.70;
            }

            if (EqualsType(type, "manufacturer_product_spec_submittal") ||
                EqualsType(type, "identification_labeling_nameplate") ||
                EqualsType(type, "drawing_spec_manual_owner_approval") ||
                EqualsType(type, "technology_low_voltage_security_fire_alarm") ||
                EqualsType(type, "commissioning_testing_om_training") ||
                EqualsType(type, "controls_bms_bas_contactors_relays"))
            {
                return 0.65;
            }

            if (EqualsType(type, "level_location_mounting_placement"))
            {
                return 0.25;
            }

            return 0.40;
        }

        private static double CalculateImpactScale(KeyIssueScoreBreakdown breakdown)
        {
            if (breakdown == null || !breakdown.CandidateScopeValid || breakdown.FullModelFallbackUsed)
            {
                return 0.0;
            }

            return Clamp(Math.Log10(Math.Max(0, breakdown.AffectedScopedElements) + 1) / 2.0);
        }

        private static bool IsCandidateScopeValid(RequirementCheckResult result)
        {
            if (result == null)
            {
                return false;
            }

            if (result.FullModelFallbackUsed || result.FallbackUsed)
            {
                return false;
            }

            if (!result.CandidateScopeValid)
            {
                return false;
            }

            if (result.MatchedModelElementCount >= 20000 &&
                !string.Equals(result.RequirementType, "unknown_ambiguous", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static IssueSeverity DetermineSeverity(RequirementCheckResult result, KeyIssueScoreBreakdown breakdown)
        {
            if (result == null || breakdown == null)
            {
                return IssueSeverity.Low;
            }

            bool weak = result.EvidenceAlignment == EvidenceAlignmentLevel.Weak ||
                result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk ||
                result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly;

            if (result.Status == RequirementCheckStatus.NeedsHumanReview &&
                (weak || !breakdown.CandidateScopeValid) &&
                breakdown.RequirementTypeRisk < 0.90)
            {
                return IssueSeverity.NeedsReview;
            }

            if (breakdown.FullModelFallbackUsed && breakdown.Score >= 0.80)
            {
                return IssueSeverity.High;
            }

            if (breakdown.Score >= 0.80 && breakdown.RequirementTypeRisk >= 0.85 && breakdown.CandidateScopeValid)
            {
                return IssueSeverity.Critical;
            }

            if (breakdown.Score >= 0.60)
            {
                return IssueSeverity.High;
            }

            if (breakdown.Score >= 0.40)
            {
                return IssueSeverity.Medium;
            }

            return IssueSeverity.Low;
        }

        private static int SeveritySortOrder(IssueSeverity severity)
        {
            switch (severity)
            {
                case IssueSeverity.Critical: return 0;
                case IssueSeverity.High: return 1;
                case IssueSeverity.Medium: return 2;
                case IssueSeverity.NeedsReview: return 3;
                case IssueSeverity.Low: return 4;
                default: return 5;
            }
        }

        private static string BuildUrgencyReason(RequirementCheckResult result, KeyIssueScoreBreakdown breakdown)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Status: {0}. Requirement type: {1}. Type risk {2:0.00}; evidence gap {3:0.00}; actionability {4:0.00}; affected scoped elements {5}; discipline owner {6}; candidate scope valid: {7}; human review needed: {8}; model evidence sufficient: {9}.",
                result.StatusLabel,
                string.IsNullOrWhiteSpace(result.RequirementType) ? "unknown_ambiguous" : result.RequirementType,
                breakdown.RequirementTypeRisk,
                breakdown.EvidenceGap,
                breakdown.Actionability,
                breakdown.CandidateScopeValid ? breakdown.AffectedScopedElements : 0,
                NormalizeDisciplineLabel(!string.IsNullOrWhiteSpace(result.Discipline) ? result.Discipline : result.Requirement?.Discipline),
                breakdown.CandidateScopeValid ? "yes" : "no",
                result.HumanReviewNeeded || result.Status == RequirementCheckStatus.NeedsHumanReview ? "yes" : "no",
                result.EvidenceAlignment == EvidenceAlignmentLevel.Strong ? "yes" : "no");
        }

        private static string DescribeEvidenceGap(double score)
        {
            if (score >= 0.80) return "High: direct required evidence is missing, weak, mismatched, or external.";
            if (score >= 0.45) return "Medium: evidence is partial or context is incomplete.";
            return "Low: evidence is mostly complete.";
        }

        private static string DescribeActionability(double score)
        {
            if (score >= 0.80) return "High: direct elements or missing parameters are available for follow-up.";
            if (score >= 0.45) return "Medium: a clear review or discipline action exists.";
            return "Low: action is broad or depends on project context.";
        }

        private static bool EqualsType(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static double Clamp(double value)
        {
            return Math.Min(1.0, Math.Max(0.0, value));
        }
    }
}
