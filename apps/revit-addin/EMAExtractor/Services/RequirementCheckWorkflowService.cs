using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Core;
using EMAExtractor.Models;
using EMAExtractor.Reporting;
using EMAExtractor.Requirements;
using EMAExtractor.Requirements.Audit;
using EMAExtractor.UI;

namespace EMAExtractor.Services
{
    public static class RequirementCheckWorkflowService
    {
        public static void Run(ExternalCommandData commandData)
        {
            if (!LoadRequirements(commandData))
            {
                return;
            }

            RunComplianceCheck(commandData);
        }

        public static bool LoadRequirements(ExternalCommandData commandData)
        {
            if (commandData == null || commandData.Application == null || commandData.Application.ActiveUIDocument == null)
            {
                throw new InvalidOperationException("No active Revit document was found.");
            }

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            EmaSettings settings = LocalConfigService.LoadSettings();
            OwnerRequirementsCheckWindow window = new OwnerRequirementsCheckWindow(
                doc != null ? doc.Title : "Revit Model",
                string.IsNullOrWhiteSpace(settings.DefaultOutputFolder) ? LoggingService.AppRoot : settings.DefaultOutputFolder);

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true)
            {
                return false;
            }

            string workbookPath = window.OwnerRequirementsFilePath;
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                throw new InvalidOperationException("Please choose an Owner Requirements workbook.");
            }

            if (!File.Exists(workbookPath))
            {
                throw new FileNotFoundException("Owner Requirements workbook not found.", workbookPath);
            }

            string outputFolder = ResolveOutputFolder(window.OutputFolder, settings.DefaultOutputFolder);
            Directory.CreateDirectory(outputFolder);

            OwnerRequirementsExcelParser parser = new OwnerRequirementsExcelParser();
            List<OwnerRequirementRow> requirementRows = parser.Parse(workbookPath);
            if (requirementRows == null || requirementRows.Count == 0)
            {
                throw new InvalidOperationException("No requirement rows were found in the workbook.");
            }

            RequirementDiscipline selectedDiscipline = window.SelectedDiscipline;
            RequirementModelScope selectedScope = window.SelectedScope;
            string detectedDisciplines = BuildDetectedDisciplines(requirementRows);

            settings.LastRequirementWorkbookPath = workbookPath;
            settings.LastRequirementsWorkbookName = Path.GetFileName(workbookPath);
            settings.LastRequirementsLoadStatus = "Loaded";
            settings.LastRequirementsLoadedAt = DateTime.Now.ToString("o");
            settings.LastRequirementsRowCount = requirementRows.Count;
            settings.LastRequirementsDetectedDisciplines = detectedDisciplines;
            settings.LastRequirementsSelectedDiscipline = selectedDiscipline.ToString();
            settings.LastRequirementsSelectedScope = selectedScope.ToString();
            settings.LastRequirementReportDiscipline = selectedDiscipline.ToString();
            settings.LastRequirementReportScope = selectedScope.ToString();
            settings.LastRequirementReportPath = "";
            settings.LastRequirementReportGeneratedAt = "";
            settings.LastRequirementReportClipboardSummary = "";
            settings.LastRequirementReportMetCount = 0;
            settings.LastRequirementReportNotMetCount = 0;
            settings.LastRequirementReportNeedsReviewCount = 0;
            settings.LastRequirementReportNotApplicableCount = 0;
            settings.LastRequirementReportInsufficientDataCount = 0;
            settings.LastRequirementReportMatchScore = 0.0;
            settings.DefaultOutputFolder = outputFolder;
            LocalConfigService.SaveSettings(settings);

