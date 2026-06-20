using System;
using System.Collections.Generic;

namespace EMAExtractor.Requirements.Audit
{
    // =====================================================================
    // EMA AI — Requirement Audit & Coherence contracts (Evaluation Bundle v1)
    //
    // These types form the auditable layer that sits *on top of* the
    // deterministic C# evaluation engine. They never recompute a requirement
    // status: the engine remains the single authority. They record *how* the
    // engine reached a decision (source -> semantic IR -> applicability ->
    // evidence policy -> candidate funnel -> decision -> confidence) and audit
    // the *coherence* of every requirement and requirement type against each
    // other (duplicates, numeric/quantity/manufacturer conflicts).
    //
    // Everything here is Revit-free so it compiles and is testable under the
    // net8.0 test project and ships inside the net4.8 add-in.
    // =====================================================================

    /// <summary>Kinds of cross-requirement coherence problems EMA AI detects.</summary>
    public enum CoherenceFindingType
    {
        ExactDuplicate,
        SemanticDuplicate,
        NumericConflict,
        QuantityConflict,
        ManufacturerConflict,
        ScopeConflict,
        BrokenReference,
        RequirementTypeInconsistency
    }

    public enum CoherenceSeverity
    {
        Info,
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>How much evidence (and of which classes) closes a requirement.</summary>
    public enum EvidencePolicyOperator
    {
        Any,
        All,
        AtLeastN,
        ManualRequired
    }

    /// <summary>
    /// Audit-layer mirror of the engine status. It is *projected* from the
    /// engine decision, never independently recomputed. Indeterminate is
    /// reserved for parser/rule/input execution failures so a technical error
    /// is never silently reported as Not Met.
    /// </summary>
    public enum AuditDecisionStatus
    {
        Compliant,
        NonCompliant,
        NeedsReview,
        InsufficientData,
        NotApplicable,
        Indeterminate
    }

    public enum AuditLifecycleStatus
    {
        Parsed,
        Classified,
        Evaluated,
        CoherenceChecked,
        HumanReviewed,
        Locked,
        Superseded
    }

    /// <summary>A single normalized numeric/unit constraint extracted from text.</summary>
    public class SemanticQuantity
    {
        public string Property { get; set; }   // voltage, current, percent, count, length, conduit_size
        public string Operator { get; set; }   // =, >=, <=, min, max
        public double Value { get; set; }
        public string Unit { get; set; }       // V, A, %, ft, in, mm, ea
        public string RawText { get; set; }
    }

    /// <summary>
    /// Deterministic "semantic IR" for a requirement: what the requirement
    /// asks for, expressed structurally. First-pass is regex/dictionary based;
    /// an LLM may later *suggest* an IR but can never replace this one.
    /// </summary>
    public class RequirementSemanticIr
    {
        public string NormalizedText { get; set; }
        public string ContentHash { get; set; }
        public string Modality { get; set; }        // shall, must, should, may, none
        public string Quantifier { get; set; }      // each, all, at_least, at_most, none
        public double? MinimumQuantity { get; set; }
        public double? MaximumQuantity { get; set; }
        public List<SemanticQuantity> Quantities { get; set; } = new List<SemanticQuantity>();
        public List<string> ManufacturerBrands { get; set; } = new List<string>();
        public List<string> ExcludedManufacturerBrands { get; set; } = new List<string>();
        public bool ManufacturerExclusive { get; set; }
        public List<string> SubjectTokens { get; set; } = new List<string>();
        public List<string> Conditions { get; set; } = new List<string>();
        public List<string> Exceptions { get; set; } = new List<string>();
        public List<string> Ambiguities { get; set; } = new List<string>();
    }

    /// <summary>Lightweight pointer to a requirement, used inside findings.</summary>
    public class RequirementRef
    {
        public string RequirementId { get; set; }
        public string SourceWorksheet { get; set; }
        public int SourceRow { get; set; }
        public string Discipline { get; set; }
        public string RequirementType { get; set; }
        public string ShortText { get; set; }
    }

    public class CoherenceFinding
    {
        public string Id { get; set; }
        public CoherenceFindingType FindingType { get; set; }
        public CoherenceSeverity Severity { get; set; }
        public string RequirementType { get; set; }
        public RequirementRef Primary { get; set; }
        public RequirementRef Related { get; set; }
        public string Rationale { get; set; }
        public Dictionary<string, string> NormalizedValues { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Status { get; set; } = "open";

        public string FindingTypeLabel
        {
            get
            {
                switch (FindingType)
                {
                    case CoherenceFindingType.ExactDuplicate: return "Exact Duplicate";
                    case CoherenceFindingType.SemanticDuplicate: return "Semantic Duplicate";
                    case CoherenceFindingType.NumericConflict: return "Numeric Conflict";
                    case CoherenceFindingType.QuantityConflict: return "Quantity Conflict";
                    case CoherenceFindingType.ManufacturerConflict: return "Manufacturer Conflict";
                    case CoherenceFindingType.ScopeConflict: return "Scope Conflict";
                    case CoherenceFindingType.BrokenReference: return "Broken Reference";
                    case CoherenceFindingType.RequirementTypeInconsistency: return "Requirement Type Inconsistency";
                    default: return FindingType.ToString();
                }
            }
        }

        public string SeverityLabel
        {
            get
            {
                switch (Severity)
                {
                    case CoherenceSeverity.Critical: return "Critical";
                    case CoherenceSeverity.High: return "High";
                    case CoherenceSeverity.Medium: return "Medium";
                    case CoherenceSeverity.Low: return "Low";
                    default: return "Info";
                }
            }
        }
    }

