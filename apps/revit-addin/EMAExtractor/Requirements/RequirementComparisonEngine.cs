using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EMAExtractor.Models;

namespace EMAExtractor.Requirements
{
    /// <summary>
    /// Pre-built evidence index for O(1) category lookups and pre-computed search blobs.
    /// Built once on the main thread from immutable DTOs, then shared read-only across parallel workers.
    /// </summary>
    public class EvidenceIndex
    {
        public IReadOnlyList<ExportElementRecord> AllRecords { get; }
        public IReadOnlyDictionary<string, List<ExportElementRecord>> ByCategoryNormalized { get; }
        public IReadOnlyDictionary<ExportElementRecord, string> SearchBlobs { get; }
        public int ElementCount => AllRecords.Count;

        public EvidenceIndex(IEnumerable<ExportElementRecord> records)
        {
            List<ExportElementRecord> list = records == null
                ? new List<ExportElementRecord>()
                : records.ToList();

            AllRecords = list;

            // Pre-build normalized search blobs per record
            Dictionary<ExportElementRecord, string> blobs = new Dictionary<ExportElementRecord, string>(list.Count);
            Dictionary<string, List<ExportElementRecord>> byCategory = new Dictionary<string, List<ExportElementRecord>>(StringComparer.OrdinalIgnoreCase);

            foreach (ExportElementRecord record in list)
            {
                if (record == null) continue;

                // Pre-compute search blob
                blobs[record] = BuildSearchBlob(record);

                // Index by normalized category
                string cat = RequirementDisciplineNormalizer.NormalizeText(record.Category ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(cat))
                {
                    List<ExportElementRecord> bucket;
                    if (!byCategory.TryGetValue(cat, out bucket))
                    {
                        bucket = new List<ExportElementRecord>();
                        byCategory[cat] = bucket;
                    }
                    bucket.Add(record);
                }
            }

            SearchBlobs = blobs;
            ByCategoryNormalized = byCategory;
        }

        private static string BuildSearchBlob(ExportElementRecord record)
        {
            StringBuilder blob = new StringBuilder(256);

            Append(blob, record.Category);
            Append(blob, record.Name);
            Append(blob, record.Family);
            Append(blob, record.Type);
            Append(blob, record.Level);

            AppendParameters(blob, record.InstanceParameters);
            AppendParameters(blob, record.TypeParameters);

            return RequirementDisciplineNormalizer.NormalizeText(blob.ToString());
        }

        private static void AppendParameters(StringBuilder builder, IReadOnlyDictionary<string, ParameterRecord> parameters)
        {
            if (parameters == null) return;

            foreach (KeyValuePair<string, ParameterRecord> pair in parameters)
            {
                Append(builder, pair.Key);
                ParameterRecord parameter = pair.Value;
                if (parameter != null)
                {
                    Append(builder, parameter.ValueString);
                    Append(builder, parameter.RawValue);
                }
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(value);
        }
    }