            TaskDialog.Show(
                "EMA AI Requirements Loaded",
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Loaded {0} requirement row(s).\n\nWorkbook: {1}\nDiscipline: {2}\nScope: {3}\nDetected disciplines: {4}\nOutput folder: {5}",
                    requirementRows.Count,
                    settings.LastRequirementsWorkbookName,
                    settings.LastRequirementsSelectedDiscipline,
                    settings.LastRequirementsSelectedScope,
                    string.IsNullOrWhiteSpace(detectedDisciplines) ? "(none)" : detectedDisciplines,
                    outputFolder));

            return true;
        }

        /// <summary>
        /// Non-blocking compliance check. Captures model snapshot on the Revit API thread (fast),
        /// then runs all deterministic analysis on a background thread.
        /// The ComplianceProgressWindow stays responsive — no "Not Responding".
        /// </summary>
        public static void RunComplianceCheck(ExternalCommandData commandData)
        {
            if (commandData == null || commandData.Application == null || commandData.Application.ActiveUIDocument == null)
            {
                throw new InvalidOperationException("No active Revit document was found.");
            }

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            EmaSettings settings = LocalConfigService.LoadSettings();

            bool requirementsLoaded = string.Equals(settings.LastRequirementsLoadStatus, "Loaded", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(settings.LastRequirementWorkbookPath)
                && File.Exists(settings.LastRequirementWorkbookPath);
            bool modelSynced = string.Equals(settings.LastModelSyncStatus, "Synced", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(settings.LastExportPath)
                && File.Exists(settings.LastExportPath);

            if (!requirementsLoaded || !modelSynced)
            {
                TaskDialog.Show("EMA AI", "Load requirements and sync model data before running the compliance check.");
                return;
            }

            string outputFolder = ResolveOutputFolder(settings.DefaultOutputFolder, LoggingService.AppRoot);
            Directory.CreateDirectory(outputFolder);

            RequirementDiscipline discipline = RequirementDisciplineNormalizer.Parse(
                settings.LastRequirementsSelectedDiscipline,
                RequirementDiscipline.All);
            if (discipline == RequirementDiscipline.All)
            {
                discipline = RequirementDisciplineNormalizer.Parse(settings.LastRequirementReportDiscipline, RequirementDiscipline.All);
            }

            RequirementModelScope scope = settings.LastRequirementsSelectedScope == nameof(RequirementModelScope.CurrentView)
                ? RequirementModelScope.CurrentView
                : RequirementModelScope.EntireModel;

            int maxParallelism = Math.Max(1, Environment.ProcessorCount - 1);
            string disciplineLabel = discipline.ToString();
            string scopeLabel = scope == RequirementModelScope.CurrentView ? "Current View" : "Entire Model";

            // ── Step A: Capture model snapshot on Revit API thread (fast, synchronous) ──
            Stopwatch snapshotTimer = Stopwatch.StartNew();
            ModelSnapshot snapshot = ModelSnapshotService.Capture(doc, discipline, scope);
            snapshotTimer.Stop();
            long snapshotMs = snapshotTimer.ElapsedMilliseconds;
            string docTitle = doc.Title;

            // Capture immutable values needed by background thread
            string workbookPath = settings.LastRequirementWorkbookPath;
            string lastModelSyncAt = settings.LastModelSyncAt;

            // ── Step B: Show modeless progress window (stays responsive) ──
            ComplianceProgressWindow progressWindow = new ComplianceProgressWindow(disciplineLabel, scopeLabel);
            progressWindow.Show();

            List<StageInfo> stages = RequirementCheckProgress.BuildDefaultStages();
            stages[0].Status = StageStatus.Complete;  // Preparing requirements — done by LoadRequirements
            stages[1].Status = StageStatus.Complete;  // Capturing model evidence — just completed above
            stages[1].ElapsedMs = snapshotMs;

            ReportProgress(progressWindow, stages, 2, 10, "Capturing model evidence complete.",
                0, 0, snapshot.TotalElements, 0, disciplineLabel, scopeLabel,
                TimeSpan.FromMilliseconds(snapshotMs), null, false, true, null);

            // ── Step C: Background deterministic engine (no Revit API, DTOs only) ──
            CancellationTokenSource cts = new CancellationTokenSource();
            RequirementDiscipline disciplineCopy = discipline;
            RequirementModelScope scopeCopy = scope;

            Task.Run(() =>
            {
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                ComplianceRunDiagnostics diagnostics = new ComplianceRunDiagnostics
                {
                    MaxDegreeOfParallelism = maxParallelism,
                    ModelSnapshotMs = snapshotMs,
                    ElementCount = snapshot.TotalElements
                };

                try
                {
                    // Stage 3: Build evidence index
                    SetStageRunning(stages, 2);
                    ReportProgress(progressWindow, stages, 3, 10, "Building evidence index...",
                        0, 0, snapshot.TotalElements, 0, disciplineLabel, scopeLabel,
                        totalStopwatch.Elapsed, null, true, true, null);

                    Stopwatch sw = Stopwatch.StartNew();
                    EvidenceIndex evidenceIndex = new EvidenceIndex(snapshot.Records);
                    sw.Stop();
                    diagnostics.EvidenceIndexMs = sw.ElapsedMilliseconds;
                    SetStageComplete(stages, 2, sw.ElapsedMilliseconds);

                    if (progressWindow.CancelRequested) { progressWindow.ShowCancelled(); return; }

                    // Stage 4 (was 3): Parse requirements
                    SetStageRunning(stages, 3);
                    ReportProgress(progressWindow, stages, 4, 10, "Reading owner requirements...",
                        0, 0, snapshot.TotalElements, 0, disciplineLabel, scopeLabel,
                        totalStopwatch.Elapsed, null, true, true, null);

                    sw.Restart();
                    OwnerRequirementsExcelParser parser = new OwnerRequirementsExcelParser();
                    List<OwnerRequirementRow> requirementRows = parser.Parse(workbookPath);
                    sw.Stop();
                    diagnostics.RequirementsParseMs = sw.ElapsedMilliseconds;
                    SetStageComplete(stages, 3, sw.ElapsedMilliseconds);

                    if (requirementRows == null || requirementRows.Count == 0)
                    {
                        progressWindow.ShowError("No actionable Owner Requirements were found in the selected workbook.");
                        return;
                    }

                    diagnostics.RequirementCount = requirementRows.Count;

                    if (progressWindow.CancelRequested) { progressWindow.ShowCancelled(); return; }

                    // Stage 5 (was 4): Match requirements (PARALLEL)
                    SetStageRunning(stages, 3);
                    sw.Restart();
                    RequirementComparisonEngine engine = new RequirementComparisonEngine();

                    // Use direct progress reporting instead of Progress<T> (which requires SynchronizationContext)
                    int processedCount = 0;
                    IProgress<int> matchProgress = new Progress<int>(count =>
                    {
                        processedCount = count;
                    });

                    // Run parallel matching with periodic UI updates via a simple counter
                    SetStageRunning(stages, 3);
                    ReportProgress(progressWindow, stages, 4, 10,
                        string.Format("Matching {0} requirements against {1} elements...", requirementRows.Count, snapshot.TotalElements),
                        0, requirementRows.Count, snapshot.TotalElements, 0, disciplineLabel, scopeLabel,
                        totalStopwatch.Elapsed, null, false, true, null);

                    List<RequirementCheckResult> results = engine.EvaluateParallel(
                        requirementRows,
                        evidenceIndex,
                        disciplineCopy,
                        maxParallelism,
                        new MatchingProgressReporter(progressWindow, stages, requirementRows.Count,
                            snapshot.TotalElements, disciplineLabel, scopeLabel, totalStopwatch),
                        cts.Token);
                    sw.Stop();
                    diagnostics.MatchingMs = sw.ElapsedMilliseconds;
                    SetStageComplete(stages, 3, sw.ElapsedMilliseconds);

                    if (progressWindow.CancelRequested) { progressWindow.ShowCancelled(); return; }

                    // Stage 5: Scoring
                    SetStageRunning(stages, 4);
                    ReportProgress(progressWindow, stages, 5, 10, "Calculating confidence and scores...",
                        requirementRows.Count, requirementRows.Count, snapshot.TotalElements, 0,
                        disciplineLabel, scopeLabel, totalStopwatch.Elapsed, null, true, true, null);

                    sw.Restart();
                    EnhanceResultsWithScoringAndRanking(results);
                    sw.Stop();
                    diagnostics.ScoringMs = sw.ElapsedMilliseconds;
                    SetStageComplete(stages, 4, sw.ElapsedMilliseconds);

                    // Stage 6: Ranking
                    SetStageRunning(stages, 5);
                    sw.Restart();
                    double overallScore = ScoreCalculator.CalculateOverallScore(results);
                    DateTime syncDate = DateTime.TryParse(lastModelSyncAt, out var sd) ? sd : DateTime.Now;
                    ReadinessMetrics readinessMetrics = ScoreCalculator.CalculateReadiness(results, syncDate);
                    List<KeyIssue> keyIssues = KeyIssueRanker.RankIssues(results, disciplineCopy, 10);
                    sw.Stop();
                    diagnostics.KeyIssueRankingMs = sw.ElapsedMilliseconds;
                    SetStageComplete(stages, 5, sw.ElapsedMilliseconds);

                    // Stage 7: Coherence checks
                    SetStageRunning(stages, 6);
                    List<string> coherenceWarnings = CoherenceChecker.Check(results, snapshot.TotalElements);
                    diagnostics.CoherenceWarnings = coherenceWarnings;
                    SetStageComplete(stages, 6, 0);

                    ReportProgress(progressWindow, stages, 7, 10, "Ranking key issues...",
                        requirementRows.Count, requirementRows.Count, snapshot.TotalElements, keyIssues.Count,
                        disciplineLabel, scopeLabel, totalStopwatch.Elapsed, null, false, true, null);

                    // Stage 8: Generate report
                    SetStageRunning(stages, 7);
                    ReportProgress(progressWindow, stages, 8, 10, "Generating report...",
                        requirementRows.Count, requirementRows.Count, snapshot.TotalElements, keyIssues.Count,
                        disciplineLabel, scopeLabel, totalStopwatch.Elapsed, null, true, true, null);

                    RequirementCheckReport report = new RequirementCheckReport
                    {
                        ProjectName = docTitle,
                        ModelName = docTitle,
                        RequirementsFileName = Path.GetFileName(workbookPath),
                        RequirementsFilePath = workbookPath,
                        Discipline = disciplineCopy,
                        Scope = scopeCopy,
                        GeneratedAt = DateTime.Now,
                        OutputFolder = outputFolder,
                        CaptureNote = snapshot.CaptureNote,
                        ModelElementCount = snapshot.TotalElements,
                        Results = results,
                        Summary = RequirementCheckSummary.FromResults(results),
                        OverallScore = overallScore,
                        ReadinessScore = readinessMetrics.OverallScore,
                        ReadinessLabel = readinessMetrics.ReadinessLabel,
                        KeyIssues = keyIssues,
                        DisciplineSummaries = BuildDisciplineSummaries(results),
                        FilterContext = BuildFilterContext(results, keyIssues, disciplineCopy, reportScope: scopeCopy, initialDiscipline: disciplineCopy),
                        LastModelSyncTime = syncDate
                    };

                    if (coherenceWarnings.Count > 0)
                    {
                        foreach (string warning in coherenceWarnings)
                        {
                            report.Warnings.Add("[Coherence] " + warning);
                        }
                    }

                    sw.Restart();
                    string reportPath = OwnerRequirementHtmlReportGenerator.Generate(report);
                    sw.Stop();
                    diagnostics.ReportGenerationMs = sw.ElapsedMilliseconds;
                    SetStageComplete(stages, 7, sw.ElapsedMilliseconds);

                    // Emit the reproducible Evaluation Bundle v1 (manifest + per-requirement
                    // audit records + coherence findings) alongside the HTML. The bundle is the
                    // canonical, hashable record of this run; the HTML is a view of it. Failure
                    // to write the bundle must never break the user-facing report.
                    try
                    {
                        string bundleFolder = EvaluationBundleWriter.CreateAndWrite(
                            report, results, report.GeneratedAt.ToUniversalTime(), outputFolder);
                        LoggingService.Info("Evaluation bundle written: " + bundleFolder);
                    }
                    catch (Exception bundleEx)
                    {
                        LoggingService.Error("Evaluation bundle generation failed.", bundleEx);
                    }

                    // Stage 9: Preparing Ask EMA AI context
                    SetStageRunning(stages, 8);
                    SetStageComplete(stages, 8, 0);

                    // Stage 10: Complete
                    SetStageComplete(stages, 9, 0);

                    totalStopwatch.Stop();
                    diagnostics.TotalRunMs = totalStopwatch.ElapsedMilliseconds;
                    LoggingService.Info(diagnostics.ToString());

                    // Save settings (file I/O, safe from background thread)
                    EmaSettings updatedSettings = LocalConfigService.LoadSettings();
                    updatedSettings.LastRequirementReportPath = reportPath;
                    updatedSettings.LastRequirementReportGeneratedAt = report.GeneratedAt.ToString("o");
                    updatedSettings.LastRequirementReportDiscipline = report.Discipline.ToString();
                    updatedSettings.LastRequirementReportScope = report.Scope.ToString();
                    updatedSettings.LastRequirementReportMetCount = report.Summary.MetCount;
                    updatedSettings.LastRequirementReportNotMetCount = report.Summary.NotMetCount;
                    updatedSettings.LastRequirementReportNeedsReviewCount = report.Summary.NeedsHumanReviewCount;
                    updatedSettings.LastRequirementReportNotApplicableCount = report.Summary.NotApplicableCount;
                    updatedSettings.LastRequirementReportInsufficientDataCount = report.Summary.InsufficientModelDataCount;
                    updatedSettings.LastRequirementReportMatchScore = report.Summary.MatchScore;
                    updatedSettings.LastRequirementsLoadStatus = "Loaded";
                    updatedSettings.LastModelSyncStatus = "Synced";
                    updatedSettings.LastRequirementReportClipboardSummary = report.BuildClipboardSummary();
                    LocalConfigService.SaveSettings(updatedSettings);

                    // Detail lines for the expander
                    List<string> details = new List<string>
                    {
                        "Output: " + reportPath,
                        string.Format("Timing: snapshot {0}ms, index {1}ms, parse {2}ms, match {3}ms",
                            diagnostics.ModelSnapshotMs, diagnostics.EvidenceIndexMs,
                            diagnostics.RequirementsParseMs, diagnostics.MatchingMs),
                        string.Format("Scoring {0}ms, ranking {1}ms, report {2}ms, total {3}ms",
                            diagnostics.ScoringMs, diagnostics.KeyIssueRankingMs,
                            diagnostics.ReportGenerationMs, diagnostics.TotalRunMs),
                        "Threads: " + diagnostics.MaxDegreeOfParallelism
                    };
                    if (coherenceWarnings.Count > 0)
                    {
                        details.Add("Coherence warnings: " + string.Join("; ", coherenceWarnings));
                    }

                    // Show completion state in the progress window (on UI thread via Dispatcher)
                    ReportProgress(progressWindow, stages, 10, 10, "Report ready.",
                        requirementRows.Count, requirementRows.Count, snapshot.TotalElements, keyIssues.Count,
                        disciplineLabel, scopeLabel, totalStopwatch.Elapsed, null, false, false, details);

                    progressWindow.ShowComplete(
                        reportPath,
                        updatedSettings.LastRequirementReportClipboardSummary,
                        report.Summary.MetCount,
                        report.Summary.NotMetCount,
                        report.Summary.NeedsHumanReviewCount,
                        report.Summary.InsufficientModelDataCount,
                        report.Summary.NotApplicableCount,
                        overallScore,
                        keyIssues.Count,
                        coherenceWarnings.Count > 0 ? coherenceWarnings : null);
                }
                catch (OperationCanceledException)
                {
                    progressWindow.ShowCancelled();
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Compliance check failed.", ex);
                    progressWindow.ShowError(ex.Message);
                }
            });
        }

        /// <summary>
        /// Simple IProgress adapter that throttles UI updates to avoid flooding the dispatcher.
        /// </summary>
        private class MatchingProgressReporter : IProgress<int>
        {
            private readonly ComplianceProgressWindow _window;
            private readonly List<StageInfo> _stages;
            private readonly int _totalRequirements;
            private readonly int _totalElements;
            private readonly string _discipline;
            private readonly string _scope;
            private readonly Stopwatch _stopwatch;
            private int _lastReported;

            public MatchingProgressReporter(ComplianceProgressWindow window, List<StageInfo> stages,
                int totalRequirements, int totalElements, string discipline, string scope, Stopwatch stopwatch)
            {
                _window = window;
                _stages = stages;
                _totalRequirements = totalRequirements;
                _totalElements = totalElements;
                _discipline = discipline;
                _scope = scope;
                _stopwatch = stopwatch;
            }

            public void Report(int count)
            {
                // Throttle: report every 25 items or at completion
                if (count - _lastReported < 25 && count < _totalRequirements) return;
                _lastReported = count;

                double stagePercent = _totalRequirements > 0 ? (double)count / _totalRequirements * 100.0 : 0;
                double overallPercent = 30.0 + stagePercent * 0.40; // Matching is ~40% of overall work

                TimeSpan elapsed = _stopwatch.Elapsed;
                TimeSpan? remaining = null;
                if (count > 0 && count < _totalRequirements)
                {
                    double rate = elapsed.TotalSeconds / count;
                    remaining = TimeSpan.FromSeconds(rate * (_totalRequirements - count));
                }

                ReportProgress(_window, _stages, 4, 10,
                    string.Format("Matching requirement {0} of {1}...", count, _totalRequirements),
                    count, _totalRequirements, _totalElements, 0, _discipline, _scope,
                    elapsed, remaining, false, true, null);
            }
        }

        private static void SetStageRunning(List<StageInfo> stages, int index)
        {
            if (index >= 0 && index < stages.Count)
            {
                stages[index].Status = StageStatus.Running;
            }
        }

        private static void SetStageComplete(List<StageInfo> stages, int index, long elapsedMs)
        {
            if (index >= 0 && index < stages.Count)
            {
                stages[index].Status = StageStatus.Complete;
                stages[index].ElapsedMs = elapsedMs;
            }
        }

        private static void ReportProgress(
            ComplianceProgressWindow window,
            List<StageInfo> stages,
            int stageIndex,
            int totalStages,
            string message,
            int processedRequirements,
            int totalRequirements,
            int indexedElements,
            int keyIssuesFound,
            string discipline,
            string scope,
            TimeSpan elapsed,
            TimeSpan? remaining,
            bool isIndeterminate,
            bool canCancel,
            List<string> detailLines)
        {
            double overallPercent = totalStages > 0 ? (double)stageIndex / totalStages * 100.0 : 0;
            double stagePercent = 0;

            // For the matching stage, calculate sub-stage percent
            if (stageIndex == 4 && totalRequirements > 0)
            {
                stagePercent = (double)processedRequirements / totalRequirements * 100.0;
                overallPercent = 30.0 + stagePercent * 0.40;
            }
            else if (stageIndex >= totalStages)
            {
                overallPercent = 100;
                stagePercent = 100;
            }

            RequirementCheckProgress progress = new RequirementCheckProgress
            {
                StageName = message,
                StageIndex = stageIndex,
                TotalStages = totalStages,
                OverallPercent = overallPercent,
                StagePercent = stagePercent,
                Message = message,
                ProcessedRequirements = processedRequirements,
                TotalRequirements = totalRequirements,
                IndexedElements = indexedElements,
                KeyIssuesFound = keyIssuesFound,
                Discipline = discipline,
                Scope = scope,
                Elapsed = elapsed,
                EstimatedRemaining = remaining,
                IsIndeterminate = isIndeterminate,
                CanCancel = canCancel,
                DetailLines = detailLines ?? new List<string>(),
                Stages = new List<StageInfo>(stages)
            };

            progress.Clamp();
            window.UpdateProgress(progress);
        }

        private static string ResolveOutputFolder(string preferredFolder, string fallbackFolder)
        {
            if (!string.IsNullOrWhiteSpace(preferredFolder))
            {
                return preferredFolder;
            }

            if (!string.IsNullOrWhiteSpace(fallbackFolder))
            {
                return fallbackFolder;
            }

            return LoggingService.AppRoot;
        }

        private static string BuildDetectedDisciplines(IReadOnlyCollection<OwnerRequirementRow> requirementRows)
        {
            if (requirementRows == null || requirementRows.Count == 0)
            {
                return string.Empty;
            }

            List<string> disciplines = new List<string>();
            foreach (OwnerRequirementRow row in requirementRows)
            {
                string label = string.IsNullOrWhiteSpace(row?.Discipline)
                    ? "Unknown"
                    : RequirementDisciplineNormalizer.Parse(row.Discipline, RequirementDiscipline.All).ToString();

                if (string.IsNullOrWhiteSpace(label))
                {
                    label = "Unknown";
                }

                if (!disciplines.Any(item => string.Equals(item, label, StringComparison.OrdinalIgnoreCase)))
                {
                    disciplines.Add(label);
                }
            }

            return string.Join(", ", disciplines);
        }

        private static void EnhanceResultsWithScoringAndRanking(List<RequirementCheckResult> results)
        {
            if (results == null)
            {
                return;
            }

            foreach (var result in results)
            {
                if (result == null)
                {
                    continue;
                }

                var validationTypeResult = ValidationTypeClassifier.Classify(
                    result.Requirement?.RequirementText ?? string.Empty,
                    result.Requirement?.SourceSheet ?? string.Empty);

                result.ValidationType = validationTypeResult.PrimaryType;
                result.TaxonomyLabels = validationTypeResult.TaxonomyLabels;

                var scoringContext = new ConfidenceScoringContext
                {
                    RequirementText = result.Requirement?.RequirementText,
                    DisciplineSource = result.Requirement?.Discipline,
                    ValidationType = validationTypeResult.PrimaryType,
                    EvidenceAlignment = result.EvidenceAlignment,
                    DisciplineMatchFound = result.Status != RequirementCheckStatus.NotApplicable,
                    ExplicitDisciplineColumn = !string.IsNullOrWhiteSpace(result.Requirement?.Discipline),
                    EvidenceStrength = result.Evidence?.Count > 0 ? Math.Min(1.0, result.Evidence.Count / 3.0) : 0.0,
                    MatchedEvidenceCount = result.Evidence?.Count ?? 0,
                    MissingExpectedParameterCount = result.MissingExpectedParameters?.Count ?? result.MissingEvidenceDetails?.Count ?? 0,
                    DirectParameterEvidenceFound = result.ActualParameterValueExamples != null && result.ActualParameterValueExamples.Count > 0,
                    HumanReviewNeeded = result.HumanReviewNeeded,
                    AmbiguousText = (result.Requirement?.RequirementText ?? string.Empty).Length < 15,
                    ParametersComplete = result.Status == RequirementCheckStatus.Met
                };

                var confidenceScore = ConfidenceScorer.Calculate(result.Requirement, scoringContext, result.Evidence);
                result.Confidence = Math.Min(1.0, confidenceScore.OverallScore);
                result.ConfidenceReason = confidenceScore.Reasoning;

                result.KeyIssueScore = CalculateKeyIssueScore(result);
            }
        }

        private static List<DisciplineSummary> BuildDisciplineSummaries(IReadOnlyCollection<RequirementCheckResult> results)
        {
            List<DisciplineSummary> summaries = new List<DisciplineSummary>();
            if (results == null || results.Count == 0)
            {
                return summaries;
            }

            foreach (string discipline in new[]
            {
                "Electrical",
                "Lighting",
                "Mechanical",
                "Plumbing",
                "Technology",
                "Unknown / Needs Classification"
            })
            {
                List<RequirementCheckResult> groupResults = results
                    .Where(result => string.Equals(GetResultDisciplineLabel(result), discipline, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (groupResults.Count == 0)
                {
                    continue;
                }

                List<string> nextActions = groupResults
                    .Where(result => result != null &&
                        result.Status != RequirementCheckStatus.Met &&
                        result.Status != RequirementCheckStatus.NotApplicable &&
                        !string.IsNullOrWhiteSpace(result.NextBestAction))
                    .OrderByDescending(ResultPriority)
                    .ThenByDescending(result => result.Confidence)
                    .Select(result => result.NextBestAction)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();

                summaries.Add(new DisciplineSummary
                {
                    Discipline = discipline,
                    Total = groupResults.Count,
                    Applicable = groupResults.Count(item => item.Status != RequirementCheckStatus.NotApplicable),
                    Met = groupResults.Count(item => item.Status == RequirementCheckStatus.Met),
                    NotMet = groupResults.Count(item => item.Status == RequirementCheckStatus.NotMet),
                    NeedsHumanReview = groupResults.Count(item => item.Status == RequirementCheckStatus.NeedsHumanReview),
                    InsufficientModelData = groupResults.Count(item => item.Status == RequirementCheckStatus.InsufficientModelData),
                    NotApplicable = groupResults.Count(item => item.Status == RequirementCheckStatus.NotApplicable),
                    DisciplineScore = ScoreCalculator.CalculateDisciplineScore(groupResults, ParseDisciplineLabel(discipline)),
                    KeyIssueCount = groupResults.Count(item => item.IsKeyIssue),
                    ResponsibleRole = discipline,
                    KeyIssues = groupResults.Count(item => item.IsKeyIssue).ToString(CultureInfo.InvariantCulture) + " key issue(s)",
                    TopNextActions = nextActions
                });
            }

            return summaries;
        }

        private static ReportFilterContext BuildFilterContext(
            IReadOnlyCollection<RequirementCheckResult> results,
            IReadOnlyCollection<KeyIssue> keyIssues,
            RequirementDiscipline selectedDiscipline,
            RequirementModelScope reportScope,
            RequirementDiscipline initialDiscipline)
        {
            List<RequirementCheckResult> resultList = results == null
                ? new List<RequirementCheckResult>()
                : results.ToList();

            string activeDiscipline = initialDiscipline == RequirementDiscipline.All
                ? "All Disciplines"
                : initialDiscipline.ToString();

            List<RequirementCheckResult> filteredResults = initialDiscipline == RequirementDiscipline.All
                ? resultList
                : resultList.Where(result => string.Equals(GetResultDisciplineLabel(result), activeDiscipline, StringComparison.OrdinalIgnoreCase)).ToList();

            List<KeyIssue> filteredKeyIssues = keyIssues == null
                ? new List<KeyIssue>()
                : initialDiscipline == RequirementDiscipline.All
                    ? keyIssues.ToList()
                    : keyIssues.Where(issue => string.Equals(issue.Discipline, activeDiscipline, StringComparison.OrdinalIgnoreCase)).ToList();

            RequirementCheckSummary counts = RequirementCheckSummary.FromResults(filteredResults);
            ReadinessMetrics readiness = ScoreCalculator.CalculateReadiness(filteredResults, DateTime.Now);

            return new ReportFilterContext
            {
                ActiveDiscipline = activeDiscipline,
                ActiveStatus = "All",
                ActiveUrgency = "All",
                FilteredResults = filteredResults,
                FilteredKeyIssues = filteredKeyIssues,
                FilteredCounts = counts,
                FilteredScores = new ReportFilterScores
                {
                    OverallScore = ScoreCalculator.CalculateOverallScore(filteredResults),
                    ReadinessScore = readiness.OverallScore,
                    DisciplineScore = initialDiscipline == RequirementDiscipline.All
                        ? ScoreCalculator.CalculateOverallScore(filteredResults)
                        : ScoreCalculator.CalculateDisciplineScore(filteredResults, selectedDiscipline),
                    ApplicableCount = counts.ConsideredCount,
                    TotalCount = counts.TotalRequirements,
                    KeyIssueCount = filteredKeyIssues.Count
                },
                SuggestedQuestions = BuildSuggestedQuestions(activeDiscipline)
            };
        }

        private static List<string> BuildSuggestedQuestions(string discipline)
        {
            if (string.Equals(discipline, "Electrical", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Electrical issues?",
                    "Why are these Electrical requirements Not Met?",
                    "Which Electrical requirements need human review?",
                    "What should the Electrical team fix first?",
                    "Which Electrical model data is missing?"
                };
            }

            if (string.Equals(discipline, "Lighting", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Lighting issues?",
                    "Why are these Lighting requirements Not Met?",
                    "Which Lighting requirements need human review?",
                    "What should the Lighting team fix first?",
                    "Which Lighting model data is missing?"
                };
            }

            if (string.Equals(discipline, "Mechanical", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Mechanical issues?",
                    "Why are these Mechanical requirements Not Met?",
                    "Which Mechanical requirements need human review?",
                    "What should the Mechanical team fix first?",
                    "Which Mechanical model data is missing?"
                };
            }

            if (string.Equals(discipline, "Plumbing", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Plumbing issues?",
                    "Why are these Plumbing requirements Not Met?",
                    "Which Plumbing requirements need human review?",
                    "What should the Plumbing team fix first?",
                    "Which Plumbing model data is missing?"
                };
            }

            if (string.Equals(discipline, "Technology", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "What are the top Technology issues?",
                    "Why are these Technology requirements Not Met?",
                    "Which Technology requirements need human review?",
                    "What should the Technology team fix first?",
                    "Which Technology model data is missing?"
                };
            }

            return new List<string>
            {
                "What are the top project-level issues?",
                "Which discipline has the most risk?",
                "What should be fixed first?",
                "Which requirements need human review?",
                "Which model data is missing?"
            };
        }

        private static RequirementDiscipline ParseDisciplineLabel(string discipline)
        {
            return RequirementDisciplineNormalizer.Parse(discipline, RequirementDiscipline.All);
        }

        private static int ResultPriority(RequirementCheckResult result)
        {
            if (result == null)
            {
                return 0;
            }

            switch (result.Status)
            {
                case RequirementCheckStatus.NotMet:
                    return 4;
                case RequirementCheckStatus.InsufficientModelData:
                    return 3;
                case RequirementCheckStatus.NeedsHumanReview:
                    return 2;
                case RequirementCheckStatus.NotApplicable:
                    return 1;
                default:
                    return 0;
            }
        }

        private static string GetResultDisciplineLabel(RequirementCheckResult result)
        {
            if (result == null)
            {
                return "Unknown / Needs Classification";
            }

            string discipline = !string.IsNullOrWhiteSpace(result.Discipline)
                ? result.Discipline
                : result.Requirement != null ? result.Requirement.Discipline : string.Empty;

            RequirementDiscipline parsed = RequirementDisciplineNormalizer.Parse(discipline, RequirementDiscipline.All);
            return parsed == RequirementDiscipline.All
                ? "Unknown / Needs Classification"
                : parsed.ToString();
        }

        private static double CalculateKeyIssueScore(RequirementCheckResult result)
        {
            if (result == null)
            {
                return 0.0;
            }

            double severity = result.Status switch
            {
                RequirementCheckStatus.NotMet => 1.0,
                RequirementCheckStatus.InsufficientModelData => 0.75,
                RequirementCheckStatus.NeedsHumanReview => 0.60,
                _ => 0.0
            };

            double disciplineRelevance = 1.0;
            double deliverableImpact = 0.9;
            double evidenceGap = result.Status == RequirementCheckStatus.NotMet ? 1.0 : 0.5;
            double actionability = string.IsNullOrWhiteSpace(result.NextBestAction) ? 0.25 : 0.75;

            double score =
                0.30 * severity +
                0.20 * disciplineRelevance +
                0.20 * deliverableImpact +
                0.15 * evidenceGap +
                0.10 * result.Confidence +
                0.05 * actionability;

            return Math.Min(1.0, Math.Max(0.0, score));
        }
    }
}