    /// <summary>Per-requirement-type coherence rollup (the "type" axis the owner asked for).</summary>
    public class RequirementTypeCoherenceSummary
    {
        public string RequirementType { get; set; }
        public int RequirementCount { get; set; }
        public int FindingCount { get; set; }
        public int DuplicateCount { get; set; }
        public int ConflictCount { get; set; }
        public CoherenceSeverity HighestSeverity { get; set; }
        public bool IsCoherent
        {
            get { return FindingCount == 0; }
        }
    }

    public class RequirementCoherenceReport
    {
        public List<CoherenceFinding> Findings { get; set; } = new List<CoherenceFinding>();
        public List<RequirementTypeCoherenceSummary> TypeSummaries { get; set; } = new List<RequirementTypeCoherenceSummary>();
        public int RequirementsAnalyzed { get; set; }
        public int RequirementTypesAnalyzed { get; set; }
        public int ExactDuplicateCount { get; set; }
        public int SemanticDuplicateCount { get; set; }
        public int NumericConflictCount { get; set; }
        public int QuantityConflictCount { get; set; }
        public int ManufacturerConflictCount { get; set; }
        public int HighSeverityCount { get; set; }
        public string CoherenceGrade { get; set; } = "Coherent";
    }

    // ----------------------------- Audit record -----------------------------

    public class SourceProvenance
    {
        public string SourceFile { get; set; }
        public string SourceWorksheet { get; set; }
        public int SourceRow { get; set; }
        public string RequirementId { get; set; }
        public string RequirementContentHash { get; set; }
        public bool TraceabilityComplete { get; set; }
        public List<string> TraceabilityGaps { get; set; } = new List<string>();
    }

    public class EvidencePolicy
    {
        public EvidencePolicyOperator Operator { get; set; }
        public List<string> RequiredEvidenceTypes { get; set; } = new List<string>();
        public int? MinimumCount { get; set; }
        public bool ClosureRequiresHumanReview { get; set; }
        public string Rationale { get; set; }
    }

    public class CandidateFunnelStage
    {
        public string StageName { get; set; }
        public int InputCount { get; set; }
        public int OutputCount { get; set; }
        public string Criteria { get; set; }
    }

    public class CandidateFunnel
    {
        public List<CandidateFunnelStage> Stages { get; set; } = new List<CandidateFunnelStage>();
        public int UniverseCount { get; set; }
        public int QualifiedCount { get; set; }
        public bool BroadMatch { get; set; }
        public string BroadMatchReason { get; set; }
    }

    /// <summary>The auditable dossier for one requirement within one run.</summary>
    public class RequirementAuditRecord
    {
        public string RequirementId { get; set; }
        public AuditDecisionStatus DecisionStatus { get; set; }
        public AuditLifecycleStatus LifecycleStatus { get; set; }
        public SourceProvenance Source { get; set; } = new SourceProvenance();
        public RequirementSemanticIr SemanticIr { get; set; } = new RequirementSemanticIr();
        public string RequirementType { get; set; }
        public string RequirementTypeReason { get; set; }
        public string ValidationType { get; set; }
        public List<string> TaxonomyLabels { get; set; } = new List<string>();
        public bool Applies { get; set; } = true;
        public string ApplicabilityReason { get; set; }
        public EvidencePolicy EvidencePolicy { get; set; } = new EvidencePolicy();
        public CandidateFunnel CandidateFunnel { get; set; } = new CandidateFunnel();
        public string RuleApplied { get; set; }
        public string DecisionReason { get; set; }
        public double Confidence { get; set; }
        public string ConfidenceReason { get; set; }
        public int DirectEvidenceCount { get; set; }
        public int SupportingEvidenceCount { get; set; }
        public List<string> CoherenceFindingIds { get; set; } = new List<string>();
        public string NextBestAction { get; set; }
        public string RecordHash { get; set; }

        public string DecisionStatusLabel
        {
            get
            {
                switch (DecisionStatus)
                {
                    case AuditDecisionStatus.Compliant: return "Compliant";
                    case AuditDecisionStatus.NonCompliant: return "Non-Compliant";
                    case AuditDecisionStatus.NeedsReview: return "Needs Human Review";
                    case AuditDecisionStatus.InsufficientData: return "Insufficient Data";
                    case AuditDecisionStatus.NotApplicable: return "Not Applicable";
                    case AuditDecisionStatus.Indeterminate: return "Indeterminate";
                    default: return DecisionStatus.ToString();
                }
            }
        }
    }

    // ----------------------------- Evaluation bundle -----------------------------

    public class EvaluationManifest
    {
        public string SchemaVersion { get; set; }
        public string EvaluationRunId { get; set; }
        public string ProjectName { get; set; }
        public string ModelName { get; set; }
        public string RequirementsFile { get; set; }
        public string EngineVersion { get; set; }
        public string RulesetVersion { get; set; }
        public string TaxonomyVersion { get; set; }
        public string ScorePolicyVersion { get; set; }
        public string AsOfUtc { get; set; }
        public string GeneratedAtUtc { get; set; }
        public string InputHash { get; set; }
        public string OutputHash { get; set; }
        public int RequirementsTotal { get; set; }
        public Dictionary<string, int> StatusCounts { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public int CoherenceFindingsTotal { get; set; }
    }

    /// <summary>
    /// A closed, reproducible record of a single evaluation run: manifest with
    /// hashes + versions, every requirement audit record, and the coherence
    /// report. The HTML/PDF report and the dashboard are downstream *views* of
    /// this bundle, never the system of record.
    /// </summary>
    public class EvaluationBundle
    {
        public EvaluationManifest Manifest { get; set; } = new EvaluationManifest();
        public List<RequirementAuditRecord> AuditRecords { get; set; } = new List<RequirementAuditRecord>();
        public RequirementCoherenceReport Coherence { get; set; } = new RequirementCoherenceReport();
    }
}
