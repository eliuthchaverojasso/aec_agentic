using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EMAExtractor.Requirements.Audit
{
    /// <summary>
    /// Builds the auditable dossier for each requirement by *projecting* the
    /// deterministic engine result — it never re-evaluates a status. The engine
    /// is the authority; this records the provenance, the parsed obligation, the
    /// evidence policy that would close it, the candidate funnel that was walked,
    /// and the decision + confidence that the engine produced, plus any coherence
    /// findings that touch the requirement.
    /// </summary>
    public static class RequirementAuditRecordBuilder
    {
        // Display-layer broad-match threshold mirrors Reporting.EvidenceEmbedLimits
        // but is kept local so the audit layer has no dependency on the report layer.
        private const int BroadMatchElementThreshold = 1000;

        public static List<RequirementAuditRecord> BuildAll(
            IReadOnlyList<RequirementCheckResult> results,
            RequirementCoherenceReport coherence)
        {
            List<RequirementAuditRecord> records = new List<RequirementAuditRecord>();
            if (results == null)
            {
                return records;
            }

            Dictionary<string, List<string>> findingsByRef = IndexFindingsByRequirement(coherence);

            foreach (RequirementCheckResult result in results)
            {
                if (result == null)
                {
                    continue;
                }
                records.Add(Build(result, findingsByRef));
            }

            return records
                .OrderBy(r => r.Source.SourceWorksheet, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Source.SourceRow)
                .ThenBy(r => r.RequirementId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static RequirementAuditRecord Build(
            RequirementCheckResult result,
            Dictionary<string, List<string>> findingsByRef)
        {
            string text = !string.IsNullOrWhiteSpace(result.RequirementText)
                ? result.RequirementText
                : (result.Requirement != null ? result.Requirement.RequirementText : string.Empty);

            RequirementSemanticIr ir = RequirementSemanticParser.Parse(text);
            SourceProvenance source = BuildProvenance(result, ir);
            EvidencePolicy policy = BuildEvidencePolicy(result, ir);
            CandidateFunnel funnel = BuildFunnel(result);

            RequirementAuditRecord record = new RequirementAuditRecord
            {
                RequirementId = source.RequirementId,
                DecisionStatus = MapStatus(result.Status),
                LifecycleStatus = AuditLifecycleStatus.CoherenceChecked,
                Source = source,
                SemanticIr = ir,
                RequirementType = string.IsNullOrWhiteSpace(result.RequirementType) ? "unclassified" : result.RequirementType,
                RequirementTypeReason = result.RequirementTypeReason,
                ValidationType = result.ValidationType.ToString(),
                TaxonomyLabels = (result.TaxonomyLabels ?? new List<TaxonomyLabel>())
                    .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Label))
                    .Select(t => t.Label)
                    .ToList(),
                Applies = result.Status != RequirementCheckStatus.NotApplicable,
                ApplicabilityReason = result.Status == RequirementCheckStatus.NotApplicable
                    ? (string.IsNullOrWhiteSpace(result.StatusReason) ? "Out of scope for the selected discipline/model." : result.StatusReason)
                    : "In scope for this evaluation.",
                EvidencePolicy = policy,
                CandidateFunnel = funnel,
                RuleApplied = result.RuleApplied,
                DecisionReason = string.IsNullOrWhiteSpace(result.StatusReason) ? result.Reasoning : result.StatusReason,
                Confidence = Math.Round(result.Confidence, 4),
                ConfidenceReason = result.ConfidenceReason,
                DirectEvidenceCount = result.DirectClosingEvidence != null ? result.DirectClosingEvidence.Count : 0,
                SupportingEvidenceCount = result.SupportingContext != null ? result.SupportingContext.Count : 0,
                NextBestAction = result.NextBestAction
            };

            string refKey = RefKey(source.SourceWorksheet, source.SourceRow, record.RequirementId);
            if (findingsByRef != null && findingsByRef.TryGetValue(refKey, out List<string> ids))
            {
                record.CoherenceFindingIds = ids.OrderBy(i => i, StringComparer.Ordinal).ToList();
            }

            record.RecordHash = ComputeRecordHash(record);
            return record;
        }

        private static SourceProvenance BuildProvenance(RequirementCheckResult result, RequirementSemanticIr ir)
        {
            string file = !string.IsNullOrWhiteSpace(result.SourceFile)
                ? result.SourceFile
                : (result.Requirement != null ? result.Requirement.SourceFile : null);
            string worksheet = !string.IsNullOrWhiteSpace(result.SourceWorksheet)
                ? result.SourceWorksheet
                : (result.Requirement != null ? result.Requirement.SourceSheet : null);
            int row = result.SourceRow > 0
                ? result.SourceRow
                : (result.Requirement != null ? result.Requirement.RowNumber : 0);
            string id = !string.IsNullOrWhiteSpace(result.RequirementId)
                ? result.RequirementId
                : (result.Requirement != null ? result.Requirement.RequirementId : null);
            if (string.IsNullOrWhiteSpace(id))
            {
                id = "row-" + row.ToString(CultureInfo.InvariantCulture);
            }

            List<string> gaps = new List<string>();
            if (string.IsNullOrWhiteSpace(file)) gaps.Add("SOURCE_FILE_MISSING");
            if (string.IsNullOrWhiteSpace(worksheet)) gaps.Add("SOURCE_WORKSHEET_UNKNOWN");
            if (row <= 0) gaps.Add("SOURCE_ROW_UNKNOWN");
            if (string.IsNullOrWhiteSpace(ir.NormalizedText)) gaps.Add("SOURCE_TEXT_EMPTY");

            return new SourceProvenance
            {
                SourceFile = file,
                SourceWorksheet = worksheet,
                SourceRow = row,
                RequirementId = id,
                RequirementContentHash = ir.ContentHash,
                TraceabilityComplete = gaps.Count == 0,
                TraceabilityGaps = gaps
            };
        }

        private static EvidencePolicy BuildEvidencePolicy(RequirementCheckResult result, RequirementSemanticIr ir)
        {
            EvidencePolicy policy = new EvidencePolicy();
            switch (result.ValidationType)
            {
                case ValidationType.Model:
                    policy.Operator = EvidencePolicyOperator.All;
                    policy.RequiredEvidenceTypes = new List<string> { "MODEL_ELEMENT", "PARAMETER" };
                    policy.ClosureRequiresHumanReview = false;
                    policy.Rationale = "Model-checkable: closable by model elements with the required parameters populated.";
                    break;
                case ValidationType.Drawing:
                    policy.Operator = EvidencePolicyOperator.All;
                    policy.RequiredEvidenceTypes = new List<string> { "DRAWING" };
                    policy.ClosureRequiresHumanReview = true;
                    policy.Rationale = "Drawing-dependent: requires a sheet/detail; not closable by model geometry alone.";
                    break;
                case ValidationType.Specification:
                    policy.Operator = EvidencePolicyOperator.All;
                    policy.RequiredEvidenceTypes = new List<string> { "SPECIFICATION_OR_MANUFACTURER_DATA" };
                    policy.ClosureRequiresHumanReview = true;
                    policy.Rationale = "Specification/manufacturer constraint: requires product data or spec review.";
                    break;
                case ValidationType.Manual:
                    policy.Operator = EvidencePolicyOperator.ManualRequired;
                    policy.RequiredEvidenceTypes = new List<string> { "MANUAL_REVIEW" };
                    policy.ClosureRequiresHumanReview = true;
                    policy.Rationale = "Coordination/field requirement: must be confirmed by a human reviewer.";
                    break;
                default: // Hybrid
                    policy.Operator = EvidencePolicyOperator.All;
                    policy.RequiredEvidenceTypes = new List<string> { "MODEL_ELEMENT", "DRAWING_OR_SPECIFICATION" };
                    policy.ClosureRequiresHumanReview = true;
                    policy.Rationale = "Hybrid: combines model evidence with drawing/spec or manual confirmation.";
                    break;
            }

            if (ir.MinimumQuantity.HasValue &&
                (result.ValidationType == ValidationType.Model || result.ValidationType == ValidationType.Hybrid))
            {
                policy.Operator = EvidencePolicyOperator.AtLeastN;
                policy.MinimumCount = (int)Math.Round(ir.MinimumQuantity.Value);
                policy.Rationale += string.Format(CultureInfo.InvariantCulture,
                    " Requires at least {0} qualifying element(s).", policy.MinimumCount);
            }

            return policy;
        }

        private static CandidateFunnel BuildFunnel(RequirementCheckResult result)
        {
            CandidateFunnel funnel = new CandidateFunnel();
            RequirementFilterTrace trace = result.FilterTrace;

            if (trace != null && trace.CandidateStages != null)
            {
                foreach (FilterStageTrace stage in trace.CandidateStages)
                {
                    if (stage == null)
                    {
                        continue;
                    }
                    funnel.Stages.Add(new CandidateFunnelStage
                    {
                        StageName = stage.StageName,
                        InputCount = stage.InputCount,
                        OutputCount = stage.OutputCount,
                        Criteria = stage.Criteria
                    });
                }
            }

            funnel.UniverseCount = funnel.Stages.Count > 0 ? funnel.Stages[0].InputCount : result.MatchedModelElementCount;
            funnel.QualifiedCount = result.MatchedModelElementCount;
            funnel.BroadMatch = result.FullModelFallbackUsed
                || !result.CandidateScopeValid
                || result.MatchedModelElementCount >= BroadMatchElementThreshold;
            funnel.BroadMatchReason = funnel.BroadMatch
                ? (string.IsNullOrWhiteSpace(result.CandidateScopeReason)
                    ? "Broad category/keyword sweep — treated as supporting context, cannot auto-close the requirement."
                    : result.CandidateScopeReason)
                : null;

            return funnel;
        }

        private static AuditDecisionStatus MapStatus(RequirementCheckStatus status)
        {
            switch (status)
            {
                case RequirementCheckStatus.Met: return AuditDecisionStatus.Compliant;
                case RequirementCheckStatus.NotMet: return AuditDecisionStatus.NonCompliant;
                case RequirementCheckStatus.NeedsHumanReview: return AuditDecisionStatus.NeedsReview;
                case RequirementCheckStatus.NotApplicable: return AuditDecisionStatus.NotApplicable;
                case RequirementCheckStatus.InsufficientModelData: return AuditDecisionStatus.InsufficientData;
                default: return AuditDecisionStatus.Indeterminate;
            }
        }

        private static Dictionary<string, List<string>> IndexFindingsByRequirement(RequirementCoherenceReport coherence)
        {
            Dictionary<string, List<string>> map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (coherence == null || coherence.Findings == null)
            {
                return map;
            }

            foreach (CoherenceFinding finding in coherence.Findings)
            {
                AddRef(map, finding.Primary, finding.Id);
                AddRef(map, finding.Related, finding.Id);
            }
            return map;
        }

        private static void AddRef(Dictionary<string, List<string>> map, RequirementRef reference, string findingId)
        {
            if (reference == null)
            {
                return;
            }
            string key = RefKey(reference.SourceWorksheet, reference.SourceRow, reference.RequirementId);
            if (!map.TryGetValue(key, out List<string> ids))
            {
                ids = new List<string>();
                map[key] = ids;
            }
            if (!ids.Contains(findingId))
            {
                ids.Add(findingId);
            }
        }

        private static string RefKey(string worksheet, int row, string id)
        {
            return (worksheet ?? string.Empty).ToLowerInvariant() + "#" +
                   row.ToString(CultureInfo.InvariantCulture) + "#" +
                   (id ?? string.Empty).ToLowerInvariant();
        }

        private static string ComputeRecordHash(RequirementAuditRecord record)
        {
            // Canonical, order-stable digest of the *decision identity* of this
            // requirement. Same requirement + same engine decision => same hash.
            string canonical = string.Join("|", new[]
            {
                record.RequirementId ?? string.Empty,
                record.Source.RequirementContentHash ?? string.Empty,
                record.DecisionStatus.ToString(),
                record.RequirementType ?? string.Empty,
                record.ValidationType ?? string.Empty,
                record.RuleApplied ?? string.Empty,
                record.Confidence.ToString("F4", CultureInfo.InvariantCulture),
                record.Applies ? "applies" : "n/a",
                record.EvidencePolicy.Operator.ToString(),
                string.Join(",", record.CoherenceFindingIds)
            });
            return RequirementSemanticParser.Sha256Hex(canonical);
        }
    }
}
