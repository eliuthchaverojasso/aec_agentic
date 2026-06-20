using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EMAExtractor.Requirements.Audit
{
    /// <summary>Versioned policy identity stamped into every evaluation bundle.</summary>
    public static class EvaluationVersions
    {
        public const string SchemaVersion = "1.0";
        public const string EngineVersion = "ema-audit-engine-1.0.0";
        public const string RulesetVersion = "ema-rules-2026.06";
        public const string TaxonomyVersion = "owner-requirements-1.0";
        public const string ScorePolicyVersion = "confidence-1.0";
    }

    /// <summary>
    /// Assembles and persists the Evaluation Bundle v1 — the closed, reproducible
    /// record of one evaluation run. The bundle (manifest + requirement audits +
    /// coherence findings) is the canonical artifact; the HTML/PDF report and the
    /// dashboard are downstream views of it. Hashes are computed from canonical,
    /// order-stable digests (not from JSON), so identical inputs + versions +
    /// as_of always yield identical hashes and run id.
    /// </summary>
    public static class EvaluationBundleWriter
    {
        // BOM-free UTF-8 so the JSON artifacts are clean machine-readable contracts
        // (a BOM breaks naive json.loads on the Python ingest side).
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>Run coherence + audit + bundle assembly in one call (no I/O).</summary>
        public static EvaluationBundle Create(
            RequirementCheckReport report,
            IReadOnlyList<RequirementCheckResult> results,
            DateTime asOfUtc)
        {
            List<RequirementCheckResult> safeResults = (results ?? new List<RequirementCheckResult>())
                .Where(r => r != null)
                .ToList();

            RequirementCoherenceReport coherence = RequirementCoherenceEngine.Analyze(safeResults);
            List<RequirementAuditRecord> records = RequirementAuditRecordBuilder.BuildAll(safeResults, coherence);
            return BuildBundle(report, safeResults, coherence, records, asOfUtc);
        }

        public static EvaluationBundle BuildBundle(
            RequirementCheckReport report,
            IReadOnlyList<RequirementCheckResult> results,
            RequirementCoherenceReport coherence,
            List<RequirementAuditRecord> auditRecords,
            DateTime asOfUtc)
        {
            List<RequirementCheckResult> safeResults = (results ?? new List<RequirementCheckResult>())
                .Where(r => r != null)
                .ToList();
            coherence = coherence ?? new RequirementCoherenceReport();
            auditRecords = auditRecords ?? new List<RequirementAuditRecord>();

            Dictionary<string, int> statusCounts = BuildStatusCounts(safeResults);
            string inputHash = ComputeInputHash(report, auditRecords);
            string outputHash = ComputeOutputHash(auditRecords, coherence, statusCounts);
            string asOf = asOfUtc.ToString("o", CultureInfo.InvariantCulture);
            string runId = RequirementSemanticParser.Sha256Hex(inputHash + "|" + outputHash + "|" + asOf).Substring(0, 32);

            EvaluationManifest manifest = new EvaluationManifest
            {
                SchemaVersion = EvaluationVersions.SchemaVersion,
                EvaluationRunId = runId,
                ProjectName = report != null ? report.ProjectName : null,
                ModelName = report != null ? report.ModelName : null,
                RequirementsFile = report != null ? report.RequirementsFileName : null,
                EngineVersion = EvaluationVersions.EngineVersion,
                RulesetVersion = EvaluationVersions.RulesetVersion,
                TaxonomyVersion = EvaluationVersions.TaxonomyVersion,
                ScorePolicyVersion = EvaluationVersions.ScorePolicyVersion,
                AsOfUtc = asOf,
                GeneratedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                InputHash = inputHash,
                OutputHash = outputHash,
                RequirementsTotal = auditRecords.Count,
                StatusCounts = statusCounts,
                CoherenceFindingsTotal = coherence.Findings != null ? coherence.Findings.Count : 0
            };

            return new EvaluationBundle
            {
                Manifest = manifest,
                AuditRecords = auditRecords,
                Coherence = coherence
            };
        }

        /// <summary>Convenience: build + persist a bundle, returning the bundle folder path.</summary>
        public static string CreateAndWrite(
            RequirementCheckReport report,
            IReadOnlyList<RequirementCheckResult> results,
            DateTime asOfUtc,
            string outputFolder)
        {
            EvaluationBundle bundle = Create(report, results, asOfUtc);
            return Write(bundle, outputFolder);
        }

        public static string Write(EvaluationBundle bundle, string outputFolder)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("An output folder is required.", nameof(outputFolder));
            }

            string bundleFolder = Path.Combine(outputFolder, "evaluation_" + bundle.Manifest.EvaluationRunId);
            Directory.CreateDirectory(bundleFolder);

            // String enum converter keeps the bundle a clean, portable contract
            // (e.g. "Compliant", "ManufacturerConflict") for the Python ingest layer.
            // Hashes are computed from canonical strings, not this JSON, so
            // serialization choices never affect reproducibility.
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };

            File.WriteAllText(Path.Combine(bundleFolder, "evaluation_manifest.json"),
                JsonSerializer.Serialize(bundle.Manifest, options), Utf8NoBom);
            File.WriteAllText(Path.Combine(bundleFolder, "requirement_audits.json"),
                JsonSerializer.Serialize(bundle.AuditRecords, options), Utf8NoBom);
            File.WriteAllText(Path.Combine(bundleFolder, "coherence_findings.json"),
                JsonSerializer.Serialize(bundle.Coherence, options), Utf8NoBom);

            object summary = new
            {
                run_id = bundle.Manifest.EvaluationRunId,
                schema_version = bundle.Manifest.SchemaVersion,
                input_hash = bundle.Manifest.InputHash,
                output_hash = bundle.Manifest.OutputHash,
                as_of_utc = bundle.Manifest.AsOfUtc,
                requirements_total = bundle.Manifest.RequirementsTotal,
                status_counts = bundle.Manifest.StatusCounts,
                coherence = new
                {
                    grade = bundle.Coherence.CoherenceGrade,
                    findings_total = bundle.Coherence.Findings != null ? bundle.Coherence.Findings.Count : 0,
                    exact_duplicates = bundle.Coherence.ExactDuplicateCount,
                    semantic_duplicates = bundle.Coherence.SemanticDuplicateCount,
                    numeric_conflicts = bundle.Coherence.NumericConflictCount,
                    quantity_conflicts = bundle.Coherence.QuantityConflictCount,
                    manufacturer_conflicts = bundle.Coherence.ManufacturerConflictCount
                }
            };
            File.WriteAllText(Path.Combine(bundleFolder, "evaluation_summary.json"),
                JsonSerializer.Serialize(summary, options), Utf8NoBom);

            return bundleFolder;
        }

        private static Dictionary<string, int> BuildStatusCounts(IReadOnlyList<RequirementCheckResult> results)
        {
            // Ordinal-keyed and built in fixed key order so iteration is deterministic.
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "met", 0 },
                { "not_met", 0 },
                { "needs_human_review", 0 },
                { "insufficient_model_data", 0 },
                { "not_applicable", 0 }
            };

            foreach (RequirementCheckResult result in results)
            {
                switch (result.Status)
                {
                    case RequirementCheckStatus.Met: counts["met"]++; break;
                    case RequirementCheckStatus.NotMet: counts["not_met"]++; break;
                    case RequirementCheckStatus.NeedsHumanReview: counts["needs_human_review"]++; break;
                    case RequirementCheckStatus.InsufficientModelData: counts["insufficient_model_data"]++; break;
                    case RequirementCheckStatus.NotApplicable: counts["not_applicable"]++; break;
                }
            }
            return counts;
        }

        private static string ComputeInputHash(RequirementCheckReport report, List<RequirementAuditRecord> records)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("project=").Append(report != null ? (report.ProjectName ?? string.Empty) : string.Empty).Append('\n');
            sb.Append("model=").Append(report != null ? (report.ModelName ?? string.Empty) : string.Empty).Append('\n');
            sb.Append("requirements_file=").Append(report != null ? (report.RequirementsFileName ?? string.Empty) : string.Empty).Append('\n');

            foreach (RequirementAuditRecord record in OrderRecords(records))
            {
                sb.Append(record.RequirementId ?? string.Empty)
                  .Append('|')
                  .Append(record.Source != null ? (record.Source.RequirementContentHash ?? string.Empty) : string.Empty)
                  .Append('\n');
            }

            return RequirementSemanticParser.Sha256Hex(sb.ToString());
        }

        private static string ComputeOutputHash(
            List<RequirementAuditRecord> records,
            RequirementCoherenceReport coherence,
            Dictionary<string, int> statusCounts)
        {
            StringBuilder sb = new StringBuilder();

            foreach (RequirementAuditRecord record in OrderRecords(records))
            {
                sb.Append(record.RecordHash ?? string.Empty).Append('\n');
            }

            if (coherence.Findings != null)
            {
                foreach (CoherenceFinding finding in coherence.Findings.OrderBy(f => f.Id, StringComparer.Ordinal))
                {
                    sb.Append(finding.Id)
                      .Append('|').Append(finding.FindingType.ToString())
                      .Append('|').Append(finding.Severity.ToString())
                      .Append('\n');
                }
            }

            foreach (KeyValuePair<string, int> pair in statusCounts.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                sb.Append(pair.Key).Append('=').Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            return RequirementSemanticParser.Sha256Hex(sb.ToString());
        }

        private static IEnumerable<RequirementAuditRecord> OrderRecords(List<RequirementAuditRecord> records)
        {
            return records
                .OrderBy(r => r.Source != null ? r.Source.SourceWorksheet : string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Source != null ? r.Source.SourceRow : 0)
                .ThenBy(r => r.RequirementId, StringComparer.OrdinalIgnoreCase);
        }
    }
}
