using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using EMAExtractor.Requirements;

namespace EMAExtractor.Services
{
    public sealed class MatrixAuditEntry
    {
        public int SourceRow { get; set; }
        public string RequirementId { get; set; }
        public string RequirementText { get; set; }
        public string Discipline { get; set; }
        public string AssignedType { get; set; }
        public string AssignedTypeReason { get; set; }
        public string Status { get; set; }
        public string Reasoning { get; set; }
        public string NextBestAction { get; set; }
        public string EvidenceAlignment { get; set; }
        public string WhyNotModelCloseable { get; set; }
        public string ModelEvidenceSufficiency { get; set; }
        public bool IsUnknown { get; set; }
        public bool ScopeWarning { get; set; }
        public string ScopeWarningReason { get; set; }
        public bool MatchedTaxonomy { get; set; }
    }

    public sealed class MatrixAuditReport
    {
        public int TotalRequirements { get; set; }
        public int KnownTypeCount { get; set; }
        public int UnknownAmbiguousCount { get; set; }
        public int ScopeWarningCount { get; set; }
        public int FalseMetRiskCount { get; set; }
        public int GenericNextActionCount { get; set; }
        public int ContradictionCount { get; set; }
        public int NotMetWrongReasonCount { get; set; }
        public double TaxonomyCoveragePercent { get; set; }
        public Dictionary<string, int> TypeDistribution { get; set; } = new Dictionary<string, int>();
        public List<MatrixAuditEntry> Entries { get; set; } = new List<MatrixAuditEntry>();
        public List<MatrixAuditEntry> UnknownEntries { get; set; } = new List<MatrixAuditEntry>();
        public List<MatrixAuditEntry> ScopeWarningEntries { get; set; } = new List<MatrixAuditEntry>();
        public List<MatrixAuditEntry> FalseMetRiskEntries { get; set; } = new List<MatrixAuditEntry>();
        public List<MatrixAuditEntry> GenericNextActionEntries { get; set; } = new List<MatrixAuditEntry>();
        public List<MatrixAuditEntry> ContradictionEntries { get; set; } = new List<MatrixAuditEntry>();
        public List<MatrixAuditEntry> NotMetWrongReasonEntries { get; set; } = new List<MatrixAuditEntry>();
        public List<int> RowsToReview { get; set; } = new List<int>();
        public List<string> RecommendedTypeUpdates { get; set; } = new List<string>();
        public List<string> CandidateScopeWarnings { get; set; } = new List<string>();
        public string TaxonomyVersion { get; set; }
        public string AuditTimestamp { get; set; }
    }

    public static class RequirementMatrixAuditService
    {
        private static readonly string[] ScopeWarningTypes = new[]
        {
            "level_location_mounting_placement",
            "unknown_ambiguous"
        };

