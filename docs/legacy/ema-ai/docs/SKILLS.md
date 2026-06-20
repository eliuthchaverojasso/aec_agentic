# EMA AI — Capability / Skill Map

**Last updated:** 2026-06-08

---

| # | Skill | Purpose | Inputs | Outputs | Source Files | Tests | Status |
|---|-------|---------|--------|---------|-------------|-------|--------|
| 1 | Parse Owner Requirements | Read XLSX workbook into requirement rows | XLSX file, workbook path | List<OwnerRequirementRow> | `RequirementCheckWorkflowService.cs` | Integration via workflow test | ✅ |
| 2 | Normalize Discipline | Standardize discipline names | Raw discipline string | Normalized discipline | `RequirementComparisonEngine.cs` | Engine tests | ✅ |
| 3 | Classify Validation Type | Determine Model/Drawing/Spec/Manual/Hybrid | Requirement text | ValidationTypeResult | `ValidationTypeClassifier.cs` | Engine tests | ✅ |
| 4 | Capture/Sync Revit Model Data | Snapshot current Revit model state | Revit Document | List<ExportElementRecord> | `RequirementCheckWorkflowService.cs`, `ModelSnapshotService` | Manual smoke | 🟡 |
| 5 | Build EvidenceIndex | Pre-index elements for O(1) lookup | List<ExportElementRecord> | EvidenceIndex (categories, blobs) | `RequirementComparisonEngine.cs` | `EvidenceIndex_BuildsCategoryIndex`, `_PreBuildsSearchBlobs` | ✅ |
| 6 | Match Evidence | Match requirements against evidence index | RequirementRow, EvidenceIndex | List<MatchedElementEvidence> | `RequirementComparisonEngine.cs` | `Evaluate_*` tests | ✅ |
| 7 | Assign Status | Assign Met/Not Met/Needs Human Review/etc. | Evidence matches, rules | RequirementCheckStatus | `RequirementComparisonEngine.cs` | Engine tests | ✅ |
| 8 | Explain Validation Type | Generate validation type reason | Requirement text, type | Reasoning string | `ValidationTypeClassifier.cs` | Engine tests | ✅ |
| 9 | Explain Rule Applied | Generate rule metadata | Rule match context | RuleContext | `RequirementComparisonEngine.cs` | Report tests | ✅ |
| 10 | Explain Evidence Found | Generate evidence summary | Matched elements | Evidence summary block | `RequirementComparisonEngine.cs` | Report tests | ✅ |
| 11 | Calculate Confidence | Multi-factor confidence scoring | Evidence, rule, context | ConfidenceScore | `ConfidenceScorer.cs` | Engine tests | ✅ |
| 12 | Calculate Scores | Overall, discipline, readiness scoring | Results, summaries | Scores, ReadinessLabels | `ScoreCalculator.cs` | Report tests | ✅ |
| 13 | Rank Key Issues | Identify and rank critical issues | Results (non-Met/NA) | List<KeyIssue> | `KeyIssueRanker.cs` | Report tests | ✅ |
| 14 | Generate Reasoning | Explain why status was assigned | Evidence, rules, scores | Reasoning string | `RequirementComparisonEngine.cs` | Engine tests | ✅ |
| 15 | Generate Next Best Action | Recommend what to do | Status, evidence, rules | Action string | `RequirementComparisonEngine.cs` | Engine tests | ✅ |
| 16 | Generate Master Report | Full self-contained HTML report | All results, summaries, issues | HTML string with embedded CSS/JS | `OwnerRequirementHtmlReportGenerator.cs` | 30 report tests | ✅ |
| 17 | Filter/Navigate by Discipline | Show subset by discipline | Full results, discipline filter | Filtered report view | Report JS + `BuildFilterContext` | Report tests | ✅ |
| 18 | Distinguish Master vs Active Filter | Show filter context banner | Current filter state | Banner text | Report generator + JS | `Generate_FilterContextBannerExistsForMasterView`, `_ForDisciplineView` | ✅ |
| 19 | Trace Revit Element IDs | Collect and display Element IDs | Matched elements | Element ID lists | `RequirementComparisonEngine.cs`, report generator | Report tests | ✅ |
| 20 | Collapse Long Traceability | Hide large ID lists by default | Element ID list | Collapsed UI with preview | Report generator JS | `Generate_TraceabilityCollapsedByDefault` | ✅ |
| 21 | Generate Machine-readable JSON | Build hidden context block | All results, summaries, metadata | JSON string → `#ema-ai-report-context` | `OwnerRequirementHtmlReportGenerator.cs` | `Generate_EmbedsValidMachineReadableJsonAndKeepsItHidden` | ✅ |
| 22 | Ask EMA AI | Answer questions about the report | Report context, user question | AI or deterministic response | `AskAboutReportCommand.cs`, `ExplainSelectedIssueCommand.cs` | Manual | 🟡 |
| 23 | Export PDF / Copy Summary | Copy/clipboard summary, browser print | Report state | Clipboard text, print layout | Report generator JS | Manual | 🟡 |
| 24 | Revit Progress UI | Modeless progress window | Progress updates | WPF progress window | `ComplianceProgressWindow.cs`, `RequirementCheckWorkflowService.cs` | Manual | ✅ |
| 25 | Diagnostics/Settings | Persist last run settings | Report metadata | Local settings | `RequirementCheckWorkflowService.cs`, settings | Manual | ✅ |
