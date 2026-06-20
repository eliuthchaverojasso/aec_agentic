using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EMAExtractor.Requirements
{
    public enum ReadinessLabel
    {
        Ready,
        OnTrack,
        AtRisk,
        Behind,
        Critical
    }

    public class ReadinessMetrics
    {
        public double OverallScore { get; set; }
        public double RequirementCoverage { get; set; }
        public double EvidenceCoverage { get; set; }
        public double QAQCHealth { get; set; }
        public double DrawingSpecCoverage { get; set; }
        public double SyncFreshness { get; set; }
        public ReadinessLabel ReadinessLabel { get; set; }
        public string ReadinessDescription { get; set; }

        public string OverallScorePercentage => Math.Round(OverallScore * 100, 1).ToString(CultureInfo.InvariantCulture) + "%";
    }

    public static class ScoreCalculator
    {
        public static double CalculateOverallScore(
            IEnumerable<RequirementCheckResult> results,
            IDictionary<string, double> resultWeights = null)
        {
            var resultList = results?.ToList() ?? new List<RequirementCheckResult>();

            var applicable = resultList
                .Where(r => r != null && r.Status != RequirementCheckStatus.NotApplicable)
                .ToList();

            if (applicable.Count == 0)
            {
                return 0.0;
            }

            double totalWeightedScore = 0.0;
            double totalWeight = 0.0;

            foreach (var result in applicable)
            {
                double weight = resultWeights != null && resultWeights.ContainsKey(result.Requirement?.RequirementId ?? "")
                    ? resultWeights[result.Requirement.RequirementId]
                    : 1.0;

                double statusValue = GetStatusValue(result.Status);
                double requirementScore = statusValue * result.Confidence;

                totalWeightedScore += weight * requirementScore;
                totalWeight += weight;
            }

            if (totalWeight == 0)
            {
                return 0.0;
            }

            double overallScore = 100.0 * (totalWeightedScore / totalWeight);
            return Math.Min(100.0, Math.Max(0.0, overallScore));
        }

        public static double CalculateDisciplineScore(
            IEnumerable<RequirementCheckResult> results,
            RequirementDiscipline discipline,
            IDictionary<string, double> resultWeights = null)
        {
            var disciplineResults = results?
                .Where(r => r != null &&
                    r.Status != RequirementCheckStatus.NotApplicable &&
                    IsDisciplineMatch(!string.IsNullOrWhiteSpace(r.Discipline) ? r.Discipline : r.Requirement?.Discipline ?? "Unknown", discipline))
                .ToList() ?? new List<RequirementCheckResult>();

            if (disciplineResults.Count == 0)
            {
                return 0.0;
            }

            return CalculateOverallScore(disciplineResults, resultWeights);
        }

        public static ReadinessMetrics CalculateReadiness(
            IEnumerable<RequirementCheckResult> results,
            DateTime lastSyncTime,
            int criticalIssueCount = 0,
            int highIssueCount = 0,
            int mediumIssueCount = 0,
            int lowIssueCount = 0)
        {
            var resultList = results?.ToList() ?? new List<RequirementCheckResult>();

            var applicable = resultList
                .Where(r => r != null && r.Status != RequirementCheckStatus.NotApplicable)
                .ToList();

            var metrics = new ReadinessMetrics();

            metrics.RequirementCoverage = CalculateRequirementCoverage(applicable);
            metrics.EvidenceCoverage = CalculateEvidenceCoverage(applicable);
            metrics.QAQCHealth = CalculateQAQCHealth(criticalIssueCount, highIssueCount, mediumIssueCount, lowIssueCount);
            metrics.DrawingSpecCoverage = 0.75;
            metrics.SyncFreshness = CalculateSyncFreshness(lastSyncTime);

            metrics.OverallScore =
                0.40 * metrics.RequirementCoverage
                + 0.25 * metrics.EvidenceCoverage
                + 0.20 * metrics.QAQCHealth
                + 0.10 * metrics.DrawingSpecCoverage
                + 0.05 * metrics.SyncFreshness;

            metrics.OverallScore = Math.Min(100.0, Math.Max(0.0, metrics.OverallScore));

            metrics.ReadinessLabel = AssignReadinessLabel(metrics.OverallScore);
            metrics.ReadinessDescription = BuildReadinessDescription(metrics);

            return metrics;
        }

        private static double CalculateRequirementCoverage(IReadOnlyList<RequirementCheckResult> applicableResults)
        {
            if (applicableResults.Count == 0)
            {
                return 0.0;
            }

            int met = applicableResults.Count(r => r.Status == RequirementCheckStatus.Met);
            int needsReview = applicableResults.Count(r => r.Status == RequirementCheckStatus.NeedsHumanReview);
            int insufficientData = applicableResults.Count(r => r.Status == RequirementCheckStatus.InsufficientModelData);

            double coverage = (met + 0.5 * needsReview + 0.3 * insufficientData) / (double)applicableResults.Count;
            return Math.Min(1.0, Math.Max(0.0, coverage));
        }

        private static double CalculateEvidenceCoverage(IReadOnlyList<RequirementCheckResult> applicableResults)
        {
            if (applicableResults.Count == 0)
            {
                return 0.0;
            }

            double totalEvidenceStrength = applicableResults.Sum(r => r.Confidence * GetEvidenceStrengthFactor(r.Status));
            return Math.Min(1.0, totalEvidenceStrength / applicableResults.Count);
        }

        private static double GetEvidenceStrengthFactor(RequirementCheckStatus status)
        {
            return status switch
            {
                RequirementCheckStatus.Met => 1.0,
                RequirementCheckStatus.NeedsHumanReview => 0.5,
                RequirementCheckStatus.InsufficientModelData => 0.3,
                RequirementCheckStatus.NotMet => 0.1,
                RequirementCheckStatus.NotApplicable => 0.0,
                _ => 0.0
            };
        }

        private static double CalculateQAQCHealth(
            int criticalIssues,
            int highIssues,
            int mediumIssues,
            int lowIssues)
        {
            double penalty = Math.Min(1.0,
                0.15 * criticalIssues +
                0.05 * highIssues +
                0.02 * mediumIssues +
                0.005 * lowIssues);

            return Math.Max(0.0, 1.0 - penalty);
        }

        private static double CalculateSyncFreshness(DateTime lastSyncTime)
        {
            if (lastSyncTime == default(DateTime))
            {
                return 0.0;
            }

            TimeSpan elapsed = DateTime.Now - lastSyncTime;

            if (elapsed.TotalHours <= 24)
            {
                return 1.0;
            }

            if (elapsed.TotalHours <= 72)
            {
                return 0.85;
            }

            if (elapsed.TotalDays <= 7)
            {
                return 0.65;
            }

            if (elapsed.TotalDays <= 14)
            {
                return 0.40;
            }

            return 0.0;
        }

        private static ReadinessLabel AssignReadinessLabel(double overallScore)
        {
            if (overallScore >= 90)
            {
                return ReadinessLabel.Ready;
            }

            if (overallScore >= 75)
            {
                return ReadinessLabel.OnTrack;
            }

            if (overallScore >= 60)
            {
                return ReadinessLabel.AtRisk;
            }

            if (overallScore >= 40)
            {
                return ReadinessLabel.Behind;
            }

            return ReadinessLabel.Critical;
        }

        private static string BuildReadinessDescription(ReadinessMetrics metrics)
        {
            return metrics.ReadinessLabel switch
            {
                ReadinessLabel.Ready => "Project is ready or very close to delivery readiness.",
                ReadinessLabel.OnTrack => "Project is on track. Most requirements are met or have clear next actions.",
                ReadinessLabel.AtRisk => "Project has moderate gaps. Prioritize resolving key issues before delivery.",
                ReadinessLabel.Behind => "Project has significant gaps. Intensive effort required to reach delivery readiness.",
                ReadinessLabel.Critical => "Project has critical gaps. Immediate intervention and planning required.",
                _ => "Readiness status is unclear."
            };
        }

        private static double GetStatusValue(RequirementCheckStatus status)
        {
            return status switch
            {
                RequirementCheckStatus.Met => 1.0,
                RequirementCheckStatus.NeedsHumanReview => 0.55,
                RequirementCheckStatus.InsufficientModelData => 0.40,
                RequirementCheckStatus.NotMet => 0.0,
                RequirementCheckStatus.NotApplicable => 0.0,
                _ => 0.0
            };
        }

        private static bool IsDisciplineMatch(string requirementDiscipline, RequirementDiscipline selectedDiscipline)
        {
            if (selectedDiscipline == RequirementDiscipline.All)
            {
                return true;
            }

            string disciplineName = selectedDiscipline.ToString();
            if (string.Equals(requirementDiscipline, disciplineName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(requirementDiscipline, "Unknown / Needs Classification", StringComparison.OrdinalIgnoreCase) &&
                selectedDiscipline == RequirementDiscipline.All)
            {
                return true;
            }

            return false;
        }
    }
}