        public static MatrixAuditReport Audit(IEnumerable<RequirementCheckResult> results, string taxonomyPath = null)
        {
            string taxonomyVersion = LoadTaxonomyVersion(taxonomyPath);

            var report = new MatrixAuditReport
            {
                TaxonomyVersion = taxonomyVersion,
                AuditTimestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            List<RequirementCheckResult> list = results?.Where(r => r != null).ToList()
                ?? new List<RequirementCheckResult>();

            report.TotalRequirements = list.Count;

            foreach (RequirementCheckResult result in list)
            {
                string reqType = result.RequirementType ?? "unknown_ambiguous";
                bool isUnknown = string.Equals(reqType, "unknown_ambiguous", StringComparison.OrdinalIgnoreCase);

                bool scopeWarning = ScopeWarningTypes.Any(t =>
                    string.Equals(t, reqType, StringComparison.OrdinalIgnoreCase));

                string scopeWarningReason = null;
                if (scopeWarning)
                {
                    if (string.Equals(reqType, "level_location_mounting_placement", StringComparison.OrdinalIgnoreCase))
                    {
                        scopeWarningReason = "Assigned to level_location_mounting_placement (Priority=10 fallback). " +
                            "Review whether a higher-priority plumbing, electrical, or specification rule should match.";
                    }
                    else
                    {
                        scopeWarningReason = "Classified as unknown_ambiguous — no deterministic rule matched. " +
                            "AI classification or human review is recommended.";
                    }
                }

                var entry = new MatrixAuditEntry
                {
                    SourceRow = result.SourceRow > 0 ? result.SourceRow : (result.Requirement?.RowNumber ?? 0),
                    RequirementId = result.RequirementId ?? result.Requirement?.RequirementId ?? string.Empty,
                    RequirementText = result.RequirementText ?? result.Requirement?.RequirementText ?? string.Empty,
                    Discipline = result.Discipline ?? result.Requirement?.Discipline ?? "Unknown",
                    AssignedType = reqType,
                    AssignedTypeReason = result.RequirementTypeReason ?? string.Empty,
                    Status = result.Status.ToString(),
                    Reasoning = result.Reasoning ?? string.Empty,
                    NextBestAction = result.NextBestAction ?? string.Empty,
                    EvidenceAlignment = result.EvidenceAlignmentLabel ?? string.Empty,
                    WhyNotModelCloseable = result.WhyNotModelCloseable ?? string.Empty,
                    ModelEvidenceSufficiency = result.ModelEvidenceSufficiency ?? string.Empty,
                    IsUnknown = isUnknown,
                    ScopeWarning = scopeWarning,
                    ScopeWarningReason = scopeWarningReason,
                    MatchedTaxonomy = !isUnknown
                };

                report.Entries.Add(entry);

                if (isUnknown)
                {
                    report.UnknownEntries.Add(entry);
                }

                if (scopeWarning)
                {
                    report.ScopeWarningEntries.Add(entry);
                }

                bool genericNextAction = IsGenericNextAction(result.NextBestAction);
                bool hasDirectClosingEvidence = result.DirectClosingEvidence != null &&
                    result.DirectClosingEvidence.Any(item => !string.IsNullOrWhiteSpace(item));
                bool falseMetRisk = result.Status == RequirementCheckStatus.Met &&
                    (string.Equals(result.EvidenceAlignmentLabel, "Weak", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(result.EvidenceAlignmentLabel, "Mismatch Risk", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(result.EvidenceAlignmentLabel, "Manual Only", StringComparison.OrdinalIgnoreCase) ||
                     genericNextAction ||
                     !hasDirectClosingEvidence);

                bool notMetWrongReason = result.Status == RequirementCheckStatus.NotMet &&
                    (
                        (!string.IsNullOrWhiteSpace(result.Reasoning) && result.Reasoning.IndexOf("marked as Met", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrWhiteSpace(result.NextBestAction) && result.NextBestAction.IndexOf("No action required", StringComparison.OrdinalIgnoreCase) >= 0)
                    );

                bool contradiction = (result.Status == RequirementCheckStatus.Met && !string.IsNullOrWhiteSpace(result.WhyNotModelCloseable)) ||
                    (result.Status != RequirementCheckStatus.Met && !string.IsNullOrWhiteSpace(result.NextBestAction) && result.NextBestAction.IndexOf("No action required", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (result.Status == RequirementCheckStatus.Met && (string.Equals(result.EvidenceAlignmentLabel, "Weak", StringComparison.OrdinalIgnoreCase) || string.Equals(result.EvidenceAlignmentLabel, "Mismatch Risk", StringComparison.OrdinalIgnoreCase)));

                if (falseMetRisk)
                {
                    report.FalseMetRiskCount++;
                    report.FalseMetRiskEntries.Add(entry);
                }

                if (genericNextAction)
                {
                    report.GenericNextActionCount++;
                    report.GenericNextActionEntries.Add(entry);
                }

                if (contradiction)
                {
                    report.ContradictionCount++;
                    report.ContradictionEntries.Add(entry);
                }

                if (notMetWrongReason)
                {
                    report.NotMetWrongReasonCount++;
                    report.NotMetWrongReasonEntries.Add(entry);
                }

                if (scopeWarning || isUnknown || falseMetRisk || contradiction || notMetWrongReason)
                {
                    int row = entry.SourceRow;
                    if (row > 0 && !report.RowsToReview.Contains(row))
                    {
                        report.RowsToReview.Add(row);
                    }
                }

                if (isUnknown || scopeWarning)
                {
                    string updateHint = entry.SourceRow > 0
                        ? "Row " + entry.SourceRow + ": " + entry.AssignedType + (string.IsNullOrWhiteSpace(entry.AssignedTypeReason) ? string.Empty : " - " + entry.AssignedTypeReason)
                        : entry.AssignedType;

                    if (!string.IsNullOrWhiteSpace(updateHint) && !report.RecommendedTypeUpdates.Contains(updateHint))
                    {
                        report.RecommendedTypeUpdates.Add(updateHint);
                    }
                }

                if (scopeWarning && !string.IsNullOrWhiteSpace(scopeWarningReason) && !report.CandidateScopeWarnings.Contains(scopeWarningReason))
                {
                    report.CandidateScopeWarnings.Add(scopeWarningReason);
                }

                if (!report.TypeDistribution.ContainsKey(reqType))
                {
                    report.TypeDistribution[reqType] = 0;
                }

                report.TypeDistribution[reqType]++;
            }

            report.UnknownAmbiguousCount = report.UnknownEntries.Count;
            report.ScopeWarningCount = report.ScopeWarningEntries.Count;
            report.KnownTypeCount = report.TotalRequirements - report.UnknownAmbiguousCount;
            report.TaxonomyCoveragePercent = report.TotalRequirements == 0
                ? 0.0
                : Math.Round(100.0 * report.KnownTypeCount / report.TotalRequirements, 1);

            return report;
        }

        private static bool IsGenericNextAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return true;
            }

            string lower = action.ToLowerInvariant();
            string[] genericPhrases =
            {
                "review the requirement manually",
                "review this requirement manually",
                "review this item manually",
                "no action required",
                "assign the correct level values",
                "confirm the owner interpretation",
                "double-check",
                "re-run the check",
                "check the model",
                "review against the relevant specification"
            };

            return genericPhrases.Any(lower.Contains);
        }

        private static string LoadTaxonomyVersion(string taxonomyPath)
        {
            if (string.IsNullOrWhiteSpace(taxonomyPath))
            {
                taxonomyPath = FindTaxonomyPath();
            }

            if (string.IsNullOrWhiteSpace(taxonomyPath) || !File.Exists(taxonomyPath))
            {
                return "unknown";
            }

            try
            {
                string json = File.ReadAllText(taxonomyPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("taxonomy_version", out JsonElement vEl) &&
                        vEl.ValueKind == JsonValueKind.String)
                    {
                        return vEl.GetString() ?? "unknown";
                    }
                }
            }
            catch { }

            return "unknown";
        }

        public static MatrixAuditReport AuditFromReportHtml(string htmlPath, string taxonomyPath = null)
        {
            if (string.IsNullOrWhiteSpace(htmlPath) || !File.Exists(htmlPath))
                return null;

            try
            {
                string html = File.ReadAllText(htmlPath);
                Match m = Regex.Match(
                    html,
                    @"<script[^>]+id=""ema-ai-report-context""[^>]*>([\s\S]*?)</script>",
                    RegexOptions.IgnoreCase);

                if (!m.Success) return null;

                string json = m.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(json)) return null;

                List<RequirementCheckResult> results = ParseHiddenJsonResults(json);
                return Audit(results, taxonomyPath);
            }
            catch
            {
                return null;
            }
        }

        private static List<RequirementCheckResult> ParseHiddenJsonResults(string json)
        {
            var results = new List<RequirementCheckResult>();
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;

                JsonElement reqsEl;
                bool hasReqs = root.TryGetProperty("requirement_results", out reqsEl) ||
                               root.TryGetProperty("requirements", out reqsEl);
                if (!hasReqs || reqsEl.ValueKind != JsonValueKind.Array)
                    return results;

                foreach (JsonElement el in reqsEl.EnumerateArray())
                {
                    string reqType = GetElString(el, "requirement_type") ?? GetElString(el, "requirementType") ?? "unknown_ambiguous";
                    string statusStr = GetElString(el, "status") ?? "NeedsHumanReview";
                    string discipline = GetElString(el, "discipline") ?? "Unknown";
                    string reqText = GetElString(el, "requirement_text") ?? GetElString(el, "requirementText") ?? string.Empty;

                    int sourceRow = 0;
                    if (el.TryGetProperty("source_row", out JsonElement srEl))
                    {
                        if (srEl.ValueKind == JsonValueKind.Number) sourceRow = srEl.GetInt32();
                        else if (srEl.ValueKind == JsonValueKind.String) int.TryParse(srEl.GetString(), out sourceRow);
                    }

                    RequirementCheckStatus status = ParseStatus(statusStr);

                    results.Add(new RequirementCheckResult
                    {
                        SourceRow = sourceRow,
                        RequirementType = reqType,
                        Discipline = discipline,
                        RequirementText = reqText,
                        Status = status,
                        Requirement = new OwnerRequirementRow
                        {
                            RequirementText = reqText,
                            Discipline = discipline
                        }
                    });
                }
            }
            return results;
        }

        private static RequirementCheckStatus ParseStatus(string s)
        {
            if (string.Equals(s, "Met", StringComparison.OrdinalIgnoreCase)) return RequirementCheckStatus.Met;
            if (string.Equals(s, "NotMet", StringComparison.OrdinalIgnoreCase)) return RequirementCheckStatus.NotMet;
            if (string.Equals(s, "NeedsHumanReview", StringComparison.OrdinalIgnoreCase)) return RequirementCheckStatus.NeedsHumanReview;
            if (string.Equals(s, "InsufficientModelData", StringComparison.OrdinalIgnoreCase)) return RequirementCheckStatus.InsufficientModelData;
            if (string.Equals(s, "NotApplicable", StringComparison.OrdinalIgnoreCase)) return RequirementCheckStatus.NotApplicable;
            return RequirementCheckStatus.NeedsHumanReview;
        }

        private static string GetElString(JsonElement el, string key)
        {
            if (el.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            return null;
        }

        private static string FindTaxonomyPath()
        {
            string[] candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "taxonomy", "requirement_type_matrix.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "data", "taxonomy", "requirement_type_matrix.json"),
                Path.Combine(LoggingService.AppRoot ?? string.Empty, "data", "taxonomy", "requirement_type_matrix.json")
            };

            foreach (string candidate in candidates)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch { }
            }

            return null;
        }
    }
}
