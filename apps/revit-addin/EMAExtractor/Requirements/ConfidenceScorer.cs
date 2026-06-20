using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EMAExtractor.Requirements
{
    public class ConfidenceScoringContext
    {
        public string RequirementText { get; set; }
        public string DisciplineSource { get; set; }
        public ValidationType ValidationType { get; set; }
        public EvidenceAlignmentLevel EvidenceAlignment { get; set; }
        public bool DisciplineMatchFound { get; set; }
        public bool ExplicitDisciplineColumn { get; set; }
        public double EvidenceStrength { get; set; }
        public int MatchedEvidenceCount { get; set; }
        public int MissingExpectedParameterCount { get; set; }
        public bool DirectParameterEvidenceFound { get; set; }
        public bool HumanReviewNeeded { get; set; }
        public bool AmbiguousText { get; set; }
        public bool ParametersComplete { get; set; }
    }

    public class ConfidenceScore
    {
        public double DisciplineConfidence { get; set; }
        public double RequirementClarity { get; set; }
        public double EvidenceStrength { get; set; }
        public double RuleSpecificity { get; set; }
        public double DataCompleteness { get; set; }
        public double SourceTraceability { get; set; }
        public double OverallScore { get; set; }
        public string Reasoning { get; set; }

        public string ToPercentage()
        {
            return Math.Round(OverallScore * 100, 1).ToString(CultureInfo.InvariantCulture) + "%";
        }
    }

    public static class ConfidenceScorer
    {
        public static ConfidenceScore Calculate(
            OwnerRequirementRow requirement,
            ConfidenceScoringContext context,
            IEnumerable<string> matchedEvidenceSummary)
        {
            if (requirement == null || context == null)
            {
                return new ConfidenceScore
                {
                    OverallScore = 0.0
                };
            }

            var score = new ConfidenceScore();

            score.DisciplineConfidence = CalculateDisciplineConfidence(context);
            score.RequirementClarity = CalculateRequirementClarity(context);
            score.EvidenceStrength = CalculateEvidenceStrength(context, matchedEvidenceSummary);
            score.RuleSpecificity = CalculateRuleSpecificity(context);
            score.DataCompleteness = CalculateDataCompleteness(context);
            score.SourceTraceability = CalculateSourceTraceability(requirement);

            score.OverallScore =
                0.25 * score.DisciplineConfidence
                + 0.20 * score.RequirementClarity
                + 0.25 * score.EvidenceStrength
                + 0.15 * score.RuleSpecificity
                + 0.10 * score.DataCompleteness
                + 0.05 * score.SourceTraceability;

            score.OverallScore = ApplyEvidenceAlignmentAdjustments(score.OverallScore, context);
            score.OverallScore = Math.Min(1.0, Math.Max(0.0, score.OverallScore));
            score.Reasoning = BuildReasoning(score, context);

            return score;
        }

        private static double CalculateDisciplineConfidence(ConfidenceScoringContext context)
        {
            if (context.ExplicitDisciplineColumn && context.DisciplineMatchFound)
            {
                return 1.0;
            }

            if (context.DisciplineMatchFound)
            {
                return 0.85;
            }

            if (!string.IsNullOrWhiteSpace(context.DisciplineSource))
            {
                return 0.70;
            }

            return 0.50;
        }

        private static double CalculateRequirementClarity(ConfidenceScoringContext context)
        {
            if (string.IsNullOrWhiteSpace(context.RequirementText))
            {
                return 0.30;
            }

            if (context.AmbiguousText)
            {
                return 0.50;
            }

            if (context.RequirementText.Length < 20)
            {
                return 0.75;
            }

            return 1.0;
        }

        private static double CalculateEvidenceStrength(
            ConfidenceScoringContext context,
            IEnumerable<string> matchedEvidenceSummary)
        {
            var evidenceList = matchedEvidenceSummary?.ToList() ?? new List<string>();

            if (context.ValidationType == ValidationType.Model)
            {
                if (context.MatchedEvidenceCount == 0)
                {
                    return 0.0;
                }

                if (context.MatchedEvidenceCount == 1)
                {
                    return 0.5;
                }

                return context.EvidenceStrength > 0 ? context.EvidenceStrength : 0.75;
            }

            if (context.ValidationType == ValidationType.Manual ||
                context.ValidationType == ValidationType.Specification)
            {
                return 0.0;
            }

            if (context.ValidationType == ValidationType.Drawing)
            {
                return 0.0;
            }

            if (context.ValidationType == ValidationType.Hybrid)
            {
                return context.EvidenceStrength > 0 ? context.EvidenceStrength * 0.75 : 0.50;
            }

            return 0.25;
        }

        private static double CalculateRuleSpecificity(ConfidenceScoringContext context)
        {
            if (context.ValidationType == ValidationType.Model)
            {
                return 1.0;
            }

            if (context.ValidationType == ValidationType.Hybrid)
            {
                return 0.75;
            }

            if (context.ValidationType == ValidationType.Drawing ||
                context.ValidationType == ValidationType.Specification)
            {
                return 0.50;
            }

            return 0.30;
        }

        private static double CalculateDataCompleteness(ConfidenceScoringContext context)
        {
            if (context.ParametersComplete)
            {
                return 1.0;
            }

            if (context.MatchedEvidenceCount > 0)
            {
                return 0.75;
            }

            if (context.ValidationType == ValidationType.Model && context.MatchedEvidenceCount == 0)
            {
                return 0.25;
            }

            return 0.50;
        }

        private static double CalculateSourceTraceability(OwnerRequirementRow requirement)
        {
            bool hasRow = requirement?.RowNumber > 0;
            bool hasSheet = !string.IsNullOrWhiteSpace(requirement?.SourceSheet);
            bool hasFile = !string.IsNullOrWhiteSpace(requirement?.SourceFile);

            if (hasRow && hasSheet && hasFile)
            {
                return 1.0;
            }

            if (hasRow && hasSheet)
            {
                return 0.75;
            }

            return 0.50;
        }

        private static double ApplyEvidenceAlignmentAdjustments(double score, ConfidenceScoringContext context)
        {
            if (context == null)
            {
                return score;
            }

            switch (context.EvidenceAlignment)
            {
                case EvidenceAlignmentLevel.Strong:
                    score += 0.10;
                    break;
                case EvidenceAlignmentLevel.Partial:
                    score -= 0.05;
                    break;
                case EvidenceAlignmentLevel.Weak:
                    score -= 0.20;
                    break;
                case EvidenceAlignmentLevel.MismatchRisk:
                    score -= 0.30;
                    break;
                case EvidenceAlignmentLevel.ManualOnly:
                    score -= 0.25;
                    break;
            }

            if (context.HumanReviewNeeded)
            {
                score -= 0.10;
            }

            if (context.ValidationType == ValidationType.Manual ||
                context.ValidationType == ValidationType.Specification)
            {
                score -= 0.10;
            }

            if (!context.DirectParameterEvidenceFound)
            {
                score -= 0.05;
            }

            if (context.MissingExpectedParameterCount > 0)
            {
                score -= Math.Min(0.20, context.MissingExpectedParameterCount * 0.03);
            }

            return score;
        }

        private static string BuildReasoning(ConfidenceScore score, ConfidenceScoringContext context)
        {
            var parts = new List<string>
            {
                "Discipline confidence: " + Math.Round(score.DisciplineConfidence * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%",
                "Requirement clarity: " + Math.Round(score.RequirementClarity * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%",
                "Evidence strength: " + Math.Round(score.EvidenceStrength * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%",
                "Rule specificity: " + Math.Round(score.RuleSpecificity * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%",
                "Data completeness: " + Math.Round(score.DataCompleteness * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%",
                "Source traceability: " + Math.Round(score.SourceTraceability * 100.0, 1).ToString(CultureInfo.InvariantCulture) + "%"
            };

            if (context == null)
            {
                return string.Join("; ", parts);
            }

            parts.Add("Evidence alignment: " + context.EvidenceAlignment);

            if (context.MissingExpectedParameterCount > 0)
            {
                parts.Add("Missing expected parameters: " + context.MissingExpectedParameterCount.ToString(CultureInfo.InvariantCulture));
            }

            if (context.DirectParameterEvidenceFound)
            {
                parts.Add("Direct parameter evidence was found.");
            }
            else
            {
                parts.Add("Only broad or indirect evidence was available.");
            }

            if (context.HumanReviewNeeded)
            {
                parts.Add("Human review is needed.");
            }

            return string.Join(" ", parts);
        }
    }
}
