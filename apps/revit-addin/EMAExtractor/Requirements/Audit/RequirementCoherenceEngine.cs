using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EMAExtractor.Requirements.Audit
{
    /// <summary>
    /// Cross-requirement coherence auditor. Given every evaluated requirement it
    /// detects duplicates and conflicts between requirements and rolls the
    /// findings up per requirement type. It is deterministic and read-only: it
    /// never mutates a requirement status. The engine remains the authority on
    /// "Met / Not Met"; this layer only answers "do these obligations agree?".
    /// </summary>
    public static class RequirementCoherenceEngine
    {
        // Two requirements are flagged as near-duplicates at or above this Jaccard
        // similarity of their content tokens (same discipline only).
        private const double SemanticDuplicateThreshold = 0.82;

        private const int MaxFindings = 750;

        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "and", "for", "with", "shall", "must", "will", "all", "any", "per",
            "that", "this", "from", "into", "onto", "are", "was", "were", "been", "have",
            "has", "had", "not", "but", "each", "every", "provide", "install", "include"
        };

        private sealed class CoherenceItem
        {
            public RequirementRef Ref;
            public RequirementSemanticIr Ir;
            public HashSet<string> Tokens;
            public HashSet<string> AllowedBrands;
        }

        public static RequirementCoherenceReport Analyze(IReadOnlyList<RequirementCheckResult> results)
        {
            RequirementCoherenceReport report = new RequirementCoherenceReport();
            if (results == null || results.Count == 0)
            {
                report.CoherenceGrade = "Coherent";
                return report;
            }

            List<CoherenceItem> items = results
                .Where(r => r != null)
                .Select(BuildItem)
                .OrderBy(i => i.Ref.SourceWorksheet, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Ref.SourceRow)
                .ThenBy(i => i.Ref.RequirementId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            report.RequirementsAnalyzed = items.Count;

            HashSet<string> emittedIds = new HashSet<string>(StringComparer.Ordinal);
            List<CoherenceFinding> findings = new List<CoherenceFinding>();

            DetectExactDuplicates(items, findings, emittedIds);
            DetectPairwise(items, findings, emittedIds);

            // Stable ordering and cap.
            findings = findings
                .OrderByDescending(f => (int)f.Severity)
                .ThenBy(f => f.FindingType.ToString(), StringComparer.Ordinal)
                .ThenBy(f => f.Id, StringComparer.Ordinal)
                .Take(MaxFindings)
                .ToList();

            report.Findings = findings;
            FillTotals(report, findings);
            report.TypeSummaries = BuildTypeSummaries(items, findings);
            report.RequirementTypesAnalyzed = report.TypeSummaries.Count;
            report.CoherenceGrade = GradeOf(report);
            return report;
        }

        private static CoherenceItem BuildItem(RequirementCheckResult result)
        {
            string text = !string.IsNullOrWhiteSpace(result.RequirementText)
                ? result.RequirementText
                : (result.Requirement != null ? result.Requirement.RequirementText : string.Empty);

            string id = !string.IsNullOrWhiteSpace(result.RequirementId)
                ? result.RequirementId
                : (result.Requirement != null ? result.Requirement.RequirementId : null);

            string worksheet = !string.IsNullOrWhiteSpace(result.SourceWorksheet)
                ? result.SourceWorksheet
                : (result.Requirement != null ? result.Requirement.SourceSheet : null);

            int row = result.SourceRow > 0
                ? result.SourceRow
                : (result.Requirement != null ? result.Requirement.RowNumber : 0);

            string discipline = !string.IsNullOrWhiteSpace(result.Discipline)
                ? result.Discipline
                : (result.Requirement != null ? result.Requirement.Discipline : "Unknown");

            RequirementSemanticIr ir = RequirementSemanticParser.Parse(text);

            HashSet<string> allowed = new HashSet<string>(ir.ManufacturerBrands, StringComparer.Ordinal);
            allowed.ExceptWith(ir.ExcludedManufacturerBrands);

            return new CoherenceItem
            {
                Ref = new RequirementRef
                {
                    RequirementId = string.IsNullOrWhiteSpace(id) ? ("row-" + row.ToString(CultureInfo.InvariantCulture)) : id,
                    SourceWorksheet = worksheet,
                    SourceRow = row,
                    Discipline = discipline,
                    RequirementType = string.IsNullOrWhiteSpace(result.RequirementType) ? "unclassified" : result.RequirementType,
                    ShortText = Shorten(text, 160)
                },
                Ir = ir,
                Tokens = Tokenize(ir.NormalizedText),
                AllowedBrands = allowed
            };
        }

        private static void DetectExactDuplicates(List<CoherenceItem> items, List<CoherenceFinding> findings, HashSet<string> emitted)
        {
            var groups = items
                .Where(i => i.Tokens.Count > 0 && !string.IsNullOrEmpty(i.Ir.ContentHash))
                .GroupBy(i => i.Ir.ContentHash, StringComparer.Ordinal)
                .Where(g => g.Count() > 1);

            foreach (var group in groups)
            {
                List<CoherenceItem> members = group.ToList();
                CoherenceItem representative = members[0];
                for (int i = 1; i < members.Count; i++)
                {
                    AddFinding(findings, emitted, CoherenceFindingType.ExactDuplicate, CoherenceSeverity.Medium,
                        representative, members[i],
                        "Identical requirement text appears more than once. Consolidate to a single source of truth.",
                        null);
                }
            }
        }

        private static void DetectPairwise(List<CoherenceItem> items, List<CoherenceFinding> findings, HashSet<string> emitted)
        {
            // Bucket by discipline; conflicts and near-duplicates are only compared
            // within a discipline to keep precision high and avoid cross-scope noise.
            var buckets = items.GroupBy(i => RequirementDisciplineNormalizer.NormalizeText(i.Ref.Discipline));

            foreach (var bucket in buckets)
            {
                List<CoherenceItem> list = bucket.ToList();
                for (int a = 0; a < list.Count; a++)
                {
                    for (int b = a + 1; b < list.Count; b++)
                    {
                        if (findings.Count >= MaxFindings)
                        {
                            return;
                        }

                        CoherenceItem x = list[a];
                        CoherenceItem y = list[b];

                        bool sameText = string.Equals(x.Ir.ContentHash, y.Ir.ContentHash, StringComparison.Ordinal);

                        if (!sameText && Jaccard(x.Tokens, y.Tokens) >= SemanticDuplicateThreshold)
                        {
                            AddFinding(findings, emitted, CoherenceFindingType.SemanticDuplicate, CoherenceSeverity.Low,
                                x, y, "Near-identical wording for the same obligation. Confirm whether these are duplicates.",
                                null);
                        }

                        // Conflicts require a shared subject so different scopes never collide.
                        List<string> sharedSubjects = x.Ir.SubjectTokens.Intersect(y.Ir.SubjectTokens, StringComparer.Ordinal).ToList();
                        if (sharedSubjects.Count == 0)
                        {
                            continue;
                        }

                        DetectNumericConflict(x, y, sharedSubjects, findings, emitted);
                        DetectQuantityConflict(x, y, sharedSubjects, findings, emitted);
                        DetectManufacturerConflict(x, y, sharedSubjects, findings, emitted);
                    }
                }
            }
        }

        private static void DetectNumericConflict(CoherenceItem x, CoherenceItem y, List<string> subjects, List<CoherenceFinding> findings, HashSet<string> emitted)
        {
            var xq = x.Ir.Quantities.Where(q => q.Property != "count" && q.Operator == "=");
            var yq = y.Ir.Quantities.Where(q => q.Property != "count" && q.Operator == "=").ToList();

            foreach (SemanticQuantity qx in xq)
            {
                foreach (SemanticQuantity qy in yq)
                {
                    if (qx.Property == qy.Property &&
                        string.Equals(qx.Unit, qy.Unit, StringComparison.Ordinal) &&
                        Math.Abs(qx.Value - qy.Value) > 0.0001)
                    {
                        var values = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            { "subject", string.Join(",", subjects) },
                            { "property", qx.Property },
                            { "unit", qx.Unit },
                            { "value_a", qx.Value.ToString("R", CultureInfo.InvariantCulture) },
                            { "value_b", qy.Value.ToString("R", CultureInfo.InvariantCulture) }
                        };
                        AddFinding(findings, emitted, CoherenceFindingType.NumericConflict, CoherenceSeverity.High,
                            x, y, string.Format(CultureInfo.InvariantCulture,
                                "Conflicting {0} for {1}: {2}{3} vs {4}{3}.",
                                qx.Property, string.Join("/", subjects),
                                qx.Value.ToString("0.###", CultureInfo.InvariantCulture), qx.Unit,
                                qy.Value.ToString("0.###", CultureInfo.InvariantCulture)),
                            values);
                        return;
                    }
                }
            }
        }

        private static void DetectQuantityConflict(CoherenceItem x, CoherenceItem y, List<string> subjects, List<CoherenceFinding> findings, HashSet<string> emitted)
        {
            foreach (string op in new[] { "min", "max", "=" })
            {
                SemanticQuantity qx = x.Ir.Quantities.FirstOrDefault(q => q.Property == "count" && q.Operator == op);
                SemanticQuantity qy = y.Ir.Quantities.FirstOrDefault(q => q.Property == "count" && q.Operator == op);
                if (qx != null && qy != null && Math.Abs(qx.Value - qy.Value) > 0.0001)
                {
                    var values = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { "subject", string.Join(",", subjects) },
                        { "operator", op },
                        { "count_a", qx.Value.ToString("R", CultureInfo.InvariantCulture) },
                        { "count_b", qy.Value.ToString("R", CultureInfo.InvariantCulture) }
                    };
                    AddFinding(findings, emitted, CoherenceFindingType.QuantityConflict, CoherenceSeverity.Medium,
                        x, y, string.Format(CultureInfo.InvariantCulture,
                            "Conflicting required count for {0}: {1} {2} vs {1} {3}.",
                            string.Join("/", subjects), op,
                            qx.Value.ToString("0.###", CultureInfo.InvariantCulture),
                            qy.Value.ToString("0.###", CultureInfo.InvariantCulture)),
                        values);
                    return;
                }
            }
        }

        private static void DetectManufacturerConflict(CoherenceItem x, CoherenceItem y, List<string> subjects, List<CoherenceFinding> findings, HashSet<string> emitted)
        {
            // Case 1: a brand excluded by one requirement is required/allowed by the other.
            bool crossExclusion =
                x.Ir.ExcludedManufacturerBrands.Any(b => y.AllowedBrands.Contains(b)) ||
                y.Ir.ExcludedManufacturerBrands.Any(b => x.AllowedBrands.Contains(b));

            // Case 2: both restrict exclusively ("only") to disjoint brand sets.
            bool disjointExclusive =
                x.Ir.ManufacturerExclusive && y.Ir.ManufacturerExclusive &&
                x.AllowedBrands.Count > 0 && y.AllowedBrands.Count > 0 &&
                !x.AllowedBrands.Overlaps(y.AllowedBrands);

            if (!crossExclusion && !disjointExclusive)
            {
                return;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "subject", string.Join(",", subjects) },
                { "allowed_a", string.Join(",", x.AllowedBrands.OrderBy(s => s, StringComparer.Ordinal)) },
                { "excluded_a", string.Join(",", x.Ir.ExcludedManufacturerBrands) },
                { "allowed_b", string.Join(",", y.AllowedBrands.OrderBy(s => s, StringComparer.Ordinal)) },
                { "excluded_b", string.Join(",", y.Ir.ExcludedManufacturerBrands) }
            };
            AddFinding(findings, emitted, CoherenceFindingType.ManufacturerConflict, CoherenceSeverity.High,
                x, y, string.Format(CultureInfo.InvariantCulture,
                    "Conflicting manufacturer restriction for {0}. Requirements specify incompatible approved brands.",
                    string.Join("/", subjects)),
                values);
        }

        private static void AddFinding(
            List<CoherenceFinding> findings, HashSet<string> emitted,
            CoherenceFindingType type, CoherenceSeverity severity,
            CoherenceItem primary, CoherenceItem related, string rationale,
            Dictionary<string, string> values)
        {
            string id = BuildFindingId(type, primary, related);
            if (!emitted.Add(id))
            {
                return;
            }

            findings.Add(new CoherenceFinding
            {
                Id = id,
                FindingType = type,
                Severity = severity,
                RequirementType = primary.Ref.RequirementType,
                Primary = primary.Ref,
                Related = related != null ? related.Ref : null,
                Rationale = rationale,
                NormalizedValues = values ?? new Dictionary<string, string>(StringComparer.Ordinal)
            });
        }

        private static string BuildFindingId(CoherenceFindingType type, CoherenceItem primary, CoherenceItem related)
        {
            string keyA = RefKey(primary.Ref);
            if (related == null)
            {
                return type.ToString().ToLowerInvariant() + ":" + keyA;
            }
            string keyB = RefKey(related.Ref);
            string first = string.CompareOrdinal(keyA, keyB) <= 0 ? keyA : keyB;
            string second = string.CompareOrdinal(keyA, keyB) <= 0 ? keyB : keyA;
            return type.ToString().ToLowerInvariant() + ":" + first + "|" + second;
        }

        private static string RefKey(RequirementRef r)
        {
            string ws = (r.SourceWorksheet ?? string.Empty).ToLowerInvariant();
            return ws + "#" + r.SourceRow.ToString(CultureInfo.InvariantCulture) + "#" + (r.RequirementId ?? string.Empty).ToLowerInvariant();
        }

        private static void FillTotals(RequirementCoherenceReport report, List<CoherenceFinding> findings)
        {
            report.ExactDuplicateCount = findings.Count(f => f.FindingType == CoherenceFindingType.ExactDuplicate);
            report.SemanticDuplicateCount = findings.Count(f => f.FindingType == CoherenceFindingType.SemanticDuplicate);
            report.NumericConflictCount = findings.Count(f => f.FindingType == CoherenceFindingType.NumericConflict);
            report.QuantityConflictCount = findings.Count(f => f.FindingType == CoherenceFindingType.QuantityConflict);
            report.ManufacturerConflictCount = findings.Count(f => f.FindingType == CoherenceFindingType.ManufacturerConflict);
            report.HighSeverityCount = findings.Count(f => f.Severity >= CoherenceSeverity.High);
        }

        private static List<RequirementTypeCoherenceSummary> BuildTypeSummaries(List<CoherenceItem> items, List<CoherenceFinding> findings)
        {
            Dictionary<string, RequirementTypeCoherenceSummary> map = new Dictionary<string, RequirementTypeCoherenceSummary>(StringComparer.Ordinal);

            foreach (CoherenceItem item in items)
            {
                string type = item.Ref.RequirementType ?? "unclassified";
                if (!map.TryGetValue(type, out RequirementTypeCoherenceSummary summary))
                {
                    summary = new RequirementTypeCoherenceSummary { RequirementType = type };
                    map[type] = summary;
                }
                summary.RequirementCount++;
            }

            foreach (CoherenceFinding finding in findings)
            {
                string type = finding.RequirementType ?? "unclassified";
                if (!map.TryGetValue(type, out RequirementTypeCoherenceSummary summary))
                {
                    summary = new RequirementTypeCoherenceSummary { RequirementType = type };
                    map[type] = summary;
                }
                summary.FindingCount++;
                if (finding.FindingType == CoherenceFindingType.ExactDuplicate || finding.FindingType == CoherenceFindingType.SemanticDuplicate)
                {
                    summary.DuplicateCount++;
                }
                else
                {
                    summary.ConflictCount++;
                }
                if (finding.Severity > summary.HighestSeverity)
                {
                    summary.HighestSeverity = finding.Severity;
                }
            }

            return map.Values
                .OrderByDescending(s => s.FindingCount)
                .ThenByDescending(s => s.RequirementCount)
                .ThenBy(s => s.RequirementType, StringComparer.Ordinal)
                .ToList();
        }

        private static string GradeOf(RequirementCoherenceReport report)
        {
            if (report.Findings.Count == 0)
            {
                return "Coherent";
            }
            int conflicts = report.NumericConflictCount + report.QuantityConflictCount + report.ManufacturerConflictCount;
            if (conflicts > 0 || report.HighSeverityCount > 0)
            {
                return "Conflicts Found";
            }
            return "Minor Issues";
        }

        private static HashSet<string> Tokenize(string normalized)
        {
            HashSet<string> tokens = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return tokens;
            }
            foreach (string token in normalized.Split(' '))
            {
                if (token.Length >= 3 && !StopWords.Contains(token))
                {
                    tokens.Add(token);
                }
            }
            return tokens;
        }

        private static double Jaccard(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0)
            {
                return 0;
            }
            int intersection = a.Count <= b.Count ? a.Count(b.Contains) : b.Count(a.Contains);
            int union = a.Count + b.Count - intersection;
            return union == 0 ? 0 : (double)intersection / union;
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max - 1).TrimEnd() + "…";
        }
    }
}