    /// <summary>
    /// Timing diagnostics for the compliance check run.
    /// </summary>
    public class ComplianceRunDiagnostics
    {
        public long RequirementsParseMs { get; set; }
        public long ModelSnapshotMs { get; set; }
        public long EvidenceIndexMs { get; set; }
        public long MatchingMs { get; set; }
        public long ScoringMs { get; set; }
        public long KeyIssueRankingMs { get; set; }
        public long ReportGenerationMs { get; set; }
        public long TotalRunMs { get; set; }
        public int RequirementCount { get; set; }
        public int ElementCount { get; set; }
        public int MaxDegreeOfParallelism { get; set; }
        public List<string> CoherenceWarnings { get; set; } = new List<string>();

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Diagnostics: {0} requirements, {1} elements, {2} parallel threads\n" +
                "  requirements_parse_ms={3}\n  model_snapshot_ms={4}\n  evidence_index_ms={5}\n" +
                "  matching_ms={6}\n  scoring_ms={7}\n  key_issue_ranking_ms={8}\n" +
                "  report_generation_ms={9}\n  total_run_ms={10}\n  coherence_warnings={11}",
                RequirementCount, ElementCount, MaxDegreeOfParallelism,
                RequirementsParseMs, ModelSnapshotMs, EvidenceIndexMs,
                MatchingMs, ScoringMs, KeyIssueRankingMs,
                ReportGenerationMs, TotalRunMs,
                CoherenceWarnings.Count > 0 ? string.Join("; ", CoherenceWarnings) : "none");
        }
    }

    /// <summary>
    /// Coherence checker that validates engine output for common failure modes.
    /// </summary>
    public static class CoherenceChecker
    {
        public static List<string> Check(List<RequirementCheckResult> results, int elementCount)
        {
            var warnings = new List<string>();

            if (results == null || results.Count == 0)
            {
                warnings.Add("No requirement results were produced.");
                return warnings;
            }

            if (elementCount == 0)
            {
                warnings.Add("Evidence element count is zero. All model-checkable requirements will be marked as Insufficient Model Data.");
            }

            int total = results.Count;
            int naCount = results.Count(r => r.Status == RequirementCheckStatus.NotApplicable);
            int reviewCount = results.Count(r => r.Status == RequirementCheckStatus.NeedsHumanReview);
            int insufficientCount = results.Count(r => r.Status == RequirementCheckStatus.InsufficientModelData);

            if (naCount == total)
                warnings.Add("All requirements are Not Applicable. Verify the selected discipline matches the workbook.");
            if (reviewCount == total)
                warnings.Add("All requirements are Needs Human Review. The engine may lack rules for this workbook.");
            if (insufficientCount == total)
                warnings.Add("All requirements are Insufficient Model Data. The model snapshot may be empty or incomplete.");

            // Check confidence uniformity
            var confidences = results.Where(r => r.Status != RequirementCheckStatus.NotApplicable)
                .Select(r => r.Confidence).Distinct().ToList();
            if (confidences.Count == 1 && results.Count(r => r.Status != RequirementCheckStatus.NotApplicable) > 10)
                warnings.Add("All confidence values are identical (" + confidences[0].ToString("0.00", CultureInfo.InvariantCulture) + "). Scoring may not be differentiating.");

            // Check non-Met items have next actions and reasoning
            foreach (var r in results.Where(r => r.Status != RequirementCheckStatus.Met && r.Status != RequirementCheckStatus.NotApplicable))
            {
                if (string.IsNullOrWhiteSpace(r.NextBestAction))
                    warnings.Add("Result for row " + r.SourceRow + " missing Next Best Action.");
                if (string.IsNullOrWhiteSpace(r.Reasoning))
                    warnings.Add("Result for row " + r.SourceRow + " missing Reasoning.");
            }

            // Cap warnings
            if (warnings.Count > 20)
            {
                int removed = warnings.Count - 20;
                warnings = warnings.Take(20).ToList();
                warnings.Add("... and " + removed + " more warnings.");
            }

            return warnings;
        }
    }

    public class RuleContext
    {
        public string RuleName { get; set; }
        public string RuleFamily { get; set; }
        public string RequirementType { get; set; }
        public string RequirementTypeReason { get; set; }
        public ValidationType? ValidationType { get; set; }
        public string ValidationTypeReason { get; set; }
        public List<string> TriggerKeywords { get; set; } = new List<string>();
        public List<string> ExpectedEvidenceSources { get; set; } = new List<string>();
        public List<string> ExpectedFamilyTypeHints { get; set; } = new List<string>();
        public List<string> ExpectedParameters { get; set; } = new List<string>();
        public string ExpectedEvidence { get; set; }
        public string[] ExpectedCategories { get; set; }
        public List<string> AllowedCategories { get; set; } = new List<string>();
        public List<string> ExcludedCategories { get; set; } = new List<string>();
        public List<string> DirectClosingEvidence { get; set; } = new List<string>();
        public List<string> SupportingContext { get; set; } = new List<string>();
        public List<string> MissingDirectEvidence { get; set; } = new List<string>();
        public string CandidateScopeReason { get; set; }
        public bool FallbackAllowed { get; set; } = true;
        public bool FallbackUsed { get; set; }
        public string ModelEvidenceSufficiency { get; set; }
        public string WhyNotModelCloseable { get; set; }
        public bool RequiresDirectParameterEvidence { get; set; }
        public bool AllowsModelOnlyMet { get; set; }
        public bool WeakEvidenceIfOnlyCategoryLevel { get; set; } = true;
        public List<string> ManualReviewIndicators { get; set; } = new List<string>();
    }

    public class RequirementComparisonEngine
    {
        private static readonly string[] SpecificEvidenceKeywords = new[]
        {
            "identification", "manufacturer", "nameplate", "label", "tag", "mark",
            "specification", "brand", "model number", "part number", "catalog",
            "basis of design", "approved equal", "listed", "certified"
        };

        private static readonly HashSet<string> HighPrioritySemanticTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "grounding_bonding_conductors",
            "conduit_raceway_size_requirement",
            "flexible_conduit_length_requirement",
            "conduit_raceway_presence",
            "dimension_clearance_distance_separation",
            "manufacturer_brand_restriction",
            "owner_standard_product_constraint",
            "plumbing_hose_bibb_rpz_valves",
            "plumbing_flush_valve_product_spec",
            "plumbing_water_hammer_arrestor_requirement",
            "plumbing_accessory_water_supply",
            "plumbing_support_hanger_requirement",
            "manufacturer_product_spec_submittal",
            "identification_labeling_nameplate",
            "drawing_spec_manual_owner_approval",
            "field_execution_demolition_protection",
            "conduit_raceway",
            "commissioning_testing_om_training",
            "controls_bms_bas_contactors_relays",
            "mechanical_controls_ddc_emcs",
            "mechanical_performance_feature",
            "installation_method_constraint",
            "code_jurisdiction_requirement",
            "lighting_control_scheme",
            "operation_maintenance_manual",
            "attic_stock_spare_parts"
        };

        /// <summary>
        /// Original sequential evaluation method (preserved for backward compatibility).
        /// </summary>
        public List<RequirementCheckResult> Evaluate(
            IEnumerable<OwnerRequirementRow> requirements,
            IEnumerable<ExportElementRecord> modelRecords,
            RequirementDiscipline selectedDiscipline)
        {
            List<ExportElementRecord> records = modelRecords == null
                ? new List<ExportElementRecord>()
                : modelRecords.ToList();

            List<RequirementCheckResult> results = new List<RequirementCheckResult>();

            if (requirements == null)
            {
                return results;
            }

            foreach (OwnerRequirementRow requirement in requirements)
            {
                results.Add(EvaluateRequirement(requirement, records, selectedDiscipline));
            }

            return results;
        }

        /// <summary>
        /// Parallel evaluation using pre-built evidence index.
        /// Results are deterministically ordered by SourceWorksheet, SourceRow, RequirementId.
        /// </summary>
        public List<RequirementCheckResult> EvaluateParallel(
            IReadOnlyList<OwnerRequirementRow> requirements,
            EvidenceIndex evidenceIndex,
            RequirementDiscipline selectedDiscipline,
            int maxDegreeOfParallelism = -1,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return new List<RequirementCheckResult>();
            }

            if (maxDegreeOfParallelism <= 0)
            {
                maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);
            }

            IReadOnlyList<ExportElementRecord> records = evidenceIndex.AllRecords;
            ConcurrentBag<Tuple<int, RequirementCheckResult>> bag = new ConcurrentBag<Tuple<int, RequirementCheckResult>>();
            int processed = 0;

            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            Parallel.For(0, requirements.Count, options, i =>
            {
                RequirementCheckResult result = EvaluateRequirementIndexed(
                    requirements[i], records, selectedDiscipline, evidenceIndex);
                bag.Add(Tuple.Create(i, result));

                int count = Interlocked.Increment(ref processed);
                if (progress != null && (count % 50 == 0 || count == requirements.Count))
                {
                    progress.Report(count);
                }
            });

            // Deterministic ordering by original index
            return bag.OrderBy(t => t.Item1).Select(t => t.Item2).ToList();
        }

        /// <summary>
        /// Evaluates a single requirement using the pre-built evidence index for faster blob lookups.
        /// Uses pre-computed search blobs instead of rebuilding per-record per-requirement.
        /// </summary>
        private RequirementCheckResult EvaluateRequirementIndexed(
            OwnerRequirementRow requirement,
            IReadOnlyList<ExportElementRecord> modelRecords,
            RequirementDiscipline selectedDiscipline,
            EvidenceIndex evidenceIndex)
        {
            return EvaluateRequirementCore(requirement, modelRecords, selectedDiscipline, evidenceIndex);
        }

        private RequirementCheckResult EvaluateRequirement(
            OwnerRequirementRow requirement,
            IReadOnlyCollection<ExportElementRecord> modelRecords,
            RequirementDiscipline selectedDiscipline)
        {
            return EvaluateRequirementCore(requirement, modelRecords, selectedDiscipline, null);
        }

        /// <summary>
        /// Core evaluation method. When evidenceIndex is provided, uses pre-built search blobs
        /// for O(1) blob lookup instead of O(P) StringBuilder + normalize per record per requirement.
        /// </summary>
        private RequirementCheckResult EvaluateRequirementCore(
            OwnerRequirementRow requirement,
            IReadOnlyCollection<ExportElementRecord> modelRecords,
            RequirementDiscipline selectedDiscipline,
            EvidenceIndex evidenceIndex)
        {
            string requirementText = Normalize(requirement != null ? requirement.RequirementText : null);
            string requirementDiscipline = Normalize(requirement != null ? requirement.Discipline : null);
            requirementDiscipline = string.IsNullOrWhiteSpace(requirementDiscipline)
                ? Normalize(requirement != null ? requirement.SourceSheet : null)
                : requirementDiscipline;

            if (requirement == null)
            {
                return BuildResult(
                    null,
                    RequirementCheckStatus.NeedsHumanReview,
                    0.0,
                    "Unreadable requirement row",
                    "Requirement row could not be read.",
                    "Review the workbook row.",
                    "No row data was available.",
                    string.Empty,
                    "Unknown / Needs Classification",
                    string.Empty,
                    0,
                    null);
            }

            if (string.IsNullOrWhiteSpace(requirementText))
            {
                return BuildResult(
                    requirement,
                    RequirementCheckStatus.NeedsHumanReview,
                    0.25,
                    BuildIssueTitle(requirement, RequirementCheckStatus.NeedsHumanReview, string.Empty, selectedDiscipline),
                    "Requirement text is blank or missing.",
                    "Fill in the requirement text.",
                    "No requirement text was available for comparison.",
                    "No evidence could be evaluated because the requirement text was blank.",
                    BuildResponsibleRole(requirement, selectedDiscipline, string.Empty),
                    requirement.SourceSheet,
                    requirement.RowNumber,
                    null);
            }

            if (!MatchesDiscipline(requirementDiscipline, selectedDiscipline))
            {
                string role = BuildResponsibleRole(requirement, selectedDiscipline, requirementText);
                return BuildResult(
                    requirement,
                    RequirementCheckStatus.NotApplicable,
                    0.1,
                    BuildIssueTitle(requirement, RequirementCheckStatus.NotApplicable, requirementText, selectedDiscipline),
                    BuildReasoningForNotApplicable(requirementDiscipline, selectedDiscipline, requirementText),
                    BuildNextBestAction(RequirementCheckStatus.NotApplicable, requirementText, selectedDiscipline, role, requirementDiscipline),
                    "Selected discipline: " + selectedDiscipline + ". Row discipline: " + SafeText(requirementDiscipline) + ".",
                    BuildEvidenceSummary(new[] { "The requirement was excluded from the focused check." }),
                    role,
                    requirement.SourceSheet,
                    requirement.RowNumber,
                    null);
            }

            string text = requirementText;
            RequirementSemanticProfile semanticProfile = RequirementSemanticClassifier.Classify(
                requirementText,
                requirement != null ? requirement.Category : null,
                selectedDiscipline);

            if (TryEvaluateHighPrioritySemanticRequirement(
                requirement,
                modelRecords,
                selectedDiscipline,
                requirementText,
                evidenceIndex,
                semanticProfile,
                out RequirementCheckResult semanticResult))
            {
                return semanticResult;
            }

            if (ContainsAny(text, new[] { "outlet", "receptacle", "duplex", "general purpose circuit", "gpc", "120v", "120 v" }))
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "outlet_circuit_assignment",
                    RuleFamily = "electrical_connection",
                    ValidationType = ValidationType.Hybrid,
                    ValidationTypeReason = "The outlet and circuit assignment can be checked from Revit parameters, but the connected circuit context may require additional project evidence.",
                    TriggerKeywords = FindMatchingKeywords(text, new[] { "outlet", "receptacle", "duplex", "general purpose circuit", "gpc", "120v", "120 v" }),
                    ExpectedEvidenceSources = new List<string> { "Revit electrical fixture/device parameters", "Panel schedules or connected circuit metadata" },
                    ExpectedFamilyTypeHints = new List<string> { "duplex", "receptacle", "outlet" },
                    ExpectedParameters = new List<string> { "Voltage", "Panel", "Panel Name", "Circuit Number", "Circuit", "Load Name", "Room", "Space", "Level" },
                    ExpectedEvidence = "Electrical fixture/device evidence with voltage, panel, circuit, and room/space context.",
                    ExpectedCategories = new[] { "Electrical Fixtures", "Electrical Equipment" },
                    RequiresDirectParameterEvidence = true,
                    AllowsModelOnlyMet = false,
                    WeakEvidenceIfOnlyCategoryLevel = true,
                    ManualReviewIndicators = new List<string> { "room general purpose circuit", "panel schedule", "connected circuit context" }
                };
                return EvaluateParameterDrivenRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Voltage", "Panel", "Panel Name", "Circuit Number", "Circuit", "Load Name", "Room", "Space", "Level" },
                    "Outlet and circuit assignment",
                    "The requirement references an electrical outlet, its voltage, and connected circuit context.",
                    "Verify the outlet's voltage, panel, circuit assignment, and room/space context in Revit or panel schedules.",
                    requirementText,
                    false,
                    new[] { "Electrical Fixtures", "Electrical Equipment" },
                    false,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsAny(text, new[] { "panel", "circuit", "circuited", "supply from", "breaker" }))
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "panel_circuit_assignment",
                    RuleFamily = "electrical_connection",
                    ValidationType = ValidationType.Model,
                    ValidationTypeReason = "Panel and circuit assignment is directly model-checkable when the electrical parameters are exported.",
                    TriggerKeywords = FindMatchingKeywords(text, new[] { "panel", "circuit", "circuited", "supply from", "breaker" }),
                    ExpectedEvidenceSources = new List<string> { "Revit electrical fixture/device parameters", "Panel metadata" },
                    ExpectedFamilyTypeHints = new List<string> { "panel", "receptacle", "fixture", "equipment" },
                    ExpectedParameters = new List<string> { "Panel", "Panel Name", "Circuit Number", "Circuit", "Supply From" },
                    ExpectedEvidence = "Panel, Circuit Number, or Supply From parameters on electrical elements",
                    ExpectedCategories = new[] { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures" },
                    RequiresDirectParameterEvidence = true,
                    AllowsModelOnlyMet = true,
                    WeakEvidenceIfOnlyCategoryLevel = true
                };
                return EvaluateParameterDrivenRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Panel", "Circuit Number", "Supply From" },
                    "Panel and circuit assignment",
                    "The model needs electrical connection information to satisfy this requirement.",
                    "Assign the missing panel or circuit parameters in Revit.",
                    requirementText,
                    false,
                    new[] { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures" },
                    false,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsAny(text, new[] { "voltage", "apparent load", "connected load", "load" }))
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "electrical_load_metadata",
                    RuleFamily = "electrical_performance",
                    ValidationType = ValidationType.Model,
                    ValidationTypeReason = "Voltage and load metadata are exported as model parameters when available.",
                    TriggerKeywords = FindMatchingKeywords(text, new[] { "voltage", "apparent load", "connected load", "load" }),
                    ExpectedEvidenceSources = new List<string> { "Revit electrical type/instance parameters" },
                    ExpectedFamilyTypeHints = new List<string> { "voltage", "load", "panel", "receptacle" },
                    ExpectedParameters = new List<string> { "Voltage", "Apparent Load", "Connected Load", "Load Name" },
                    ExpectedEvidence = "Voltage, Apparent Load, or Connected Load parameters on electrical elements",
                    ExpectedCategories = new[] { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures" },
                    RequiresDirectParameterEvidence = true,
                    AllowsModelOnlyMet = true,
                    WeakEvidenceIfOnlyCategoryLevel = true
                };
                return EvaluateParameterDrivenRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Voltage", "Apparent Load", "Connected Load" },
                    "Electrical load metadata",
                    "The model needs electrical load metadata to satisfy this requirement.",
                    "Populate the electrical type parameters in Revit.",
                    requirementText,
                    false,
                    new[] { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures" },
                    false,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsAny(text, new[] { "lighting", "light fixture", "fixture", "illumination" }) || selectedDiscipline == RequirementDiscipline.Lighting)
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "lighting_fixture_coverage",
                    RuleFamily = "lighting",
                    ValidationType = ValidationType.Model,
                    ValidationTypeReason = "Lighting fixture presence and level assignment are typically checked from model elements.",
                    TriggerKeywords = FindMatchingKeywords(text, new[] { "lighting", "light fixture", "fixture", "illumination" }),
                    ExpectedEvidenceSources = new List<string> { "Revit lighting fixture elements" },
                    ExpectedFamilyTypeHints = new List<string> { "lighting fixture", "fixture" },
                    ExpectedParameters = new List<string> { "Level" },
                    ExpectedEvidence = "Lighting Fixtures category with Level assignment",
                    ExpectedCategories = new[] { "Lighting Fixtures" },
                    RequiresDirectParameterEvidence = false,
                    AllowsModelOnlyMet = true,
                    WeakEvidenceIfOnlyCategoryLevel = false
                };
                return EvaluateCategoryAndLevelRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Lighting Fixtures" },
                    "Lighting fixture coverage",
                    "Lighting fixture coverage was checked against the extracted model records.",
                    "Verify the lighting fixture category, level, and connection parameters.",
                    requirementText,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsAny(text, new[] { "level", "elevation" }))
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "level_assignment",
                    RuleFamily = "placement",
                    ValidationType = ValidationType.Model,
                    ValidationTypeReason = "Level and elevation requirements are directly model-checkable when the model exports the Level parameter.",
                    TriggerKeywords = FindMatchingKeywords(text, new[] { "level", "elevation" }),
                    ExpectedEvidenceSources = new List<string> { "Revit element level assignment" },
                    ExpectedFamilyTypeHints = new List<string> { "level", "elevation" },
                    ExpectedParameters = new List<string> { "Level" },
                    ExpectedEvidence = "Level parameter assigned to elements",
                    ExpectedCategories = semanticProfile != null && semanticProfile.AllowedCategories != null && semanticProfile.AllowedCategories.Count > 0
                        ? semanticProfile.AllowedCategories.ToArray()
                        : null,
                    RequiresDirectParameterEvidence = true,
                    AllowsModelOnlyMet = true,
                    WeakEvidenceIfOnlyCategoryLevel = false
                };
                return EvaluateParameterDrivenRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Level" },
                    "Level assignment",
                    "The requirement asks for level assignment or level-based placement.",
                    "Assign the elements to the correct level in Revit.",
                    requirementText,
                    true,
                    semanticProfile != null && semanticProfile.AllowedCategories != null && semanticProfile.AllowedCategories.Count > 0
                        ? semanticProfile.AllowedCategories
                        : null,
                    false,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsMechanicalSignals(text))
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "mechanical_equipment_placement",
                    RuleFamily = "mechanical",
                    ValidationType = ValidationType.Model,
                    ValidationTypeReason = "Mechanical equipment placement is primarily checked from model categories and levels.",
                    TriggerKeywords = FindMatchingKeywords(text, new[] { "airflow", "duct", "ductwork", "hvac", "mechanical equipment", "pump", "chiller", "coil", "fan" }),
                    ExpectedEvidenceSources = new List<string> { "Revit mechanical equipment elements" },
                    ExpectedFamilyTypeHints = new List<string> { "mechanical equipment", "pump", "fan", "chiller" },
                    ExpectedParameters = new List<string> { "Level" },
                    ExpectedEvidence = "Mechanical Equipment category with Level assignment",
                    ExpectedCategories = new[] { "Mechanical Equipment" },
                    RequiresDirectParameterEvidence = false,
                    AllowsModelOnlyMet = true,
                    WeakEvidenceIfOnlyCategoryLevel = true
                };
                return EvaluateCategoryAndLevelRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Mechanical Equipment" },
                    "Mechanical equipment placement",
                    "Mechanical equipment was present in the captured model snapshot.",
                    "Verify the mechanical equipment category and required parameters.",
                    requirementText,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsAny(text, new[] { "pipe", "plumbing", "drain", "vent", "water", "fixture", "sanitary", "waste", "slope" }))
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "plumbing_routing_coverage",
                    RuleFamily = "plumbing",
                    ValidationType = ValidationType.Model,
                    ValidationTypeReason = "Plumbing routing and fixture evidence is typically checked from model categories and level data.",
                    TriggerKeywords = FindMatchingKeywords(text, new[] { "pipe", "plumbing", "drain", "vent", "water", "fixture", "sanitary", "waste", "slope" }),
                    ExpectedEvidenceSources = new List<string> { "Revit plumbing fixture and pipe elements" },
                    ExpectedFamilyTypeHints = new List<string> { "plumbing fixture", "pipe", "fitting" },
                    ExpectedParameters = new List<string> { "Level" },
                    ExpectedEvidence = "Plumbing categories (Plumbing Fixtures, Pipe Curves, etc.) with Level assignment",
                    ExpectedCategories = new[] { "Plumbing Fixtures", "Pipe Curves", "Pipe Fitting", "Pipe Accessories" },
                    RequiresDirectParameterEvidence = false,
                    AllowsModelOnlyMet = true,
                    WeakEvidenceIfOnlyCategoryLevel = true
                };
                return EvaluateCategoryAndLevelRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Plumbing Fixtures", "Pipe Curves", "Pipe Fitting", "Pipe Accessories" },
                    "Plumbing routing and fixture coverage",
                    "Plumbing-related categories were present in the captured model snapshot.",
                    "Verify the plumbing category and required routing parameters.",
                    requirementText,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsAny(text, new[] { "data", "communication", "fire alarm", "security", "telephone", "nurse call", "low voltage", "device" }))
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "technology_low_voltage_coverage",
                    RuleFamily = "technology",
                    ValidationType = ValidationType.Model,
                    ValidationTypeReason = "Technology device presence and level are usually checked from model categories and exported parameters.",
                    TriggerKeywords = FindMatchingKeywords(text, new[] { "data", "communication", "fire alarm", "security", "telephone", "nurse call", "low voltage", "device" }),
                    ExpectedEvidenceSources = new List<string> { "Revit technology device elements" },
                    ExpectedFamilyTypeHints = new List<string> { "communication device", "data device", "fire alarm", "security device" },
                    ExpectedParameters = new List<string> { "Level", "Panel", "Circuit Number" },
                    ExpectedEvidence = "Technology device categories (Communication, Data, Fire Alarm, etc.) with Level assignment",
                    ExpectedCategories = new[] { "Communication Devices", "Data Devices", "Fire Alarm Devices", "Security Devices", "Nurse Call Devices", "Telephone Devices" },
                    RequiresDirectParameterEvidence = false,
                    AllowsModelOnlyMet = true,
                    WeakEvidenceIfOnlyCategoryLevel = true
                };
                return EvaluateCategoryAndLevelRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[]
                    {
                        "Communication Devices",
                        "Data Devices",
                        "Fire Alarm Devices",
                        "Security Devices",
                        "Nurse Call Devices",
                        "Telephone Devices"
                    },
                    "Technology / low-voltage coverage",
                    "Technology / low-voltage categories were present in the captured model snapshot.",
                    "Verify the device category and required connection parameters.",
                    requirementText,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsAny(text, new[]
            {
                "identification", "identify", "label", "labeling", "nameplate", "tag",
                "manufacturer", "manufacturers", "product", "submittal", "warranty",
                "approved equal", "brady", "carlton", "seton"
            }))
            {
                var ruleCtx = new RuleContext
                {
                    RuleName = "identification_labeling_manufacturer_requirement",
                    RuleFamily = "specification_and_marking",
                    ValidationType = ValidationType.Specification,
                    ValidationTypeReason = "Identification and manufacturer requirements are specification-driven and usually need direct tag/label/manufacturer evidence.",
                    TriggerKeywords = FindMatchingKeywords(text, new[]
                    {
                        "identification", "identify", "label", "labeling", "nameplate", "tag",
                        "manufacturer", "manufacturers", "product", "submittal", "warranty",
                        "approved equal", "brady", "carlton", "seton"
                    }),
                    ExpectedEvidenceSources = new List<string> { "Specification text", "Submittals", "Product data", "Tagged model parameters" },
                    ExpectedFamilyTypeHints = new List<string> { "nameplate", "label", "tag", "manufacturer", "marker" },
                    ExpectedParameters = new List<string> { "Manufacturer", "Tag", "Label", "Nameplate", "Mark", "Identification" },
                    ExpectedEvidence = "Manufacturer, tag, label, nameplate, or identification parameters; otherwise drawings/specifications/manual review.",
                    ExpectedCategories = new[] { "Electrical Equipment", "Electrical Fixtures", "Specialty Equipment" },
                    RequiresDirectParameterEvidence = true,
                    AllowsModelOnlyMet = false,
                    WeakEvidenceIfOnlyCategoryLevel = true,
                    ManualReviewIndicators = new List<string> { "specification", "submittal", "owner standards", "product data" }
                };
                return EvaluateParameterDrivenRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Manufacturer", "Tag", "Label", "Nameplate", "Mark", "Identification" },
                    "Identification, labeling, and manufacturer requirements",
                    "The requirement is about product identification, labeling, or manufacturer criteria, which is not proven by broad category/level data alone.",
                    "Review the specification or submittal, then populate or export manufacturer/tag/label fields if the requirement should be model-checkable.",
                    requirementText,
                    false,
                    new[] { "Electrical Equipment", "Electrical Fixtures", "Specialty Equipment" },
                    false,
                    evidenceIndex,
                    ruleCtx,
                    semanticProfile);
            }

            if (ContainsAny(text, new[]
            {
                "protect", "protection", "clean", "original manufacturer", "work completion",
                "demolition", "remove", "removal", "relocate", "disconnect", "abandoned",
                "salvage", "dispose", "blank cover", "grounding", "conduit", "conductor"
            }))
            {
                var fallbackRule = new RuleContext
                {
                    RuleName = "field_execution_or_demolition_requirement",
                    RuleFamily = "manual_or_drawing_review",
                    ValidationType = ValidationType.Manual,
                    ValidationTypeReason = "Field execution, demolition, protection, conduit, and grounding requirements usually need drawings, specifications, or human verification.",
                    TriggerKeywords = FindMatchingKeywords(text, new[]
                    {
                        "protect", "protection", "clean", "original manufacturer", "work completion",
                        "demolition", "remove", "removal", "relocate", "disconnect", "abandoned",
                        "salvage", "dispose", "blank cover", "grounding", "conduit", "conductor"
                    }),
                    ExpectedEvidenceSources = new List<string> { "Drawings", "Specifications", "Field verification", "Owner direction" },
                    ExpectedFamilyTypeHints = new List<string> { "demolition", "blank cover", "grounding", "conduit", "protection" },
                    ExpectedParameters = new List<string> { "Phase Created", "Phase Demolished", "Demo Status", "Ground Conductor", "Conductor Type", "Status" },
                    ExpectedEvidence = "Drawing, specification, field, or manual evidence rather than generic model presence.",
                    ExpectedCategories = new[] { "Electrical Equipment", "Electrical Fixtures", "Mechanical Equipment", "Communication Devices" },
                    RequiresDirectParameterEvidence = true,
                    AllowsModelOnlyMet = false,
                    WeakEvidenceIfOnlyCategoryLevel = true,
                    ManualReviewIndicators = new List<string> { "field verification", "owner direction", "specification", "drawing review" }
                };
                return EvaluateParameterDrivenRequirement(
                    requirement,
                    modelRecords,
                    selectedDiscipline,
                    new[] { "Phase Created", "Phase Demolished", "Demo Status", "Ground Conductor", "Conductor Type", "Status" },
                    "Field execution, demolition, protection, or grounding requirement",
                    "The requirement depends on drawings, specifications, or field execution evidence and cannot be closed from category/level evidence alone.",
                    "Review the drawings and specifications, or verify the field execution status before closing this item.",
                    requirementText,
                    false,
                    new[] { "Electrical Equipment", "Electrical Fixtures", "Mechanical Equipment", "Communication Devices" },
                    false,
                    evidenceIndex,
                    fallbackRule,
                    semanticProfile);
            }

            var fallbackResult = BuildResult(
                requirement,
                RequirementCheckStatus.NeedsHumanReview,
                0.45,
                BuildIssueTitle(requirement, RequirementCheckStatus.NeedsHumanReview, requirementText, selectedDiscipline),
                BuildReasoningForOpenEndedRequirement(requirementText, selectedDiscipline),
                BuildNextBestAction(RequirementCheckStatus.NeedsHumanReview, requirementText, selectedDiscipline, BuildResponsibleRole(requirement, selectedDiscipline, requirementText), requirementDiscipline),
                "No deterministic rule matched this requirement text.",
                BuildEvidenceSummary(new[] { "The requirement text did not match a deterministic rule." }),
                BuildResponsibleRole(requirement, selectedDiscipline, requirementText),
                requirement.SourceSheet,
                requirement.RowNumber,
                null);
            fallbackResult.EvidenceAlignment = EvidenceAlignmentLevel.ManualOnly;
            fallbackResult.EvidenceAlignmentReason = "No deterministic rule matched. Evidence alignment requires human judgment.";
            fallbackResult.RuleApplied = "(none)";
            fallbackResult.RuleFamily = "unmatched";
            ApplySemanticMetadata(fallbackResult, semanticProfile);
            EnrichValidationType(fallbackResult, requirementText);
            return fallbackResult;
        }

        private RequirementCheckResult EvaluateParameterDrivenRequirement(
            OwnerRequirementRow requirement,
            IReadOnlyCollection<ExportElementRecord> modelRecords,
            RequirementDiscipline selectedDiscipline,
            IEnumerable<string> requiredParameters,
            string issueTitle,
            string evidenceNarrative,
            string suggestedAction,
            string requirementText,
            bool allowAnyLevel = false,
            IEnumerable<string> categoryHints = null,
            bool allowFullModelFallback = false,
            EvidenceIndex evidenceIndex = null,
            RuleContext ruleContext = null,
            RequirementSemanticProfile semanticProfile = null)
        {
            List<ExportElementRecord> candidates = FilterByTextHints(modelRecords, requirementText, categoryHints, evidenceIndex, allowFullModelFallback);
            candidates = NarrowHighRiskCandidates(candidates, ruleContext);
            string responsibleRole = BuildResponsibleRole(requirement, selectedDiscipline, requirementText);
            List<string> parameterList = requiredParameters == null
                ? new List<string>()
                : requiredParameters.ToList();
            string evidenceSummary = string.Empty;

            if (candidates.Count == 0)
            {
                evidenceSummary = BuildEvidenceSummary(new[] { "No matching model elements were available for this check." });
                var insuffResult = BuildResult(
                    requirement,
                    RequirementCheckStatus.InsufficientModelData,
                    0.2,
                    issueTitle,
                    BuildReasoningForInsufficientData(issueTitle, requirementText, selectedDiscipline),
                    BuildNextBestAction(RequirementCheckStatus.InsufficientModelData, requirementText, selectedDiscipline, responsibleRole, issueTitle),
                    "No matching model elements were available for this check.",
                    evidenceSummary,
                    responsibleRole,
                    requirement.SourceSheet,
                    requirement.RowNumber,
                    candidates,
                    parameterList);
                EnrichResult(insuffResult, requirementText, candidates, ruleContext, parameterList, semanticProfile);
                ApplySemanticGuardrail(insuffResult, requirementText, candidates, ruleContext);
                return insuffResult;
            }
            int satisfied = 0;
            List<string> evidence = new List<string>();

            foreach (ExportElementRecord record in candidates)
            {
                bool hasValue = allowAnyLevel
                    ? HasNonEmptyParameter(record, "Level")
                    : parameterList.Count <= 1
                        ? parameterList.Any(parameter => HasNonEmptyParameter(record, parameter))
                        : parameterList.All(parameter => HasNonEmptyParameter(record, parameter));

                if (hasValue)
                {
                    satisfied++;
                }
            }

            evidence.Add(candidates.Count.ToString(CultureInfo.InvariantCulture) + " candidate element(s) inspected.");
            evidence.Add(satisfied.ToString(CultureInfo.InvariantCulture) + " element(s) already contain the required parameter data.");
            evidenceSummary = BuildEvidenceSummary(evidence);

            if (satisfied == 0)
            {
                var notMetResult = BuildResult(
                    requirement,
                    RequirementCheckStatus.NotMet,
                    0.86,
                    issueTitle,
                    BuildReasoningForNotMet(issueTitle, requirementText, evidenceNarrative, selectedDiscipline, evidence, candidates.Count, satisfied),
                    BuildNextBestAction(RequirementCheckStatus.NotMet, requirementText, selectedDiscipline, responsibleRole, issueTitle),
                    evidence,
                    evidenceSummary,
                    responsibleRole,
                    requirement.SourceSheet,
                    requirement.RowNumber,
                    candidates,
                    parameterList);
                EnrichResult(notMetResult, requirementText, candidates, ruleContext, parameterList, semanticProfile);
                ApplySemanticGuardrail(notMetResult, requirementText, candidates, ruleContext);
                return notMetResult;
            }

            if (satisfied < candidates.Count)
            {
                evidence.Add((candidates.Count - satisfied).ToString(CultureInfo.InvariantCulture) + " element(s) still need attention.");
                evidenceSummary = BuildEvidenceSummary(evidence);
                var partialResult = BuildResult(
                    requirement,
                    RequirementCheckStatus.NotMet,
                    0.79,
                    issueTitle,
                    BuildReasoningForPartialCoverage(issueTitle, requirementText, evidenceNarrative, selectedDiscipline, evidence, candidates.Count, satisfied),
                    BuildNextBestAction(RequirementCheckStatus.NotMet, requirementText, selectedDiscipline, responsibleRole, issueTitle),
                    evidence,
                    evidenceSummary,
                    responsibleRole,
                    requirement.SourceSheet,
                    requirement.RowNumber,
                    candidates,
                    parameterList);
                EnrichResult(partialResult, requirementText, candidates, ruleContext, parameterList, semanticProfile);
                ApplySemanticGuardrail(partialResult, requirementText, candidates, ruleContext);
                return partialResult;
            }

            evidence.Add("All candidate elements satisfied the selected parameter check.");
            evidenceSummary = BuildEvidenceSummary(evidence);
            var metResult = BuildResult(
                requirement,
                RequirementCheckStatus.Met,
                0.94,
                issueTitle,
                BuildReasoningForMet(issueTitle, requirementText, evidenceNarrative, selectedDiscipline, evidence, candidates.Count),
                "No action required for this requirement.",
                evidence,
                evidenceSummary,
                responsibleRole,
                requirement.SourceSheet,
                requirement.RowNumber,
                candidates,
                parameterList);
            EnrichResult(metResult, requirementText, candidates, ruleContext, parameterList, semanticProfile);
            ApplySemanticGuardrail(metResult, requirementText, candidates, ruleContext);
            return metResult;
        }

        private RequirementCheckResult EvaluateCategoryAndLevelRequirement(
            OwnerRequirementRow requirement,
            IReadOnlyCollection<ExportElementRecord> modelRecords,
            RequirementDiscipline selectedDiscipline,
            IEnumerable<string> expectedCategoryHints,
            string issueTitle,
            string evidenceNarrative,
            string suggestedAction,
            string requirementText,
            EvidenceIndex evidenceIndex = null,
            RuleContext ruleContext = null,
            RequirementSemanticProfile semanticProfile = null)
        {
            List<string> categoryHintList = expectedCategoryHints == null
                ? new List<string>()
                : expectedCategoryHints.ToList();
            List<ExportElementRecord> candidates = FilterByTextHints(modelRecords, requirementText, categoryHintList, evidenceIndex, false);
            string responsibleRole = BuildResponsibleRole(requirement, selectedDiscipline, requirementText);

            if (candidates.Count == 0)
            {
                var insuffResult = BuildResult(
                    requirement,
                    RequirementCheckStatus.InsufficientModelData,
                    0.2,
                    issueTitle,
                    BuildReasoningForInsufficientData(issueTitle, requirementText, selectedDiscipline),
                    BuildNextBestAction(RequirementCheckStatus.InsufficientModelData, requirementText, selectedDiscipline, responsibleRole, issueTitle),
                    "No matching model elements were available for this check.",
                    BuildEvidenceSummary(new[] { "No matching model elements were available for this check." }),
                    responsibleRole,
                    requirement.SourceSheet,
                    requirement.RowNumber,
                    candidates,
                    new[] { "Level" });
                EnrichResult(insuffResult, requirementText, candidates, ruleContext, new[] { "Level" }, semanticProfile);
                ApplySemanticGuardrail(insuffResult, requirementText, candidates, ruleContext);
                return insuffResult;
            }

            int withLevel = candidates.Count(record => HasNonEmptyParameter(record, "Level") || !string.IsNullOrWhiteSpace(record.Level));
            List<string> evidence = new List<string>
            {
                candidates.Count.ToString(CultureInfo.InvariantCulture) + " candidate element(s) inspected.",
                withLevel.ToString(CultureInfo.InvariantCulture) + " candidate element(s) have level data."
            };
            string evidenceSummary = BuildEvidenceSummary(evidence);

            if (withLevel == 0)
            {
                var notMetResult = BuildResult(
                    requirement,
                    RequirementCheckStatus.NotMet,
                    0.82,
                    issueTitle,
                    BuildReasoningForNotMet(issueTitle, requirementText, evidenceNarrative, selectedDiscipline, evidence, candidates.Count, withLevel),
                    BuildNextBestAction(RequirementCheckStatus.NotMet, requirementText, selectedDiscipline, responsibleRole, issueTitle),
                    evidence,
                    evidenceSummary,
                    responsibleRole,
                    requirement.SourceSheet,
                    requirement.RowNumber,
                    candidates,
                    new[] { "Level" });
                EnrichResult(notMetResult, requirementText, candidates, ruleContext, new[] { "Level" }, semanticProfile);
                ApplySemanticGuardrail(notMetResult, requirementText, candidates, ruleContext);
                return notMetResult;
            }

            if (withLevel < candidates.Count)
            {
                evidence.Add((candidates.Count - withLevel).ToString(CultureInfo.InvariantCulture) + " candidate element(s) still need review.");
                evidenceSummary = BuildEvidenceSummary(evidence);
                var reviewResult = BuildResult(
                    requirement,
                    RequirementCheckStatus.NeedsHumanReview,
                    0.66,
                    issueTitle,
                    BuildReasoningForPartialCoverage(issueTitle, requirementText, evidenceNarrative, selectedDiscipline, evidence, candidates.Count, withLevel),
                    BuildNextBestAction(RequirementCheckStatus.NeedsHumanReview, requirementText, selectedDiscipline, responsibleRole, issueTitle),
                    evidence,
                    evidenceSummary,
                    responsibleRole,
                    requirement.SourceSheet,
                    requirement.RowNumber,
                    candidates,
                    new[] { "Level" });
                EnrichResult(reviewResult, requirementText, candidates, ruleContext, new[] { "Level" }, semanticProfile);
                ApplySemanticGuardrail(reviewResult, requirementText, candidates, ruleContext);
                return reviewResult;
            }

            evidence.Add("All candidate elements satisfied the first-pass category and level check.");
            evidenceSummary = BuildEvidenceSummary(evidence);
            var metResult = BuildResult(
                requirement,
                RequirementCheckStatus.Met,
                0.9,
                issueTitle,
                BuildReasoningForMet(issueTitle, requirementText, evidenceNarrative, selectedDiscipline, evidence, candidates.Count),
                "No action required for this requirement.",
                evidence,
                evidenceSummary,
                responsibleRole,
                requirement.SourceSheet,
                requirement.RowNumber,
                candidates,
                new[] { "Level" });
            EnrichResult(metResult, requirementText, candidates, ruleContext, new[] { "Level" }, semanticProfile);
            ApplySemanticGuardrail(metResult, requirementText, candidates, ruleContext);
            return metResult;
        }

        private static void EnrichResult(
            RequirementCheckResult result,
            string requirementText,
            List<ExportElementRecord> candidates,
            RuleContext ruleContext,
            IEnumerable<string> expectedParameters,
            RequirementSemanticProfile semanticProfile = null)
        {
            if (result == null) return;

            ApplySemanticMetadata(result, semanticProfile);

            if (ruleContext != null)
            {
                result.RuleApplied = ruleContext.RuleName;
                result.RuleFamily = ruleContext.RuleFamily;
                result.RuleTriggerKeywords = ruleContext.TriggerKeywords ?? new List<string>();
                result.RuleExpectedEvidence = ruleContext.ExpectedEvidence;
                result.ExpectedEvidenceSources = ruleContext.ExpectedEvidenceSources ?? new List<string>();
                result.ExpectedCategories = ruleContext.ExpectedCategories == null
                    ? new List<string>()
                    : ruleContext.ExpectedCategories.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
                result.ExpectedFamilyTypeHints = ruleContext.ExpectedFamilyTypeHints ?? new List<string>();
                result.ExpectedParameters = ruleContext.ExpectedParameters ?? new List<string>();
                result.CandidateScopeReason = ruleContext.CandidateScopeReason;
                result.FallbackUsed = ruleContext.FallbackUsed;
                result.FallbackAllowed = ruleContext.FallbackAllowed;
                result.ModelEvidenceSufficiency = ruleContext.ModelEvidenceSufficiency;
                result.WhyNotModelCloseable = ruleContext.WhyNotModelCloseable;
                if (ruleContext.ValidationType.HasValue)
                {
                    result.ValidationType = ruleContext.ValidationType.Value;
                    result.ValidationTypeReason = string.IsNullOrWhiteSpace(ruleContext.ValidationTypeReason)
                        ? result.ValidationTypeReason
                        : ruleContext.ValidationTypeReason;
                }
            }

            bool preserveRuleContextValidationType = ruleContext != null && ruleContext.ValidationType.HasValue;
            EnrichValidationType(result, requirementText, preserveRuleContextValidationType);

            List<string> paramList = expectedParameters == null
                ? new List<string>()
                : expectedParameters.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            result.ParameterValueExamples = BuildParameterValueExamples(candidates, paramList);
            result.MissingEvidenceDetails = BuildMissingEvidenceDetails(candidates, paramList);
            result.ExpectedParameters = paramList.Count > 0 ? paramList : result.ExpectedParameters;
            result.ActualMatchedCategories = BuildDistinctValues(candidates, record => record.Category);
            result.ActualMatchedParameters = BuildActualMatchedParameters(candidates, paramList);
            result.MatchedCategories = result.ActualMatchedCategories.ToList();
            result.MatchedParameters = result.ActualMatchedParameters.ToList();
            result.ActualParameterValueExamples = result.ParameterValueExamples.ToList();
            result.MissingExpectedParameters = result.MissingEvidenceDetails.Select(detail => detail.ParameterName).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            result.ParameterChecks = BuildParameterChecks(candidates, paramList);
            result.MatchedFamilyTypeSummary = BuildFamilyTypeSummary(candidates);
            result.DirectClosingEvidence = BuildDirectClosingEvidence(result, candidates, ruleContext, paramList);
            result.SupportingContext = BuildSupportingContext(result, candidates, ruleContext);
            result.MissingDirectEvidence = BuildMissingDirectEvidence(result, candidates, ruleContext, paramList);
            result.EvidenceAlignment = AssessEvidenceAlignment(result, requirementText, candidates, ruleContext);
            result.EvidenceAlignmentReason = BuildAlignmentReason(result.EvidenceAlignment, result, candidates, ruleContext);
            result.FilterTrace = BuildFilterTrace(result, requirementText, candidates, ruleContext, paramList, semanticProfile);
            result.FilterTrace.DirectClosingEvidence = result.DirectClosingEvidence ?? new List<string>();
            result.FilterTrace.SupportingContext = result.SupportingContext ?? new List<string>();
            result.FilterTrace.MissingDirectEvidence = result.MissingDirectEvidence ?? new List<string>();
            result.ModelEvidenceLimitations = BuildModelEvidenceLimitations(result, candidates, ruleContext, paramList);
            result.StatusReason = result.Reasoning;
            result.HumanReviewNeeded = result.Status == RequirementCheckStatus.NeedsHumanReview;
            result.ConfidenceReason = BuildConfidenceReason(result, candidates, ruleContext, paramList);
            ApplyNarrativeGuardrails(result, requirementText, ruleContext);
        }

        private static void EnrichValidationType(RequirementCheckResult result, string requirementText, bool preserveExisting = false)
        {
            if (result == null) return;
            var vtResult = ValidationTypeClassifier.Classify(
                requirementText,
                result.SourceWorksheet ?? string.Empty);
            if (!preserveExisting || string.IsNullOrWhiteSpace(result.ValidationTypeReason))
            {
                result.ValidationType = vtResult.PrimaryType;
                result.ValidationTypeReason = vtResult.Reasoning;
            }
            else if (!string.IsNullOrWhiteSpace(vtResult.Reasoning))
            {
                result.ValidationTypeReason = result.ValidationTypeReason + " " + vtResult.Reasoning;
            }

            result.TaxonomyLabels = vtResult.TaxonomyLabels;
        }

        private static EvidenceAlignmentLevel AssessEvidenceAlignment(
            RequirementCheckResult result,
            string requirementText,
            List<ExportElementRecord> candidates,
            RuleContext ruleContext)
        {
            if (result.Status == RequirementCheckStatus.InsufficientModelData)
                return EvidenceAlignmentLevel.Weak;
            if (result.Status == RequirementCheckStatus.NotApplicable)
                return EvidenceAlignmentLevel.ManualOnly;
            if (result.Status == RequirementCheckStatus.NeedsHumanReview && (candidates == null || candidates.Count == 0))
                return EvidenceAlignmentLevel.ManualOnly;

            if (candidates == null || candidates.Count == 0)
                return EvidenceAlignmentLevel.Weak;

            string normalizedReq = RequirementDisciplineNormalizer.NormalizeText(requirementText ?? string.Empty);
            bool reqNeedsSpecificEvidence = ContainsAny(normalizedReq, SpecificEvidenceKeywords);
            bool requiresDirectEvidence = ruleContext != null && ruleContext.RequiresDirectParameterEvidence;
            bool modelOnlyAllowed = ruleContext == null || ruleContext.AllowsModelOnlyMet;
            bool hasDirectEvidence = result != null && result.DirectClosingEvidence != null && result.DirectClosingEvidence.Any(item => !string.IsNullOrWhiteSpace(item));
            bool isUnknown = string.Equals(result?.RequirementType, "unknown_ambiguous", StringComparison.OrdinalIgnoreCase);
            int matchedParameterCount = result?.ActualMatchedParameters != null ? result.ActualMatchedParameters.Count : 0;

            if (requiresDirectEvidence && !hasDirectEvidence)
            {
                return reqNeedsSpecificEvidence ? EvidenceAlignmentLevel.MismatchRisk : EvidenceAlignmentLevel.Weak;
            }

            if (requiresDirectEvidence && matchedParameterCount < 2)
            {
                return reqNeedsSpecificEvidence ? EvidenceAlignmentLevel.MismatchRisk : EvidenceAlignmentLevel.Weak;
            }

            if (isUnknown)
            {
                return hasDirectEvidence ? EvidenceAlignmentLevel.ManualOnly : EvidenceAlignmentLevel.Weak;
            }

            bool modelOnlyMetBlocked = ruleContext != null && !ruleContext.AllowsModelOnlyMet;

            if (ruleContext?.ExpectedCategories != null && ruleContext.ExpectedCategories.Length > 0)
            {
                int categoryMatchCount = 0;
                foreach (var candidate in candidates)
                {
                    if (candidate == null || string.IsNullOrWhiteSpace(candidate.Category)) continue;
                    string normalizedCat = RequirementDisciplineNormalizer.NormalizeText(candidate.Category);
                    foreach (string expected in ruleContext.ExpectedCategories)
                    {
                        if (normalizedCat.IndexOf(RequirementDisciplineNormalizer.NormalizeText(expected), StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            categoryMatchCount++;
                            break;
                        }
                    }
                }

                double categoryRatio = (double)categoryMatchCount / candidates.Count;

                if (categoryRatio >= 0.8 && !reqNeedsSpecificEvidence && !modelOnlyMetBlocked && hasDirectEvidence)
                    return EvidenceAlignmentLevel.Strong;
                if (categoryRatio >= 0.5 && !reqNeedsSpecificEvidence && hasDirectEvidence)
                    return EvidenceAlignmentLevel.Partial;
                if (categoryRatio < 0.3 && (reqNeedsSpecificEvidence || requiresDirectEvidence))
                    return EvidenceAlignmentLevel.MismatchRisk;
                if (reqNeedsSpecificEvidence || requiresDirectEvidence || !hasDirectEvidence)
                    return EvidenceAlignmentLevel.Weak;
                if (categoryRatio >= 0.3)
                    return EvidenceAlignmentLevel.Partial;
                return EvidenceAlignmentLevel.Weak;
            }

            if (requiresDirectEvidence)
            {
                return hasDirectEvidence
                    ? EvidenceAlignmentLevel.Strong
                    : (reqNeedsSpecificEvidence ? EvidenceAlignmentLevel.MismatchRisk : EvidenceAlignmentLevel.Weak);
            }

            if (result.MatchedModelElementCount > 0 && !reqNeedsSpecificEvidence)
                return hasDirectEvidence || modelOnlyAllowed
                    ? EvidenceAlignmentLevel.Strong
                    : EvidenceAlignmentLevel.Partial;

            return reqNeedsSpecificEvidence || !hasDirectEvidence ? EvidenceAlignmentLevel.Weak : EvidenceAlignmentLevel.Partial;
        }

        private static string BuildAlignmentReason(
            EvidenceAlignmentLevel level,
            RequirementCheckResult result,
            List<ExportElementRecord> candidates,
            RuleContext ruleContext)
        {
            string ruleName = ruleContext?.RuleName ?? "(none)";
            int candidateCount = candidates?.Count ?? 0;

            switch (level)
            {
                case EvidenceAlignmentLevel.Strong:
                    return "Evidence categories closely match the requirement. Rule '" + ruleName + "' found " + candidateCount + " element(s) in the expected categories.";
                case EvidenceAlignmentLevel.Partial:
                    return "Evidence partially aligns. Some candidate elements match the expected categories but full semantic coverage is not confirmed. Rule: '" + ruleName + "'.";
                case EvidenceAlignmentLevel.Weak:
                    return "Evidence alignment is weak. The matched elements may not directly address the specific requirement intent. Human review recommended.";
                case EvidenceAlignmentLevel.MismatchRisk:
                    return "The requirement asks for specific evidence (identification, manufacturer, specification) but only generic category/level data was found. This status should not be treated as confident without manual verification.";
                case EvidenceAlignmentLevel.ManualOnly:
                    return "No model-based evidence is available. This requirement depends on drawings, specifications, or manual coordination.";
                default:
                    return "Evidence alignment could not be determined.";
            }
        }

        private static void ApplySemanticGuardrail(
            RequirementCheckResult result,
            string requirementText,
            List<ExportElementRecord> candidates,
            RuleContext ruleContext)
        {
            if (result == null)
                return;

            bool weakEvidence = result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk ||
                result.EvidenceAlignment == EvidenceAlignmentLevel.Weak ||
                result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly;
            bool requiresDirectEvidence = ruleContext != null && ruleContext.RequiresDirectParameterEvidence;
            bool modelOnlyAllowed = ruleContext == null || ruleContext.AllowsModelOnlyMet;
            bool hasDirectEvidence = result.DirectClosingEvidence != null && result.DirectClosingEvidence.Any(item => !string.IsNullOrWhiteSpace(item));
            bool isUnknown = string.Equals(result.RequirementType, "unknown_ambiguous", StringComparison.OrdinalIgnoreCase);
            bool metWithoutStrongEvidence = result.Status == RequirementCheckStatus.Met &&
                (result.EvidenceAlignment != EvidenceAlignmentLevel.Strong || !hasDirectEvidence || isUnknown);

            if (metWithoutStrongEvidence || (result.Status == RequirementCheckStatus.Met && weakEvidence))
            {
                result.Status = RequirementCheckStatus.NeedsHumanReview;
                result.Confidence = Math.Min(result.Confidence, 0.55);
                result.IsKeyIssue = true;
                result.Urgency = "Needs Review";
                result.HumanReviewNeeded = true;
                result.Reasoning = BuildReviewEscalationReason(requirementText, ruleContext, result.Reasoning ?? string.Empty, result.EvidenceAlignment);
                result.NextBestAction = BuildReviewEscalationAction(requirementText, ruleContext);
                RewriteNarrativeForStatus(result, requirementText, ruleContext);
                return;
            }

            if ((result.Status == RequirementCheckStatus.NotMet || result.Status == RequirementCheckStatus.InsufficientModelData) &&
                weakEvidence &&
                (requiresDirectEvidence || !modelOnlyAllowed))
            {
                result.Status = RequirementCheckStatus.NeedsHumanReview;
                result.Confidence = Math.Min(result.Confidence, 0.60);
                result.Urgency = "Needs Review";
                result.HumanReviewNeeded = true;
                string originalReasoning = result.Reasoning ?? string.Empty;
                result.Reasoning = BuildReviewEscalationReason(requirementText, ruleContext, originalReasoning, result.EvidenceAlignment);
                result.NextBestAction = BuildReviewEscalationAction(requirementText, ruleContext);
                RewriteNarrativeForStatus(result, requirementText, ruleContext);
                return;
            }

            if (result.Status != RequirementCheckStatus.Met)
            {
                RewriteNarrativeForStatus(result, requirementText, ruleContext);
            }
        }

        private static void RewriteNarrativeForStatus(
            RequirementCheckResult result,
            string requirementText,
            RuleContext ruleContext)
        {
            if (result == null)
            {
                return;
            }

            string originalReasoning = result.Reasoning ?? string.Empty;
            string originalAction = result.NextBestAction ?? string.Empty;

            switch (result.Status)
            {
                case RequirementCheckStatus.NotMet:
                    result.Reasoning = BuildReasoningForNotMet(
                        BuildFocusLabel(result.IssueTitle, requirementText, RequirementDiscipline.All),
                        requirementText,
                        BuildEvidenceSummary(result.Evidence ?? new List<string>()),
                        RequirementDiscipline.All,
                        result.Evidence ?? new List<string>(),
                        result.MatchedModelElementCount,
                        result.MatchedModelElementCount > 0 ? 1 : 0);
                    if (string.IsNullOrWhiteSpace(result.NextBestAction) || originalAction.IndexOf("No action required", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.NextBestAction = BuildReviewEscalationAction(requirementText, ruleContext);
                    }
                    result.StatusReason = result.Reasoning;
                    break;
                case RequirementCheckStatus.NeedsHumanReview:
                    result.Reasoning = BuildReviewEscalationReason(requirementText, ruleContext, originalReasoning, result.EvidenceAlignment);
                    result.NextBestAction = BuildReviewEscalationAction(requirementText, ruleContext);
                    result.StatusReason = result.Reasoning;
                    break;
                case RequirementCheckStatus.InsufficientModelData:
                    result.Reasoning = "The requirement appears model-checkable, but the current export did not contain relevant candidates or direct evidence for closure.";
                    result.NextBestAction = BuildReviewEscalationAction(requirementText, ruleContext);
                    result.StatusReason = result.Reasoning;
                    break;
                case RequirementCheckStatus.NotApplicable:
                    result.StatusReason = string.IsNullOrWhiteSpace(result.StatusReason) ? originalReasoning : result.StatusReason;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(result.Reasoning))
            {
                result.Reasoning = result.Reasoning
                    .Replace("marked as Met", "requires review")
                    .Replace("No action required", "Further review is required");
            }

            if (!string.IsNullOrWhiteSpace(result.NextBestAction) &&
                result.Status != RequirementCheckStatus.Met &&
                result.NextBestAction.IndexOf("No action required", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.NextBestAction = BuildReviewEscalationAction(requirementText, ruleContext);
            }

            if (result.Status != RequirementCheckStatus.Met &&
                !string.IsNullOrWhiteSpace(result.ModelEvidenceLimitations) &&
                result.ModelEvidenceLimitations.IndexOf("No major limitations", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.ModelEvidenceLimitations = BuildModelEvidenceLimitations(result, new List<ExportElementRecord>(), ruleContext, result.ExpectedParameters);
            }

            result.ConfidenceReason = BuildConfidenceReason(result, new List<ExportElementRecord>(), ruleContext, result.ExpectedParameters);
        }

        private static void ApplyNarrativeGuardrails(
            RequirementCheckResult result,
            string requirementText,
            RuleContext ruleContext)
        {
            if (result == null)
            {
                return;
            }

            if (result.Status != RequirementCheckStatus.Met)
            {
                RewriteNarrativeForStatus(result, requirementText, ruleContext);
            }

            if (!string.IsNullOrWhiteSpace(result.Reasoning))
            {
                result.Reasoning = result.Reasoning
                    .Replace("marked as Met", "requires review")
                    .Replace("No action required", "Further review is required")
                    .Replace("No major limitations were detected in the current pass.", "The current evidence was not sufficient to close this requirement.");
            }

            if (!string.IsNullOrWhiteSpace(result.NextBestAction) &&
                result.Status != RequirementCheckStatus.Met &&
                result.NextBestAction.IndexOf("No action required", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.NextBestAction = BuildReviewEscalationAction(requirementText, ruleContext);
            }

            if (result.Status != RequirementCheckStatus.Met &&
                !string.IsNullOrWhiteSpace(result.ModelEvidenceLimitations) &&
                result.ModelEvidenceLimitations.IndexOf("No major limitations", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.ModelEvidenceLimitations = "The current evidence was not sufficient to close this requirement.";
            }

            result.StatusReason = result.Reasoning;
            result.ConfidenceReason = BuildConfidenceReason(result, new List<ExportElementRecord>(), ruleContext, result.ExpectedParameters);
        }

        private static string BuildReviewEscalationReason(
            string requirementText,
            RuleContext ruleContext,
            string originalReasoning,
            EvidenceAlignmentLevel alignment)
        {
            string evidenceSentence = "Model evidence is supporting context only. Direct closing evidence is missing or requires drawing/specification/field review.";
            string reason = " The evidence alignment is " + alignment.ToString() + ", so the result cannot be treated as a confident deterministic match.";

            if (!string.IsNullOrWhiteSpace(ruleContext?.ValidationTypeReason))
            {
                reason += " " + ruleContext.ValidationTypeReason;
            }

            if (!string.IsNullOrWhiteSpace(originalReasoning))
            {
                string sanitized = originalReasoning
                    .Replace("marked as Not Met", "requires review")
                    .Replace("marked as Met", "requires review")
                    .Replace("No action required", "Further review is required");
                reason += " " + sanitized;
            }

            return evidenceSentence + reason;
        }

        private static string BuildReviewEscalationAction(string requirementText, RuleContext ruleContext)
        {
            if (ContainsAny(Normalize(requirementText), new[] { "identification", "identify", "label", "labeling", "nameplate", "tag", "manufacturer", "submittal" }))
            {
                return "Review the specification or submittal, then export the identification and manufacturer parameters if the requirement should be model-checkable.";
            }

            if (ContainsAny(Normalize(requirementText), new[] { "p-trap", "ptrap", "clevis hanger", "clevis", "pipe hanger", "pipe support", "hanger rod", "seismic hanger", "trapeze" }))
            {
                return "Review the hanger detail or plumbing specification, then populate hanger type, spacing, and comments before rerunning the check.";
            }

            if (ContainsAny(Normalize(requirementText), new[] { "outlet", "receptacle", "duplex", "general purpose circuit", "120v" }))
            {
                return "Verify the outlet voltage, panel, circuit, room, and load context in Revit or panel schedules before closing this item.";
            }

            if (ContainsAny(Normalize(requirementText), new[] { "protect", "protection", "clean", "demolition", "remove", "relocate", "disconnect", "salvage", "grounding", "conduit", "conductor" }))
            {
                return "Review the drawings, specifications, and field execution evidence before closing this item.";
            }

            if (!string.IsNullOrWhiteSpace(ruleContext?.ExpectedEvidence))
            {
                return "Review the expected evidence: " + ruleContext.ExpectedEvidence + ".";
            }

            return "Review the drawings, specifications, and project context before closing this item.";
        }

        private static List<string> BuildParameterValueExamples(
            List<ExportElementRecord> candidates,
            List<string> expectedParameters)
        {
            var examples = new List<string>();
            if (candidates == null || candidates.Count == 0 || expectedParameters == null || expectedParameters.Count == 0)
                return examples;

            foreach (string paramName in expectedParameters.Take(5))
            {
                foreach (var record in candidates.Take(3))
                {
                    string value = GetParameterValue(record, paramName);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string category = record.Category ?? "Unknown";
                        examples.Add(paramName + " = \"" + value + "\" (" + category + ", ElementId " +
                            (record.ElementId > 0 ? record.ElementId.ToString(CultureInfo.InvariantCulture) : "?") + ")");
                        break;
                    }
                }
            }

            return examples;
        }

        private static List<string> BuildDistinctValues(
            IEnumerable<ExportElementRecord> candidates,
            Func<ExportElementRecord, string> selector)
        {
            if (candidates == null || selector == null)
            {
                return new List<string>();
            }

            return candidates
                .Select(selector)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();
        }

        private static List<string> BuildActualMatchedParameters(
            IEnumerable<ExportElementRecord> candidates,
            IEnumerable<string> expectedParameters)
        {
            List<string> actual = new List<string>();
            if (candidates == null || expectedParameters == null)
            {
                return actual;
            }

            foreach (string parameter in expectedParameters)
            {
                if (string.IsNullOrWhiteSpace(parameter))
                {
                    continue;
                }

                if (candidates.Any(record => HasNonEmptyParameter(record, parameter)))
                {
                    actual.Add(parameter);
                }
            }

            return actual.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<ParameterCheckResult> BuildParameterChecks(
            IEnumerable<ExportElementRecord> candidates,
            IEnumerable<string> expectedParameters)
        {
            var checks = new List<ParameterCheckResult>();
            if (expectedParameters == null)
            {
                return checks;
            }

            List<ExportElementRecord> candidateList = candidates == null
                ? new List<ExportElementRecord>()
                : candidates.Where(item => item != null).Take(10).ToList();

            foreach (string parameterName in expectedParameters.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ExportElementRecord match = candidateList.FirstOrDefault(record => HasNonEmptyParameter(record, parameterName));
                ExportElementRecord emptyMatch = candidateList.FirstOrDefault(record => HasParameter(record, parameterName));
                string actualValue = GetParameterValue(match ?? emptyMatch, parameterName);
                bool isPresent = emptyMatch != null || match != null;
                bool isEmpty = isPresent && string.IsNullOrWhiteSpace(actualValue);
                bool isMatch = match != null;
                string source = match != null
                    ? GetParameterSource(match, parameterName)
                    : emptyMatch != null
                        ? GetParameterSource(emptyMatch, parameterName)
                        : "not in export";

                checks.Add(new ParameterCheckResult
                {
                    ParameterName = parameterName,
                    ExpectedMeaning = DescribeExpectedMeaning(parameterName),
                    ExpectedValuePattern = DescribeExpectedPattern(parameterName),
                    ActualValue = string.IsNullOrWhiteSpace(actualValue) ? (isPresent ? "(empty)" : "(not found)") : actualValue,
                    Source = source,
                    IsPresent = isPresent,
                    IsEmpty = isEmpty,
                    IsMatch = isMatch,
                    IsRequired = true,
                    FailureReason = BuildParameterFailureReason(parameterName, isPresent, isEmpty, isMatch)
                });
            }

            return checks;
        }

        private static List<string> BuildFamilyTypeSummary(IEnumerable<ExportElementRecord> candidates)
        {
            if (candidates == null)
            {
                return new List<string>();
            }

            return candidates
                .Where(record => record != null)
                .GroupBy(record => string.Join(" | ", new[]
                {
                    SafeText(record.Family),
                    SafeText(record.Type)
                }), StringComparer.OrdinalIgnoreCase)
                .Select(group => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1} element(s)",
                    group.Key,
                    group.Count()))
                .Take(10)
                .ToList();
        }

        private static List<string> BuildDirectClosingEvidence(
            RequirementCheckResult result,
            IReadOnlyCollection<ExportElementRecord> candidates,
            RuleContext ruleContext,
            IReadOnlyCollection<string> expectedParameters)
        {
            List<string> evidence = new List<string>();
            List<string> directHints = ruleContext?.DirectClosingEvidence == null
                ? new List<string>()
                : ruleContext.DirectClosingEvidence.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
            List<string> directParameters = result?.ParameterChecks == null
                ? new List<string>()
                : result.ParameterChecks.Where(check => check != null && check.IsMatch)
                    .Select(check => string.IsNullOrWhiteSpace(check.ActualValue)
                        ? check.ParameterName
                        : check.ParameterName + " = " + check.ActualValue)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (directParameters.Count > 0)
            {
                evidence.AddRange(directParameters);
            }

            bool allowModelSignals = ruleContext != null &&
                ruleContext.AllowsModelOnlyMet &&
                directHints.Any(hint =>
                    ContainsAny(Normalize(hint), new[]
                    {
                        "category", "family", "type", "level", "placement", "presence", "location", "mounted", "elevation"
                    }));

            if (allowModelSignals)
            {
                foreach (ExportElementRecord record in candidates?.Take(5) ?? Enumerable.Empty<ExportElementRecord>())
                {
                    if (record == null)
                    {
                        continue;
                    }

                    string summary = string.Join(" | ", new[]
                    {
                        SafeText(record.Category),
                        SafeText(record.Family),
                        SafeText(record.Type),
                        string.IsNullOrWhiteSpace(record.Level) ? string.Empty : "Level " + record.Level
                    }.Where(item => !string.IsNullOrWhiteSpace(item)));

                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        evidence.Add(summary);
                    }
                }
            }

            return evidence.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        }

        private static List<string> BuildSupportingContext(
            RequirementCheckResult result,
            IReadOnlyCollection<ExportElementRecord> candidates,
            RuleContext ruleContext)
        {
            List<string> context = new List<string>();

            foreach (string value in result?.ActualMatchedCategories ?? result?.MatchedCategories ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    context.Add("Category: " + value);
                }
            }

            foreach (string value in result?.MatchedFamilyTypeSummary ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    context.Add("Family/Type: " + value);
                }
            }

            foreach (ExportElementRecord record in candidates?.Take(5) ?? Enumerable.Empty<ExportElementRecord>())
            {
                if (record == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(record.Level))
                {
                    context.Add("Level: " + record.Level);
                }

                if (record.ElementId > 0)
                {
                    context.Add("ElementId: " + record.ElementId.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (ruleContext != null)
            {
                foreach (string value in ruleContext.AllowedCategories ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        context.Add("Expected Category: " + value);
                    }
                }
            }

            return context.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
        }

        private static List<string> BuildMissingDirectEvidence(
            RequirementCheckResult result,
            IReadOnlyCollection<ExportElementRecord> candidates,
            RuleContext ruleContext,
            IReadOnlyCollection<string> expectedParameters)
        {
            List<string> missing = new List<string>();

            if (result != null &&
                result.Status == RequirementCheckStatus.Met &&
                result.DirectClosingEvidence != null &&
                result.DirectClosingEvidence.Count > 0)
            {
                return missing;
            }

            foreach (string parameter in result?.MissingExpectedParameters ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(parameter))
                {
                    missing.Add(parameter);
                }
            }

            if (missing.Count == 0 && ruleContext != null && !ruleContext.AllowsModelOnlyMet)
            {
                missing.Add("Direct closing evidence is missing or requires drawing/specification/field review.");
            }

            if (missing.Count == 0 && candidates != null && candidates.Count > 0 && result != null && result.Status != RequirementCheckStatus.Met)
            {
                missing.Add("Supporting context is present, but direct closing evidence is still missing.");
            }

            if (expectedParameters != null && expectedParameters.Count > 0 && missing.Count == 0 && ruleContext != null && ruleContext.RequiresDirectParameterEvidence)
            {
                missing.AddRange(expectedParameters.Where(item => !string.IsNullOrWhiteSpace(item)).Take(5));
            }

            return missing.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        }

        private static RequirementFilterTrace BuildFilterTrace(
            RequirementCheckResult result,
            string requirementText,
            IReadOnlyCollection<ExportElementRecord> candidates,
            RuleContext ruleContext,
            IReadOnlyCollection<string> expectedParameters,
            RequirementSemanticProfile semanticProfile = null)
        {
            List<FilterStageTrace> stages = new List<FilterStageTrace>();
            int inputCount = candidates?.Count ?? 0;
            List<string> keywords = ruleContext?.TriggerKeywords ?? FindMatchingKeywords(requirementText, new[] { "provide", "install", "require", "shall" });
            List<string> expectedCategories = ruleContext?.ExpectedCategories == null
                ? new List<string>()
                : ruleContext.ExpectedCategories.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
            List<string> expectedHints = ruleContext?.ExpectedFamilyTypeHints ?? new List<string>();
            List<string> expectedEvidenceSources = ruleContext?.ExpectedEvidenceSources ?? new List<string>();
            List<string> parameterList = expectedParameters == null
                ? new List<string>()
                : expectedParameters.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();

            stages.Add(new FilterStageTrace
            {
                StageName = "Model snapshot loaded",
                Description = "All synced Revit elements were loaded into the deterministic evidence index.",
                InputCount = inputCount,
                OutputCount = inputCount,
                Criteria = "Loaded model snapshot",
                ExampleMatchedValues = candidates == null
                    ? new List<string>()
                    : candidates.Take(3).Select(record => record == null
                        ? string.Empty
                        : string.Join(" | ", new[]
                        {
                            record.ElementId > 0 ? "ElementId " + record.ElementId.ToString(CultureInfo.InvariantCulture) : string.Empty,
                            SafeText(record.Category),
                            SafeText(record.Family),
                            SafeText(record.Type)
                        }.Where(item => !string.IsNullOrWhiteSpace(item)))).Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
            });

            stages.Add(new FilterStageTrace
            {
                StageName = "Discipline/category prefilter",
                Description = "The engine narrowed the model to categories and discipline hints related to this requirement.",
                InputCount = inputCount,
                OutputCount = candidates?.Count ?? 0,
                Criteria = expectedCategories.Count > 0 ? string.Join(", ", expectedCategories) : "Discipline hints from requirement text",
                ExampleMatchedValues = BuildDistinctValues(candidates, record => record.Category)
            });

            stages.Add(new FilterStageTrace
            {
                StageName = "Text / family / type hint filter",
                Description = "The engine searched for the requirement intent using text, family, and type hints.",
                InputCount = inputCount,
                OutputCount = candidates?.Count ?? 0,
                Criteria = keywords.Count > 0 ? string.Join(", ", keywords) : "Requirement intent keywords",
                ExampleMatchedValues = expectedHints
            });

            stages.Add(new FilterStageTrace
            {
                StageName = "Parameter completeness check",
                Description = "The engine checked whether the expected parameters were present, empty, or matched.",
                InputCount = candidates?.Count ?? 0,
                OutputCount = parameterList.Count == 0 ? 0 : parameterList.Count,
                Criteria = parameterList.Count > 0 ? string.Join(", ", parameterList) : "No direct parameter expectations",
                ExampleMatchedValues = BuildParameterValueExamples(candidates?.ToList() ?? new List<ExportElementRecord>(), parameterList)
            });

            stages.Add(new FilterStageTrace
            {
                StageName = "Status decision",
                Description = "The deterministic engine assigned the final status from the evidence quality and rule guardrails.",
                InputCount = parameterList.Count,
                OutputCount = 1,
                Criteria = result != null ? result.StatusLabel : "Unknown",
                ExampleMatchedValues = new List<string>
                {
                    result == null ? "Unknown" : result.StatusLabel,
                    result == null ? "Unknown" : result.EvidenceAlignmentLabel
                }
            });

            return new RequirementFilterTrace
            {
                DisciplineFilter = string.IsNullOrWhiteSpace(result?.Discipline) ? SafeText(result?.Requirement?.Discipline) : result.Discipline,
                ScopeFilter = "Entire Model",
                StatusFilter = "All",
                RequirementType = SafeText(result?.RequirementType ?? semanticProfile?.RequirementType),
                RequirementTypeReason = SafeText(result?.RequirementTypeReason ?? semanticProfile?.RequirementTypeReason),
                RequirementIntent = SafeText(requirementText),
                ValidationType = result != null ? result.ValidationType.ToString() : "Unknown",
                ValidationTypeReason = SafeText(result?.ValidationTypeReason),
                RuleApplied = SafeText(result?.RuleApplied),
                RuleFamily = SafeText(result?.RuleFamily),
                TriggerKeywords = keywords,
                ExpectedEvidenceSources = expectedEvidenceSources,
                ExpectedCategories = expectedCategories,
                ExpectedFamilyTypeHints = expectedHints,
                ExpectedParameters = parameterList,
                AllowedCategories = result?.AllowedCategories ?? (semanticProfile?.AllowedCategories ?? new List<string>()),
                ExcludedCategories = result?.ExcludedCategories ?? (semanticProfile?.ExcludedCategories ?? new List<string>()),
                DirectClosingEvidence = result?.DirectClosingEvidence ?? new List<string>(),
                SupportingContext = result?.SupportingContext ?? new List<string>(),
                MissingDirectEvidence = result?.MissingDirectEvidence ?? new List<string>(),
                CandidateScopeReason = SafeText(result?.CandidateScopeReason ?? semanticProfile?.CandidateScopeReason),
                FallbackUsed = result != null && result.FallbackUsed,
                FallbackAllowed = result?.FallbackAllowed ?? (semanticProfile?.FallbackAllowed ?? false),
                CandidateScopeValid = result == null || result.CandidateScopeValid,
                FullModelFallbackUsed = result != null && result.FullModelFallbackUsed,
                ModelEvidenceSufficiency = SafeText(result?.ModelEvidenceSufficiency ?? semanticProfile?.ModelEvidenceSufficiency),
                WhyNotModelCloseable = SafeText(result?.WhyNotModelCloseable ?? semanticProfile?.WhyNotModelCloseable),
                CandidateStages = stages
            };
        }

        private static string BuildModelEvidenceLimitations(
            RequirementCheckResult result,
            IEnumerable<ExportElementRecord> candidates,
            RuleContext ruleContext,
            IEnumerable<string> expectedParameters)
        {
            List<string> missing = result?.MissingExpectedParameters ?? new List<string>();
            List<string> phrases = new List<string>();

            if (ruleContext != null && ruleContext.RequiresDirectParameterEvidence && missing.Count > 0)
            {
                phrases.Add("Direct parameters were missing: " + string.Join(", ", missing.Take(5)));
            }

            if (result != null &&
                (result.EvidenceAlignment == EvidenceAlignmentLevel.Weak ||
                 result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk ||
                 result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly))
            {
                phrases.Add("The matched evidence is " + result.EvidenceAlignmentLabel.ToLowerInvariant() + ".");
            }

            if (phrases.Count == 0)
            {
                if (result != null && result.Status != RequirementCheckStatus.Met)
                    return "The current evidence was not sufficient to close this requirement.";
                if (ruleContext != null && !ruleContext.AllowsModelOnlyMet)
                    return "Category and level evidence alone cannot close this requirement type. Direct parameter or specification evidence is needed.";
                return "No major limitations were detected in the current pass.";
            }

            return string.Join(" ", phrases);
        }

        private static string BuildConfidenceReason(
            RequirementCheckResult result,
            IEnumerable<ExportElementRecord> candidates,
            RuleContext ruleContext,
            IReadOnlyCollection<string> expectedParameters)
        {
            List<string> parts = new List<string>();
            if (result != null)
            {
                parts.Add("Evidence alignment: " + result.EvidenceAlignmentLabel);
                parts.Add("Status: " + result.StatusLabel);
            }

            if (ruleContext != null && !string.IsNullOrWhiteSpace(ruleContext.ValidationTypeReason))
            {
                parts.Add(ruleContext.ValidationTypeReason);
            }

            if (expectedParameters != null && expectedParameters.Count > 0)
            {
                int missingCount = result != null && result.MissingExpectedParameters != null
                    ? result.MissingExpectedParameters.Count
                    : 0;
                parts.Add("Expected parameters: " + string.Join(", ", expectedParameters));
                if (missingCount > 0)
                {
                    parts.Add("Missing expected parameters: " + missingCount.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (result != null && result.EvidenceAlignment == EvidenceAlignmentLevel.Strong)
            {
                parts.Add("Direct evidence and parameters were aligned.");
            }
            else if (result != null && (result.EvidenceAlignment == EvidenceAlignmentLevel.Weak || result.EvidenceAlignment == EvidenceAlignmentLevel.MismatchRisk || result.EvidenceAlignment == EvidenceAlignmentLevel.ManualOnly))
            {
                parts.Add("Confidence was reduced because the evidence was broad, mismatched, or manual-only.");
            }

            if (result != null && result.Status == RequirementCheckStatus.NeedsHumanReview)
            {
                parts.Add("Model evidence is supporting context only. Direct closing evidence is missing or requires drawing/specification/field review.");
            }

            if (result != null && result.HumanReviewNeeded)
            {
                parts.Add("Human review is required for closure.");
            }

            return string.Join(" ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string DescribeExpectedMeaning(string parameterName)
        {
            switch (Normalize(parameterName))
            {
                case "voltage":
                    return "Outlet or equipment voltage";
                case "panel":
                case "panel name":
                    return "Connected panel";
                case "circuit":
                case "circuit number":
                    return "Circuit assignment";
                case "load name":
                    return "Connected load label";
                case "room":
                    return "Room context";
                case "space":
                    return "Space context";
                case "level":
                    return "Model level assignment";
                case "manufacturer":
                    return "Manufacturer or product source";
                case "tag":
                    return "Identification tag";
                case "label":
                    return "Identification label";
                case "nameplate":
                    return "Nameplate or marking";
                case "mark":
                case "identification":
                    return "Identification / marking evidence";
                case "phase created":
                case "phase demolished":
                case "demo status":
                case "status":
                    return "Phase or demolition status";
                case "ground conductor":
                case "conductor type":
                    return "Conductor or grounding evidence";
                default:
                    return "Expected requirement parameter";
            }
        }

        private static string DescribeExpectedPattern(string parameterName)
        {
            switch (Normalize(parameterName))
            {
                case "voltage":
                    return "120V / 120 V / 277 V / populated";
                case "panel":
                case "panel name":
                    return "non-empty panel name";
                case "circuit":
                case "circuit number":
                    return "non-empty circuit number";
                case "load name":
                    return "non-empty load name";
                case "room":
                case "space":
                    return "non-empty room or space reference";
                case "level":
                    return "LEVEL 01 / assigned level";
                case "manufacturer":
                    return "manufacturer name or approved manufacturer";
                case "tag":
                case "label":
                case "nameplate":
                case "mark":
                case "identification":
                    return "non-empty identification value";
                case "phase created":
                case "phase demolished":
                case "demo status":
                case "status":
                    return "phase or demolition status value";
                case "ground conductor":
                case "conductor type":
                    return "conductor or grounding value";
                default:
                    return "non-empty value";
            }
        }

        private static string BuildParameterFailureReason(string parameterName, bool isPresent, bool isEmpty, bool isMatch)
        {
            string label = parameterName ?? "parameter";
            if (!isPresent)
            {
                return label + " was not found in the current export.";
            }

            if (isEmpty)
            {
                return label + " exists in the export but has no value.";
            }

            if (!isMatch)
            {
                return label + " exists, but the value does not satisfy the requirement.";
            }

            return label + " satisfies the requirement.";
        }

        private static string GetParameterSource(ExportElementRecord record, string parameterName)
        {
            if (record == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return "not in export";
            }

            if (record.InstanceParameters != null &&
                record.InstanceParameters.ContainsKey(parameterName))
            {
                return "instance parameter";
            }

            if (record.TypeParameters != null &&
                record.TypeParameters.ContainsKey(parameterName))
            {
                return "type parameter";
            }

            return "not in export";
        }

        private static bool HasParameter(ExportElementRecord record, string parameterName)
        {
            if (record == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            return (record.InstanceParameters != null && record.InstanceParameters.ContainsKey(parameterName)) ||
                   (record.TypeParameters != null && record.TypeParameters.ContainsKey(parameterName));
        }

        private static List<MissingEvidenceDetail> BuildMissingEvidenceDetails(
            List<ExportElementRecord> candidates,
            List<string> expectedParameters)
        {
            var details = new List<MissingEvidenceDetail>();
            if (candidates == null || candidates.Count == 0 || expectedParameters == null || expectedParameters.Count == 0)
                return details;

            foreach (string paramName in expectedParameters)
            {
                bool anyHasParam = false;
                bool anyHasValue = false;

                foreach (var record in candidates.Take(20))
                {
                    if (record == null) continue;
                    bool foundInInstance = record.InstanceParameters != null && record.InstanceParameters.ContainsKey(paramName);
                    bool foundInType = record.TypeParameters != null && record.TypeParameters.ContainsKey(paramName);
                    if (foundInInstance || foundInType)
                    {
                        anyHasParam = true;
                        string value = GetParameterValue(record, paramName);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            anyHasValue = true;
                            break;
                        }
                    }
                }

                if (anyHasValue)
                    continue;

                MissingEvidenceReason reason;
                if (!anyHasParam)
                    reason = MissingEvidenceReason.NotInExport;
                else
                    reason = MissingEvidenceReason.EmptyValue;

                details.Add(new MissingEvidenceDetail
                {
                    ParameterName = paramName,
                    Reason = reason
                });
            }

            return details;
        }

        private static List<string> FindMatchingKeywords(string text, string[] keywords)
        {
            var matched = new List<string>();
            if (string.IsNullOrWhiteSpace(text) || keywords == null) return matched;
            foreach (string kw in keywords)
            {
                if (!string.IsNullOrWhiteSpace(kw) && text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    matched.Add(kw);
            }
            return matched;
        }

        private static List<ExportElementRecord> NarrowHighRiskCandidates(List<ExportElementRecord> candidates, RuleContext ruleContext)
        {
            if (candidates == null || candidates.Count == 0 || ruleContext == null || !HighPrioritySemanticTypes.Contains(ruleContext.RequirementType))
            {
                return candidates ?? new List<ExportElementRecord>();
            }

            List<string> hints = ruleContext.ExpectedFamilyTypeHints == null
                ? new List<string>()
                : ruleContext.ExpectedFamilyTypeHints.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();

            List<string> directParameters = (ruleContext.ExpectedParameters ?? new List<string>())
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter))
                .Where(parameter => !IsBroadPlacementParameter(parameter))
                .ToList();

            if (hints.Count == 0 && directParameters.Count == 0)
            {
                return candidates;
            }

            List<ExportElementRecord> narrowed = candidates
                .Where(record => MatchesFamilyTypeHint(record, hints) || HasAnyParameter(record, directParameters))
                .ToList();

            return narrowed.Count > 0 ? narrowed : new List<ExportElementRecord>();
        }

        private static bool MatchesFamilyTypeHint(ExportElementRecord record, IEnumerable<string> hints)
        {
            if (record == null || hints == null)
            {
                return false;
            }

            string blob = Normalize(string.Join(" ", new[] { record.Name, record.Family, record.Type }));
            foreach (string hint in hints)
            {
                string normalized = Normalize(hint);
                if (!string.IsNullOrWhiteSpace(normalized) && blob.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyParameter(ExportElementRecord record, IEnumerable<string> parameters)
        {
            if (record == null || parameters == null)
            {
                return false;
            }

            return parameters.Any(parameter => HasParameter(record, parameter) || HasNonEmptyParameter(record, parameter));
        }

        private static bool IsBroadPlacementParameter(string parameter)
        {
            string normalized = Normalize(parameter);
            return normalized == "level" ||
                normalized == "elevation" ||
                normalized == "location" ||
                normalized == "room" ||
                normalized == "space" ||
                normalized == "comments" ||
                normalized == "description" ||
                normalized == "system type";
        }

        private static bool ContainsMechanicalSignals(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (ContainsAny(text, new[] { "airflow", "hvac", "mechanical equipment", "pump", "chiller", "coil", "fan" }))
            {
                return true;
            }

            return ContainsAnyWord(text, new[] { "duct", "ductwork" });
        }

        private static RequirementCheckResult BuildResult(
            OwnerRequirementRow requirement,
            RequirementCheckStatus status,
            double confidence,
            string issueTitle,
            string reasoning,
            string nextBestAction,
            string evidence,
            string evidenceSummary,
            string responsibleRole,
            string sourceWorksheet,
            int sourceRow,
            IEnumerable<ExportElementRecord> matchedElements = null,
            IEnumerable<string> expectedParameters = null)
        {
            return BuildResult(
                requirement,
                status,
                confidence,
                issueTitle,
                reasoning,
                nextBestAction,
                new[] { evidence },
                evidenceSummary,
                responsibleRole,
                sourceWorksheet,
                sourceRow,
                matchedElements,
                expectedParameters);
        }

        private static RequirementCheckResult BuildResult(
            OwnerRequirementRow requirement,
            RequirementCheckStatus status,
            double confidence,
            string issueTitle,
            string reasoning,
            string nextBestAction,
            IEnumerable<string> evidence,
            string evidenceSummary,
            string responsibleRole,
            string sourceWorksheet,
            int sourceRow,
            IEnumerable<ExportElementRecord> matchedElements = null,
            IEnumerable<string> expectedParameters = null)
        {
            List<string> evidenceList = evidence == null
                ? new List<string>()
                : evidence.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();

            string sourceSheet = sourceWorksheet;
            if (string.IsNullOrWhiteSpace(sourceSheet) && requirement != null)
            {
                sourceSheet = requirement.SourceSheet;
            }

            int resolvedSourceRow = sourceRow > 0
                ? sourceRow
                : requirement != null
                    ? requirement.RowNumber
                    : 0;

            List<ExportElementRecord> matchedRecords = matchedElements == null
                ? new List<ExportElementRecord>()
                : matchedElements.Where(item => item != null).ToList();

            List<MatchedElementEvidence> matchedElementEvidence = BuildMatchedElementEvidence(matchedRecords, expectedParameters);
            List<long> matchedIds = matchedRecords
                .Select(item => item.ElementId)
                .Where(value => value > 0)
                .Distinct()
                .ToList();
            List<string> matchedUniqueIds = matchedRecords
                .Select(item => item.UniqueId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> matchedSummaries = matchedElementEvidence
                .Select(BuildMatchedElementSummary)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            if (matchedSummaries.Count == 0 && matchedRecords.Count > 0)
            {
                matchedSummaries = matchedRecords
                    .Take(10)
                    .Select(item => item.ElementId > 0
                        ? "ElementId " + item.ElementId.ToString(CultureInfo.InvariantCulture)
                        : string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();
            }

            return new RequirementCheckResult
            {
                Requirement = requirement,
                Status = status,
                Confidence = confidence,
                IssueTitle = string.IsNullOrWhiteSpace(issueTitle) ? BuildIssueTitle(requirement, status, string.Empty, RequirementDiscipline.All) : issueTitle,
                Reasoning = string.IsNullOrWhiteSpace(reasoning) ? string.Empty : reasoning.Trim(),
                NextBestAction = string.IsNullOrWhiteSpace(nextBestAction) ? string.Empty : nextBestAction.Trim(),
                ResponsibleRole = string.IsNullOrWhiteSpace(responsibleRole)
                    ? BuildResponsibleRole(requirement, RequirementDiscipline.All, string.Empty)
                    : responsibleRole.Trim(),
                EvidenceSummary = string.IsNullOrWhiteSpace(evidenceSummary)
                    ? BuildEvidenceSummary(evidenceList)
                    : evidenceSummary.Trim(),
                RequirementId = requirement != null ? requirement.RequirementId : string.Empty,
                RequirementText = requirement != null ? requirement.RequirementText : string.Empty,
                Discipline = requirement != null ? requirement.Discipline : string.Empty,
                SourceFile = requirement != null ? requirement.SourceFile : string.Empty,
                SourceWorksheet = sourceSheet,
                SourceRow = resolvedSourceRow,
                Evidence = evidenceList,
                MatchedModelElementCount = matchedRecords.Count > 0 ? matchedRecords.Count : evidenceList.Count,
                MatchedElementIds = matchedIds,
                MatchedUniqueIds = matchedUniqueIds,
                MatchedElementSummaries = matchedSummaries,
                MatchedElements = matchedElementEvidence,
                ElementIdCopyText = string.Join(";", matchedIds.Select(id => id.ToString(CultureInfo.InvariantCulture))),
                Urgency = BuildUrgencyLabel(status),
                IsKeyIssue = status != RequirementCheckStatus.Met && status != RequirementCheckStatus.NotApplicable,
                StatusReason = string.IsNullOrWhiteSpace(reasoning) ? string.Empty : reasoning.Trim(),
                HumanReviewNeeded = status == RequirementCheckStatus.NeedsHumanReview
            };
        }

        private static string BuildUrgencyLabel(RequirementCheckStatus status)
        {
            switch (status)
            {
                case RequirementCheckStatus.NotMet:
                    return "Critical";
                case RequirementCheckStatus.InsufficientModelData:
                    return "High";
                case RequirementCheckStatus.NeedsHumanReview:
                    return "Needs Review";
                case RequirementCheckStatus.NotApplicable:
                    return "Low";
                default:
                    return "Low";
            }
        }

        private static string BuildIssueTitle(
            OwnerRequirementRow requirement,
            RequirementCheckStatus status,
            string requirementText,
            RequirementDiscipline selectedDiscipline)
        {
            string trimmed = NormalizeTitle(requirementText);

            if (status == RequirementCheckStatus.NotApplicable)
            {
                return "Outside focused discipline";
            }

            if (ContainsAny(trimmed, new[] { "panel", "circuit", "breaker", "supply from" }))
            {
                return "Panel and circuit assignment";
            }

            if (ContainsAny(trimmed, new[] { "outlet", "receptacle", "duplex", "general purpose circuit", "120v" }))
            {
                return "Outlet and circuit assignment";
            }

            if (ContainsAny(trimmed, new[] { "voltage", "apparent load", "connected load", "load" }))
            {
                return "Electrical load metadata";
            }

            if (ContainsAny(trimmed, new[] { "lighting", "light fixture", "fixture", "illumination" }))
            {
                return "Lighting fixture coverage";
            }

            if (ContainsAny(trimmed, new[] { "level", "elevation" }))
            {
                return "Level assignment";
            }

            if (ContainsAny(trimmed, new[] { "identification", "identify", "label", "labeling", "nameplate", "tag", "manufacturer", "submittal" }))
            {
                return "Identification, labeling, and manufacturer requirements";
            }

            if (ContainsAny(trimmed, new[] { "protect", "protection", "clean", "demolition", "remove", "relocate", "disconnect", "salvage", "grounding", "conduit", "conductor" }))
            {
                return "Field execution, demolition, protection, or grounding requirement";
            }

            if (ContainsMechanicalSignals(trimmed))
            {
                return "Mechanical equipment placement";
            }

            if (ContainsAny(trimmed, new[] { "pipe", "plumbing", "drain", "vent", "water", "fixture", "sanitary", "waste", "slope" }))
            {
                return "Plumbing routing and fixture coverage";
            }

            if (ContainsAny(trimmed, new[] { "data", "communication", "fire alarm", "security", "telephone", "nurse call", "low voltage", "device" }))
            {
                return "Technology / low-voltage coverage";
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                return TruncateToWords(trimmed, 10);
            }

            if (selectedDiscipline != RequirementDiscipline.All)
            {
                return selectedDiscipline + " requirement";
            }

            return requirement != null && !string.IsNullOrWhiteSpace(requirement.RequirementId)
                ? requirement.RequirementId
                : "Requirement";
        }

        private static string BuildResponsibleRole(
            OwnerRequirementRow requirement,
            RequirementDiscipline selectedDiscipline,
            string requirementText)
        {
            RequirementDiscipline explicitDiscipline = RequirementDisciplineNormalizer.Parse(
                requirement != null ? requirement.Discipline : null,
                RequirementDiscipline.All);

            if (explicitDiscipline != RequirementDiscipline.All)
            {
                return explicitDiscipline.ToString();
            }

            RequirementDiscipline inferredDiscipline = InferDisciplineFromText(requirementText);
            if (inferredDiscipline != RequirementDiscipline.All)
            {
                return inferredDiscipline.ToString();
            }

            if (selectedDiscipline != RequirementDiscipline.All)
            {
                return selectedDiscipline.ToString();
            }

            return "Unknown / Needs Classification";
        }

        private static RequirementDiscipline InferDisciplineFromText(string requirementText)
        {
            string text = Normalize(requirementText);

            if (ContainsAny(text, new[] { "lighting", "light fixture", "fixture", "illumination" }))
            {
                return RequirementDiscipline.Lighting;
            }

            if (ContainsAny(text, new[] { "panel", "circuit", "voltage", "load", "electrical", "breaker" }))
            {
                return RequirementDiscipline.Electrical;
            }

            if (ContainsMechanicalSignals(text))
            {
                return RequirementDiscipline.Mechanical;
            }

            if (ContainsAny(text, new[] { "plumbing", "pipe", "drain", "vent", "water", "sanitary" }))
            {
                return RequirementDiscipline.Plumbing;
            }

            if (ContainsAny(text, new[] { "technology", "data", "communication", "security", "fire alarm", "telephone", "low voltage" }))
            {
                return RequirementDiscipline.Technology;
            }

            return RequirementDiscipline.All;
        }

        private static string BuildReasoningForNotApplicable(
            string requirementDiscipline,
            RequirementDiscipline selectedDiscipline,
            string requirementText)
        {
            string explicitDiscipline = BuildDisciplineLabel(requirementDiscipline);
            string inferredDiscipline = BuildDisciplineLabel(InferDisciplineFromText(requirementText).ToString());

            if (string.IsNullOrWhiteSpace(explicitDiscipline) || explicitDiscipline == "All")
            {
                explicitDiscipline = inferredDiscipline;
            }

            if (string.IsNullOrWhiteSpace(explicitDiscipline) || explicitDiscipline == "All")
            {
                explicitDiscipline = "another discipline";
            }

            return "The requirement appears to apply to " + explicitDiscipline + ", while the selected discipline is " + selectedDiscipline + ", so it was excluded from the focused check.";
        }

        private static string BuildReasoningForInsufficientData(
            string issueTitle,
            string requirementText,
            RequirementDiscipline selectedDiscipline)
        {
            string focus = BuildFocusLabel(issueTitle, requirementText, selectedDiscipline);
            return "The model snapshot did not contain enough evidence for " + focus + ", so the item is marked as Insufficient Model Data.";
        }

        private static string BuildReasoningForNotMet(
            string issueTitle,
            string requirementText,
            string evidenceNarrative,
            RequirementDiscipline selectedDiscipline,
            IReadOnlyCollection<string> evidence,
            int candidateCount,
            int satisfiedCount)
        {
            string focus = BuildFocusLabel(issueTitle, requirementText, selectedDiscipline);
            string evidenceText = BuildEvidenceSummary(evidence);

            if (satisfiedCount == 0)
            {
                return "This requirement depends on " + focus + ". " + evidenceNarrative + " " + evidenceText + " The expected values were not present, so it is marked as Not Met.";
            }

            return "This requirement depends on " + focus + ". " + evidenceNarrative + " " + evidenceText + " Some candidate elements still need attention, so it is marked as Not Met.";
        }

        private static string BuildReasoningForPartialCoverage(
            string issueTitle,
            string requirementText,
            string evidenceNarrative,
            RequirementDiscipline selectedDiscipline,
            IReadOnlyCollection<string> evidence,
            int candidateCount,
            int satisfiedCount)
        {
            string focus = BuildFocusLabel(issueTitle, requirementText, selectedDiscipline);
            string evidenceText = BuildEvidenceSummary(evidence);
            return "This requirement depends on " + focus + ". " + evidenceNarrative + " " + evidenceText + " Some, but not all, candidate elements are populated, so human review is still needed.";
        }

        private static string BuildReasoningForMet(
            string issueTitle,
            string requirementText,
            string evidenceNarrative,
            RequirementDiscipline selectedDiscipline,
            IReadOnlyCollection<string> evidence,
            int candidateCount)
        {
            string focus = BuildFocusLabel(issueTitle, requirementText, selectedDiscipline);
            string evidenceText = BuildEvidenceSummary(evidence);
            return "This requirement depends on " + focus + ". " + evidenceNarrative + " " + evidenceText + " The deterministic check found direct closing evidence, so it is marked as Met.";
        }

        private static string BuildReasoningForOpenEndedRequirement(
            string requirementText,
            RequirementDiscipline selectedDiscipline)
        {
            string focus = BuildFocusLabel(string.Empty, requirementText, selectedDiscipline);
            return "The requirement is open-ended or cross-disciplinary, and the deterministic model evidence is not specific enough to confirm " + focus + " without human judgment.";
        }

        private static string BuildNextBestAction(
            RequirementCheckStatus status,
            string requirementText,
            RequirementDiscipline selectedDiscipline,
            string responsibleRole,
            string focusLabel)
        {
            string focus = BuildFocusLabel(focusLabel, requirementText, selectedDiscipline);
            string role = string.IsNullOrWhiteSpace(responsibleRole) ? "the selected discipline" : responsibleRole;

            switch (status)
            {
                case RequirementCheckStatus.NotMet:
                    if (ContainsAny(Normalize(requirementText), new[] { "outlet", "receptacle", "duplex", "general purpose circuit", "120v" }))
                    {
                        return "Verify the outlet voltage, panel, circuit, room, and load context in Revit or panel schedules before closing this item.";
                    }

                    if (ContainsAny(Normalize(requirementText), new[] { "panel", "circuit", "breaker", "supply from" }))
                    {
                        return "Assign panel and circuit values to the listed elements before the next deliverable.";
                    }

                    if (ContainsAny(Normalize(requirementText), new[] { "voltage", "apparent load", "connected load", "load" }))
                    {
                        return "Populate the electrical load parameters in Revit, then rerun the check.";
                    }

                    if (ContainsAny(Normalize(requirementText), new[] { "lighting", "light fixture", "fixture", "illumination" }))
                    {
                        return "Verify the lighting fixture records and connection data, then rerun the check.";
                    }

                    if (ContainsAny(Normalize(requirementText), new[] { "p-trap", "ptrap", "clevis hanger", "clevis", "pipe hanger", "pipe support", "hanger rod", "seismic hanger", "trapeze" }))
                    {
                        return "Review the hanger detail or plumbing specification, then populate hanger type, spacing, and comments before rerunning the check.";
                    }

                    if (ContainsAny(Normalize(requirementText), new[] { "level", "elevation" }))
                    {
                        return "Assign the correct level values to the affected elements, then rerun the check.";
                    }

                    if (ContainsAny(Normalize(requirementText), new[] { "identification", "identify", "label", "labeling", "nameplate", "tag", "manufacturer", "submittal" }))
                    {
                        return "Review the specification or submittal, then export or populate the identification and manufacturer parameters if the requirement should be model-checkable.";
                    }

                    if (ContainsAny(Normalize(requirementText), new[] { "protect", "protection", "clean", "demolition", "remove", "relocate", "disconnect", "salvage", "grounding", "conduit", "conductor" }))
                    {
                        return "Review the drawings, specifications, and field execution evidence before closing this item.";
                    }

                    return "Add or verify the missing parameter values in Revit, then rerun the check.";

                case RequirementCheckStatus.NeedsHumanReview:
                    if (ContainsAny(Normalize(requirementText), new[] { "identification", "identify", "label", "labeling", "nameplate", "tag", "manufacturer", "submittal" }))
                    {
                        return "Review the specification or submittal because the requirement depends on identification, labeling, or manufacturer evidence.";
                    }

                    if (ContainsAny(Normalize(requirementText), new[] { "protect", "protection", "clean", "demolition", "remove", "relocate", "disconnect", "salvage", "grounding", "conduit", "conductor" }))
                    {
                        return "Review the drawings, specifications, and field execution evidence because the requirement depends on non-model verification.";
                    }

                    return "Review this requirement manually because it depends on non-model evidence or broader specification judgment.";

                case RequirementCheckStatus.InsufficientModelData:
                    return "Review this requirement against the relevant specification because the model does not contain enough evidence.";

                case RequirementCheckStatus.NotApplicable:
                    return "Confirm whether this owner requirement applies to " + role + " before excluding it from the focused check.";

                default:
                    return "No action required for this requirement.";
            }
        }

        private static string BuildEvidenceSummary(IEnumerable<string> evidence)
        {
            List<string> items = evidence == null
                ? new List<string>()
                : evidence.Where(item => !string.IsNullOrWhiteSpace(item)).Take(3).ToList();

            if (items.Count == 0)
            {
                return "No evidence captured.";
            }

            return string.Join(" ", items);
        }

        private static List<MatchedElementEvidence> BuildMatchedElementEvidence(
            IEnumerable<ExportElementRecord> records,
            IEnumerable<string> expectedParameters)
        {
            List<MatchedElementEvidence> evidence = new List<MatchedElementEvidence>();
            if (records == null)
            {
                return evidence;
            }

            List<string> expected = expectedParameters == null
                ? new List<string>()
                : expectedParameters.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            foreach (ExportElementRecord record in records.Where(item => item != null).Take(10))
            {
                List<string> matchedParameters = new List<string>();
                List<string> missingParameters = new List<string>();

                if (expected.Count > 0)
                {
                    foreach (string parameterName in expected)
                    {
                        if (HasNonEmptyParameter(record, parameterName))
                        {
                            matchedParameters.Add(parameterName);
                        }
                        else
                        {
                            missingParameters.Add(parameterName);
                        }
                    }
                }
                else
                {
                    AddNonEmptyParameterNames(matchedParameters, record.InstanceParameters);
                    AddNonEmptyParameterNames(matchedParameters, record.TypeParameters);
                }

                var paramValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string pName in matchedParameters)
                {
                    string val = GetParameterValue(record, pName);
                    if (!string.IsNullOrWhiteSpace(val))
                        paramValues[pName] = val;
                }

                List<string> parameterValueExamples = paramValues
                    .Select(pair => pair.Key + " = " + pair.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();

                List<ParameterCheckResult> parameterChecks = new List<ParameterCheckResult>();
                foreach (string expectedParameter in expected)
                {
                    string actualValue = GetParameterValue(record, expectedParameter);
                    bool present = HasParameter(record, expectedParameter);
                    bool match = !string.IsNullOrWhiteSpace(actualValue);
                    parameterChecks.Add(new ParameterCheckResult
                    {
                        ParameterName = expectedParameter,
                        ExpectedMeaning = DescribeExpectedMeaning(expectedParameter),
                        ExpectedValuePattern = DescribeExpectedPattern(expectedParameter),
                        ActualValue = string.IsNullOrWhiteSpace(actualValue) ? (present ? "(empty)" : "(not found)") : actualValue,
                        Source = GetParameterSource(record, expectedParameter),
                        IsPresent = present,
                        IsEmpty = present && string.IsNullOrWhiteSpace(actualValue),
                        IsMatch = match,
                        IsRequired = true,
                        FailureReason = BuildParameterFailureReason(expectedParameter, present, present && string.IsNullOrWhiteSpace(actualValue), match)
                    });
                }

                evidence.Add(new MatchedElementEvidence
                {
                    ElementId = record.ElementId > 0 ? record.ElementId.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    UniqueId = record.UniqueId ?? string.Empty,
                    Category = record.Category ?? string.Empty,
                    Family = record.Family ?? string.Empty,
                    Type = record.Type ?? string.Empty,
                    Level = record.Level ?? string.Empty,
                    MatchedParameters = matchedParameters.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    MissingParameters = missingParameters.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    EvidenceReason = BuildMatchedElementReason(record, matchedParameters, missingParameters),
                    ParameterValues = paramValues,
                    ParameterValueExamples = parameterValueExamples,
                    ParameterChecks = parameterChecks
                });
            }

            return evidence;
        }

        private static string BuildMatchedElementSummary(MatchedElementEvidence evidence)
        {
            if (evidence == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(evidence.ElementId))
            {
                parts.Add("ElementId " + evidence.ElementId);
            }

            if (!string.IsNullOrWhiteSpace(evidence.Category))
            {
                parts.Add(evidence.Category);
            }

            if (!string.IsNullOrWhiteSpace(evidence.Family))
            {
                parts.Add(evidence.Family);
            }

            return string.Join(" | ", parts);
        }

        private static void AddNonEmptyParameterNames(List<string> target, IReadOnlyDictionary<string, ParameterRecord> parameters)
        {
            if (target == null || parameters == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ParameterRecord> pair in parameters)
            {
                if (pair.Value != null && (!string.IsNullOrWhiteSpace(pair.Value.ValueString) || !string.IsNullOrWhiteSpace(pair.Value.RawValue)))
                {
                    target.Add(pair.Key);
                }
            }
        }

        private static string BuildMatchedElementReason(
            ExportElementRecord record,
            IReadOnlyCollection<string> matchedParameters,
            IReadOnlyCollection<string> missingParameters)
        {
            List<string> parts = new List<string>();

            if (record != null)
            {
                if (!string.IsNullOrWhiteSpace(record.Category))
                {
                    parts.Add("Category matched: " + record.Category);
                }

                if (!string.IsNullOrWhiteSpace(record.Family))
                {
                    parts.Add("Family: " + record.Family);
                }

                if (!string.IsNullOrWhiteSpace(record.Type))
                {
                    parts.Add("Type: " + record.Type);
                }
            }

            if (matchedParameters != null && matchedParameters.Count > 0)
            {
                parts.Add("Matched parameters: " + string.Join(", ", matchedParameters));
            }

            if (missingParameters != null && missingParameters.Count > 0)
            {
                parts.Add("Missing parameters: " + string.Join(", ", missingParameters));
            }

            return parts.Count == 0 ? "Matched by deterministic evidence search." : string.Join(". ", parts) + ".";
        }

        private static string BuildFocusLabel(
            string issueTitle,
            string requirementText,
            RequirementDiscipline selectedDiscipline)
        {
            if (!string.IsNullOrWhiteSpace(issueTitle))
            {
                return issueTitle.ToLowerInvariant();
            }

            RequirementDiscipline inferred = InferDisciplineFromText(requirementText);
            if (inferred != RequirementDiscipline.All)
            {
                return inferred.ToString().ToLowerInvariant() + " evidence";
            }

            if (selectedDiscipline != RequirementDiscipline.All)
            {
                return selectedDiscipline.ToString().ToLowerInvariant() + " evidence";
            }

            return "the available model evidence";
        }

        private static string BuildDisciplineLabel(string discipline)
        {
            if (string.IsNullOrWhiteSpace(discipline))
            {
                return string.Empty;
            }

            RequirementDiscipline parsed = RequirementDisciplineNormalizer.Parse(discipline, RequirementDiscipline.All);
            return parsed == RequirementDiscipline.All ? discipline : parsed.ToString();
        }

        private static string NormalizeTitle(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string TruncateToWords(string value, int wordCount)
        {
            if (string.IsNullOrWhiteSpace(value) || wordCount <= 0)
            {
                return string.Empty;
            }

            string[] words = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= wordCount)
            {
                return value;
            }

            return string.Join(" ", words.Take(wordCount)) + "...";
        }

        private static bool MatchesDiscipline(string requirementDiscipline, RequirementDiscipline selectedDiscipline)
        {
            return RequirementDisciplineNormalizer.Matches(selectedDiscipline, requirementDiscipline);
        }

        private static RuleContext BuildRuleContextFromSemanticProfile(RequirementSemanticProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            return new RuleContext
            {
                RuleName = profile.RuleApplied,
                RuleFamily = profile.RuleFamily,
                RequirementType = profile.RequirementType,
                RequirementTypeReason = profile.RequirementTypeReason,
                ValidationType = profile.ValidationType,
                ValidationTypeReason = profile.ValidationTypeReason,
                TriggerKeywords = profile.TriggerKeywords ?? new List<string>(),
                ExpectedEvidenceSources = profile.ExpectedEvidenceSources ?? new List<string>(),
                ExpectedFamilyTypeHints = profile.ExpectedFamilyTypeHints ?? new List<string>(),
                ExpectedParameters = profile.ExpectedParameters ?? new List<string>(),
                ExpectedCategories = profile.AllowedCategories == null ? null : profile.AllowedCategories.ToArray(),
                AllowedCategories = profile.AllowedCategories == null ? new List<string>() : profile.AllowedCategories.ToList(),
                ExcludedCategories = profile.ExcludedCategories == null ? new List<string>() : profile.ExcludedCategories.ToList(),
                DirectClosingEvidence = profile.DirectClosingEvidence == null ? new List<string>() : profile.DirectClosingEvidence.ToList(),
                SupportingContext = profile.SupportingContext == null ? new List<string>() : profile.SupportingContext.ToList(),
                MissingDirectEvidence = profile.MissingDirectEvidence == null ? new List<string>() : profile.MissingDirectEvidence.ToList(),
                CandidateScopeReason = profile.CandidateScopeReason,
                FallbackAllowed = profile.FallbackAllowed,
                ModelEvidenceSufficiency = profile.ModelEvidenceSufficiency,
                WhyNotModelCloseable = profile.WhyNotModelCloseable,
                RequiresDirectParameterEvidence = profile.RequiresDirectParameterEvidence,
                AllowsModelOnlyMet = profile.AllowsModelOnlyMet,
                WeakEvidenceIfOnlyCategoryLevel = true
            };
        }

        private static void ApplySemanticMetadata(
            RequirementCheckResult result,
            RequirementSemanticProfile profile)
        {
            if (result == null || profile == null)
            {
                return;
            }

            result.RequirementType = profile.RequirementType;
            result.RequirementTypeReason = profile.RequirementTypeReason;
            result.RuleApplied = profile.RuleApplied;
            result.RuleFamily = profile.RuleFamily;
            result.ValidationType = profile.ValidationType;
            result.ValidationTypeReason = profile.ValidationTypeReason;

            result.ExpectedEvidenceSources = profile.ExpectedEvidenceSources == null
                ? new List<string>()
                : profile.ExpectedEvidenceSources.ToList();
            result.ExpectedCategories = profile.AllowedCategories == null
                ? new List<string>()
                : profile.AllowedCategories.ToList();
            result.ExpectedFamilyTypeHints = profile.ExpectedFamilyTypeHints == null
                ? new List<string>()
                : profile.ExpectedFamilyTypeHints.ToList();
            result.ExpectedParameters = profile.ExpectedParameters == null
                ? new List<string>()
                : profile.ExpectedParameters.ToList();
            result.DirectClosingEvidence = profile.DirectClosingEvidence == null
                ? new List<string>()
                : profile.DirectClosingEvidence.ToList();
            result.SupportingContext = profile.SupportingContext == null
                ? new List<string>()
                : profile.SupportingContext.ToList();
            result.MissingDirectEvidence = profile.MissingDirectEvidence == null
                ? new List<string>()
                : profile.MissingDirectEvidence.ToList();

            if (result.FilterTrace != null)
            {
                result.FilterTrace.RequirementType = profile.RequirementType;
                result.FilterTrace.RequirementTypeReason = profile.RequirementTypeReason;
                result.FilterTrace.CandidateScopeReason = profile.CandidateScopeReason;
                result.FilterTrace.AllowedCategories = profile.AllowedCategories == null
                    ? new List<string>()
                    : profile.AllowedCategories.ToList();
                result.FilterTrace.ExcludedCategories = profile.ExcludedCategories == null
                    ? new List<string>()
                    : profile.ExcludedCategories.ToList();
                result.FilterTrace.FallbackAllowed = profile.FallbackAllowed;
                result.FilterTrace.ModelEvidenceSufficiency = profile.ModelEvidenceSufficiency;
                result.FilterTrace.WhyNotModelCloseable = profile.WhyNotModelCloseable;
                result.FilterTrace.DirectClosingEvidence = profile.DirectClosingEvidence == null
                    ? new List<string>()
                    : profile.DirectClosingEvidence.ToList();
                result.FilterTrace.SupportingContext = profile.SupportingContext == null
                    ? new List<string>()
                    : profile.SupportingContext.ToList();
                result.FilterTrace.MissingDirectEvidence = profile.MissingDirectEvidence == null
                    ? new List<string>()
                    : profile.MissingDirectEvidence.ToList();
            }

            result.CandidateScopeReason = profile.CandidateScopeReason;
            result.AllowedCategories = profile.AllowedCategories == null
                ? new List<string>()
                : profile.AllowedCategories.ToList();
            result.ExcludedCategories = profile.ExcludedCategories == null
                ? new List<string>()
                : profile.ExcludedCategories.ToList();
            result.FallbackAllowed = profile.FallbackAllowed;
            result.ModelEvidenceSufficiency = profile.ModelEvidenceSufficiency;
            result.WhyNotModelCloseable = profile.WhyNotModelCloseable;
            result.FullModelFallbackUsed = result.FallbackUsed;
            result.CandidateScopeValid = !result.FullModelFallbackUsed &&
                (result.MatchedModelElementCount == 0 || result.AllowedCategories.Count == 0 || result.MatchedModelElementCount < 20000);
        }

        private bool TryEvaluateHighPrioritySemanticRequirement(
            OwnerRequirementRow requirement,
            IReadOnlyCollection<ExportElementRecord> modelRecords,
            RequirementDiscipline selectedDiscipline,
            string requirementText,
            EvidenceIndex evidenceIndex,
            RequirementSemanticProfile semanticProfile,
            out RequirementCheckResult result)
        {
            result = null;

            if (semanticProfile == null || !HighPrioritySemanticTypes.Contains(semanticProfile.RequirementType))
            {
                return false;
            }

            RuleContext ruleContext = BuildRuleContextFromSemanticProfile(semanticProfile);
            List<string> scopedCategories = semanticProfile.AllowedCategories ?? new List<string>();
            bool fallbackAllowed = semanticProfile.FallbackAllowed;

            switch (semanticProfile.RequirementType)
            {
                case "grounding_bonding_conductors":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Grounding and bonding conductors",
                        "The requirement is about grounding or bonding evidence rather than generic device coverage.",
                        "Verify grounding conductors, ground bars, bonding jumpers, and specification details before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "conduit_raceway_size_requirement":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Conduit size requirement",
                        "The requirement is about conduit size, minimums, or maximums and needs direct conduit evidence.",
                        "Review conduit detail notes or specification limits before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "flexible_conduit_length_requirement":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Flexible conduit length requirement",
                        "The requirement is about flexible conduit length limits and needs direct conduit evidence.",
                        "Review the conduit detail or specification limit before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "conduit_raceway_presence":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Conduit and raceway presence",
                        "The requirement is about conduit or raceway presence and direct conduit elements are required.",
                        "Review conduit/raceway model elements and any supporting drawings before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "dimension_clearance_distance_separation":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Clearance, distance, or separation constraint",
                        "The requirement is about dimensional clearances or separation and needs measured evidence.",
                        "Review the drawing detail or measured clearance before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "manufacturer_brand_restriction":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Manufacturer or brand restriction",
                        "The requirement is about manufacturer or brand restrictions and needs direct product or submittal evidence.",
                        "Review the specification or submittal before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "owner_standard_product_constraint":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Owner standard product constraint",
                        "The requirement is about owner-standard product constraints and needs direct product evidence.",
                        "Review the owner standard or product submittal before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "plumbing_hose_bibb_rpz_valves":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Plumbing hose bibb, RPZ, and valve coordination",
                        "The requirement is about hose bibbs, RPZ/backflow, or valve coordination and must stay within plumbing evidence scope.",
                        "Verify plumbing fixture, fitting, and valve evidence or review the drawings if the export lacks direct plumbing candidates.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "plumbing_flush_valve_product_spec":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Plumbing flush valve product specification",
                        "The requirement specifies a flush valve product (e.g. Sloan, flushometer, diaphragm type) and must be evaluated against plumbing fixture evidence only.",
                        "Verify flush valve manufacturer, model, or GPF rating in plumbing fixture schedules or drawings. Electrical and lighting evidence must be excluded.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "plumbing_water_hammer_arrestor_requirement":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Plumbing water hammer arrestor requirement",
                        "The requirement calls for water hammer arrestors or shock arrestors and must stay within plumbing pipe accessory scope.",
                        "Verify plumbing accessory, pipe fitting, or arrestor evidence in model. Electrical and mechanical elements must not be used as closing evidence.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "plumbing_accessory_water_supply":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Plumbing accessory water supply connection",
                        "The requirement defines a cold or hot water supply line for a plumbing accessory (soap dispenser, eye wash, drinking fountain) and must be scoped to plumbing piping evidence.",
                        "Confirm plumbing pipe or accessory evidence for the supply connection. Electrical or mechanical equipment must not serve as closing evidence.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "plumbing_support_hanger_requirement":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Plumbing pipe support or hanger requirement",
                        "The requirement specifies pipe supports, clevis hangers, or seismic bracing for plumbing systems and must be evaluated against plumbing structural evidence.",
                        "Verify pipe support, hanger, or structural attachment evidence in plumbing drawings. Electrical fixtures, conduit, and communication devices must be excluded.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "manufacturer_product_spec_submittal":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Manufacturer, product, spec, and submittal requirements",
                        "The requirement is specification-driven and needs direct manufacturer, model, or catalog evidence.",
                        "Review the specification or submittal and export product metadata only if the requirement is intended to be model-checkable.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "installation_method_constraint":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Installation method constraint",
                        "The requirement is about installation method, roof penetration, or field execution and needs direct detail evidence.",
                        "Review the installation detail or field instruction before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "code_jurisdiction_requirement":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Code or jurisdiction requirement",
                        "The requirement is about code or jurisdiction review and needs direct code evidence.",
                        "Review the code matrix or jurisdiction notes before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "mechanical_performance_feature":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Mechanical performance feature",
                        "The requirement is about performance features such as compressor speed or ionization and needs direct product evidence.",
                        "Review the product data or specification before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "identification_labeling_nameplate":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Identification, labels, tags, and nameplates",
                        "The requirement is about identification or marking and cannot be closed from equipment presence alone.",
                        "Verify mark, label, tag, or nameplate evidence in the model, submittal, or specification.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "drawing_spec_manual_owner_approval":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Drawing, specification, manual, or owner approval requirement",
                        "The requirement depends on drawings, specifications, manuals, or owner direction and is not safely model-closed.",
                        "Review the drawings/specifications or owner guidance before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "field_execution_demolition_protection":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Field execution, demolition, salvage, or protection requirement",
                        "The requirement is tied to phase, demolition, or field-execution evidence rather than broad category presence.",
                        "Review the drawings and phase context, then verify the field condition before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "conduit_raceway":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Conduit and raceway requirement",
                        "The requirement is about conduit, raceway, or installation-pathway evidence and cannot be closed from unrelated equipment presence.",
                        "Review conduit/raceway model evidence plus drawings and specifications before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "commissioning_testing_om_training":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Commissioning, testing, O&M, or training requirement",
                        "The requirement depends on closeout deliverables rather than model element presence.",
                        "Review commissioning records, test reports, O&M manuals, training logs, or specifications before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "controls_bms_bas_contactors_relays":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Controls, BMS/BAS, contactors, or relays requirement",
                        "The requirement is about controls, BMS/BAS, contactors, or relays and cannot be closed from equipment presence alone.",
                        "Review controls drawings, specifications, and coordination before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "mechanical_controls_ddc_emcs":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "DDC/EMCS/controls requirement",
                        "The requirement is about DDC, EMCS, demand control ventilation, or control sequences and is not safely model-closed.",
                        "Review controls drawings, DDC/EMCS schedules, and specifications before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "lighting_control_scheme":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Lighting control scheme requirement",
                        "The requirement is about lighting controls, occupancy sensors, or switching schemes and cannot be closed from fixture presence alone.",
                        "Review lighting controls drawings and specifications before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "operation_maintenance_manual":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "O&M manual or maintenance documentation requirement",
                        "The requirement depends on O&M manual deliverables rather than model element presence.",
                        "Review O&M manual requirements and closeout deliverables before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;

                case "attic_stock_spare_parts":
                    result = EvaluateParameterDrivenRequirement(
                        requirement,
                        modelRecords,
                        selectedDiscipline,
                        semanticProfile.ExpectedParameters,
                        "Attic stock or spare parts requirement",
                        "The requirement depends on procurement and physical inventory, not model element presence.",
                        "Review attic stock quantities against specification requirements before closing this item.",
                        requirementText,
                        false,
                        scopedCategories,
                        fallbackAllowed,
                        evidenceIndex,
                        ruleContext,
                        semanticProfile);
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Filters model records by text hints derived from the requirement text.
        /// When an EvidenceIndex is provided, uses pre-built search blobs (O(1) lookup per record)
        /// instead of rebuilding them (O(P) per record where P = parameter count).
        /// This eliminates the O(N×M×P) bottleneck: 804 requirements × 21,868 elements × ~10 params = 175M+ string ops.
        /// With index: O(N × M_candidates) where M_candidates &lt;&lt; M due to category pre-filtering.
        /// </summary>
        private static List<ExportElementRecord> FilterByTextHints(
            IReadOnlyCollection<ExportElementRecord> modelRecords,
            string requirementText,
            IEnumerable<string> categoryHints = null,
            EvidenceIndex evidenceIndex = null,
            bool allowFullModelFallback = false)
        {
            List<ExportElementRecord> records = modelRecords == null
                ? new List<ExportElementRecord>()
                : modelRecords.ToList();

            if (records.Count == 0)
            {
                return records;
            }

            string text = Normalize(requirementText);
            HashSet<string> hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (categoryHints != null)
            {
                foreach (string hint in categoryHints)
                {
                    if (!string.IsNullOrWhiteSpace(hint))
                    {
                        hints.Add(Normalize(hint));
                    }
                }
            }

            if (ContainsAny(text, new[] { "lighting", "light fixture", "fixture", "illumination" }))
            {
                hints.Add("lighting fixtures");
            }

            if (ContainsAny(text, new[] { "panel", "circuit", "voltage", "load", "electrical" }))
            {
                hints.Add("electrical");
                hints.Add("lighting");
            }

            if (ContainsMechanicalSignals(text))
            {
                hints.Add("mechanical");
            }

            if (ContainsAny(text, new[] { "plumbing", "pipe", "drain", "vent", "water", "sanitary" }))
            {
                hints.Add("plumbing");
            }

            if (ContainsAny(text, new[] { "technology", "data", "communication", "security", "fire alarm", "telephone", "low voltage" }))
            {
                hints.Add("technology");
                hints.Add("communication");
            }

            if (ContainsAny(text, new[] { "level", "elevation" }))
            {
                hints.Add("level");
            }

            if (hints.Count == 0)
            {
                return allowFullModelFallback ? records : new List<ExportElementRecord>();
            }

            // --- Fast path: use category index for initial candidate selection ---
            // When we have an evidence index AND category hints, narrow down candidates
            // by category first (O(1) dictionary lookup per category) before blob matching.
            IEnumerable<ExportElementRecord> candidatePool = records;
            if (evidenceIndex != null && categoryHints != null)
            {
                HashSet<ExportElementRecord> categoryCandidates = new HashSet<ExportElementRecord>();
                foreach (string hint in hints)
                {
                    string normalizedHint = hint.ToLowerInvariant();
                    List<ExportElementRecord> catRecords;
                    if (evidenceIndex.ByCategoryNormalized.TryGetValue(normalizedHint, out catRecords))
                    {
                        foreach (ExportElementRecord r in catRecords)
                        {
                            categoryCandidates.Add(r);
                        }
                    }
                }

                // Only use category pre-filter if it found candidates.
                // High-risk rules forbid falling back to the full model when no scoped candidates exist.
                if (categoryCandidates.Count > 0)
                {
                    candidatePool = categoryCandidates;
                }
                else if (!allowFullModelFallback)
                {
                    return new List<ExportElementRecord>();
                }
            }

            List<ExportElementRecord> filtered = candidatePool
                .Where(record => RecordMatchesAnyHint(record, hints, evidenceIndex))
                .ToList();
            if (filtered.Count == 0 && allowFullModelFallback)
            {
                return records;
            }

            return filtered;
        }

        /// <summary>
        /// Checks whether a record's search blob contains any of the given hints.
        /// When evidenceIndex is provided, uses pre-built blob (O(1) dictionary lookup).
        /// Without index, falls back to BuildSearchBlob (backward compatible).
        /// </summary>
        private static bool RecordMatchesAnyHint(ExportElementRecord record, HashSet<string> hints, EvidenceIndex evidenceIndex = null)
        {
            if (record == null)
            {
                return false;
            }

            // Use pre-built blob from index if available — eliminates O(P) StringBuilder + normalize per call
            string blob;
            if (evidenceIndex != null && evidenceIndex.SearchBlobs.TryGetValue(record, out blob))
            {
                // blob is already lowercased from EvidenceIndex constructor
            }
            else
            {
                blob = BuildSearchBlob(record);
            }

            foreach (string hint in hints)
            {
                if (!string.IsNullOrWhiteSpace(hint) && blob.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasNonEmptyParameter(ExportElementRecord record, string parameterName)
        {
            string value = GetParameterValue(record, parameterName);
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string GetParameterValue(ExportElementRecord record, string parameterName)
        {
            if (record == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return null;
            }

            ParameterRecord parameter;
            if (record.InstanceParameters != null &&
                record.InstanceParameters.TryGetValue(parameterName, out parameter))
            {
                return FirstNonEmpty(parameter);
            }

            if (record.TypeParameters != null &&
                record.TypeParameters.TryGetValue(parameterName, out parameter))
            {
                return FirstNonEmpty(parameter);
            }

            return null;
        }

        private static string FirstNonEmpty(ParameterRecord parameter)
        {
            if (parameter == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(parameter.ValueString))
            {
                return parameter.ValueString;
            }

            if (!string.IsNullOrWhiteSpace(parameter.RawValue))
            {
                return parameter.RawValue;
            }

            return null;
        }

        private static bool ContainsAny(string text, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null)
            {
                return false;
            }

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) &&
                    text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAnyWord(string text, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null)
            {
                return false;
            }

            foreach (string keyword in keywords)
            {
                if (ContainsWholeWord(text, keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsWholeWord(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            string trimmed = keyword.Trim();
            if (trimmed.IndexOf(' ') >= 0 || trimmed.IndexOf('-') >= 0 || trimmed.IndexOf('/') >= 0 || trimmed.Any(char.IsDigit))
            {
                return text.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            string pattern = @"(?<!\w)" + Regex.Escape(trimmed) + @"(?:s|es)?(?!\w)";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string BuildSearchBlob(ExportElementRecord record)
        {
            StringBuilder blob = new StringBuilder();

            Append(blob, record.Category);
            Append(blob, record.Name);
            Append(blob, record.Family);
            Append(blob, record.Type);
            Append(blob, record.Level);

            AppendParameters(blob, record.InstanceParameters);
            AppendParameters(blob, record.TypeParameters);

            return Normalize(blob.ToString());
        }

        private static void AppendParameters(StringBuilder builder, IReadOnlyDictionary<string, ParameterRecord> parameters)
        {
            if (parameters == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ParameterRecord> pair in parameters)
            {
                Append(builder, pair.Key);

                ParameterRecord parameter = pair.Value;
                if (parameter != null)
                {
                    Append(builder, parameter.ValueString);
                    Append(builder, parameter.RawValue);
                }
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(value);
        }

        private static string Normalize(string value)
        {
            return RequirementDisciplineNormalizer.NormalizeText(value);
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(not set)" : value;
        }
    }
}
