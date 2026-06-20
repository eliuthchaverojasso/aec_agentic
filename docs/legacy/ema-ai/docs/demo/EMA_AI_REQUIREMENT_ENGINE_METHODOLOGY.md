# EMA AI Requirement Engine Methodology

## Pipeline

```
Owner Requirements XLSX
→ Requirement Parser
→ Discipline Classifier
→ Validation Type Classifier
→ Evidence Matcher
→ Status Assignment Engine
→ Confidence Scorer
→ Reasoning Generator
→ Next Best Action Generator
→ Key Issue Ranker
→ Score Calculator
→ Professional Report
```

## Formulas

### Confidence Score

```
Confidence =
  0.25 × DisciplineConfidence
+ 0.20 × RequirementClarity
+ 0.25 × EvidenceStrength
+ 0.15 × RuleSpecificity
+ 0.10 × DataCompleteness
+ 0.05 × SourceTraceability
```

Components:
- **DisciplineConfidence**: 1.0 explicit column, 0.85 worksheet, 0.70 keywords, 0.50 weak inference
- **RequirementClarity**: 1.0 clear object+action+condition, 0.75 partial, 0.50 generic, 0.30 ambiguous
- **EvidenceStrength**: 1.0 exact, 0.75 partial, 0.50 indirect, 0.25 weak, 0.0 none
- **RuleSpecificity**: 1.0 model-checkable, 0.75 hybrid, 0.50 drawing/spec, 0.30 manual
- **DataCompleteness**: 1.0 complete, 0.75 few missing, 0.50 several missing, 0.25 insufficient
- **SourceTraceability**: 1.0 worksheet+row+source, 0.75 worksheet+row, 0.50 text only

### Key Issue Score

```
KeyIssueScore =
  0.30 × Severity
+ 0.20 × DisciplineRelevance
+ 0.20 × DeliverableImpact
+ 0.15 × EvidenceGap
+ 0.10 × Confidence
+ 0.05 × Actionability
```

Components:
- **Severity**: NotMet=1.0, InsufficientModelData=0.75, NeedsHumanReview=0.60, Met/NA=0.0
- **DisciplineRelevance**: Selected=1.0, Cross-discipline=0.75, Related=0.40, Unrelated=0.0
- **DeliverableImpact**: Current=1.0, Next=0.75, General=0.50, Future=0.25
- **EvidenceGap**: Missing=1.0, Partial=0.75, Ambiguous=0.50, External=0.25, Complete=0.0
- **Actionability**: Clear model update=1.0, Clear review=0.75, Triage=0.50, Unclear=0.25

### Overall Score

```
OverallScore = 100 × Σ(w_i × RequirementScore_i) / Σ(w_i)
RequirementScore = StatusValue × Confidence
```

Status values: Met=1.00, NeedsHumanReview=0.55, InsufficientModelData=0.40, NotMet=0.00, NotApplicable=excluded

### Readiness Score

```
ReadinessScore =
  0.40 × RequirementCoverage
+ 0.25 × EvidenceCoverage
+ 0.20 × QAQCHealth
+ 0.10 × DrawingSpecCoverage
+ 0.05 × SyncFreshness
```

Labels: 90-100 Ready, 75-89 OnTrack, 60-74 AtRisk, 40-59 Behind, 0-39 Critical

## Status Definitions

| Status | Meaning |
|--------|---------|
| **Met** | Required model evidence exists and is complete |
| **Not Met** | Related evidence exists but required data is missing |
| **Needs Human Review** | Depends on specs, drawings, owner standards, or judgment |
| **Insufficient Model Data** | Appears model-checkable but evidence is inadequate |
| **Not Applicable** | Outside selected discipline/scope |

## Human Review Boundary

Items marked for human review include:
- Specification/manufacturer requirements
- Drawing/schedule-dependent items
- Owner standard preferences
- Professional judgment items
- Cross-discipline coordination

## No-Overclaim Policy

The engine never uses: Certified, Approved, Guaranteed, Legally compliant, Code compliant, Final compliance.

AI may explain, summarize, and suggest. AI must never approve compliance, change official statuses, close issues, invent evidence, or replace engineering review.

## Source Files

| Component | File |
|-----------|------|
| Parser | `EMAExtractor/Services/OwnerRequirementsExcelParser.cs` |
| Discipline Normalizer | `EMAExtractor/Requirements/RequirementDisciplineNormalizer.cs` |
| Validation Classifier | `EMAExtractor/Requirements/ValidationTypeClassifier.cs` |
| Comparison Engine | `EMAExtractor/Requirements/RequirementComparisonEngine.cs` |
| Confidence Scorer | `EMAExtractor/Requirements/ConfidenceScorer.cs` |
| Key Issue Ranker | `EMAExtractor/Requirements/KeyIssueRanker.cs` |
| Score Calculator | `EMAExtractor/Requirements/ScoreCalculator.cs` |
| Models | `EMAExtractor/Requirements/RequirementCheckModels.cs` |
| Report Generator | `EMAExtractor/Reporting/OwnerRequirementHtmlReportGenerator.cs` |
| Workflow Service | `EMAExtractor/Services/RequirementCheckWorkflowService.cs` |
