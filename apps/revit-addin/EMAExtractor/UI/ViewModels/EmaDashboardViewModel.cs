using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using EMAExtractor.Models;
using EMAExtractor.Services;

namespace EMAExtractor.UI.ViewModels
{
    public class EmaDashboardViewModel
    {
        public EmaSettings Settings { get; private set; }
        public ProjectBinding Binding { get; private set; }
        public string ConnectionStatus { get; private set; }
        public string EnvironmentLabel { get; private set; }
        public string ProjectName { get; private set; }
        public string ModelName { get; private set; }
        public string ProjectIdText { get; private set; }
        public string ClientIdText { get; private set; }
        public string ProjectSummary { get; private set; }

        public string RequirementsStatus { get; private set; }
        public string RequirementsWorkbookName { get; private set; }
        public string RequirementsRowCountText { get; private set; }
        public string RequirementsDisciplines { get; private set; }
        public string RequirementsLoadedAt { get; private set; }

        public string ModelSyncStatus { get; private set; }
        public string ModelSyncElementCountText { get; private set; }
        public string ModelSyncAt { get; private set; }
        public string ModelSyncMessage { get; private set; }

        public string ComplianceStatus { get; private set; }
        public string ComplianceDisabledReason { get; private set; }
        public bool CanRunComplianceCheck { get; private set; }
        public bool HasGeneratedReport { get; private set; }
        public string LatestReportPath { get; private set; }

        public string ApiBaseUrl { get; private set; }
        public string DashboardUrl { get; private set; }
        public string BackendHealth { get; private set; }
        public string LastExportPath { get; private set; }
        public string LastExportMetadataPath { get; private set; }
        public string LastExportedAt { get; private set; }
        public string LastExportDiscipline { get; private set; }
        public string LastExportScope { get; private set; }
        public string LastRequirementReportPath { get; private set; }
        public string LastRequirementReportGeneratedAt { get; private set; }
        public string LastRequirementReportDiscipline { get; private set; }
        public string LastRequirementReportScope { get; private set; }
        public string LastRequirementWorkbookPath { get; private set; }
        public string LastRequirementReportCounts { get; private set; }
        public string ReadinessScore { get; private set; }
        public string ReadinessLabel { get; private set; }
        public string ReadinessDetail { get; private set; }
        public string IssuesSummary { get; private set; }
        public string TopGapSummary { get; private set; }
        public string TopActionSummary { get; private set; }
        public string ErrorMessage { get; private set; }
        public bool HasProject { get; private set; }
        public bool BackendAvailable { get; private set; }
        public int OpenIssuesCount { get; private set; }
        public List<IssueDto> Issues { get; private set; } = new List<IssueDto>();
        public List<ReadinessGapDto> TopGaps { get; private set; } = new List<ReadinessGapDto>();
        public List<ReadinessActionDto> RecommendedActions { get; private set; } = new List<ReadinessActionDto>();
        public List<TradeReadinessDto> TradeReadiness { get; private set; } = new List<TradeReadinessDto>();

        public static async Task<EmaDashboardViewModel> LoadAsync(Document document, CancellationToken cancellationToken = default)
        {
            EmaDashboardViewModel viewModel = new EmaDashboardViewModel();
            viewModel.Settings = LocalConfigService.LoadSettings();
            viewModel.Binding = ProjectBindingService.Load(document);

            viewModel.ApiBaseUrl = string.IsNullOrWhiteSpace(viewModel.Settings.ApiBaseUrl)
                ? EmaSettings.GetApiBaseUrlForEnvironment(viewModel.Settings.GetEnvironmentLabel())
                : viewModel.Settings.ApiBaseUrl.Trim();
            viewModel.DashboardUrl = string.IsNullOrWhiteSpace(viewModel.Settings.DashboardUrl)
                ? EmaSettings.GetDashboardUrlForEnvironment(viewModel.Settings.GetEnvironmentLabel())
                : viewModel.Settings.DashboardUrl.Trim();
            viewModel.EnvironmentLabel = viewModel.Settings.GetEnvironmentLabel();

            viewModel.LastExportPath = string.IsNullOrWhiteSpace(viewModel.Settings.LastExportPath) ? "(none)" : viewModel.Settings.LastExportPath;
            viewModel.LastExportMetadataPath = string.IsNullOrWhiteSpace(viewModel.Settings.LastExportMetadataPath) ? "(none)" : viewModel.Settings.LastExportMetadataPath;
            viewModel.LastExportedAt = string.IsNullOrWhiteSpace(viewModel.Settings.LastExportedAt) ? "(never)" : viewModel.Settings.LastExportedAt;
            viewModel.LastExportDiscipline = string.IsNullOrWhiteSpace(viewModel.Settings.LastExportDiscipline) ? "(not set)" : viewModel.Settings.LastExportDiscipline;
            viewModel.LastExportScope = string.IsNullOrWhiteSpace(viewModel.Settings.LastExportScope) ? "(not set)" : viewModel.Settings.LastExportScope;
            viewModel.LastRequirementReportPath = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementReportPath) ? "(none)" : viewModel.Settings.LastRequirementReportPath;
            viewModel.LastRequirementReportGeneratedAt = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementReportGeneratedAt) ? "(never)" : viewModel.Settings.LastRequirementReportGeneratedAt;
            viewModel.LastRequirementReportDiscipline = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementReportDiscipline) ? "(not set)" : viewModel.Settings.LastRequirementReportDiscipline;
            viewModel.LastRequirementReportScope = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementReportScope) ? "(not set)" : viewModel.Settings.LastRequirementReportScope;
            viewModel.LastRequirementWorkbookPath = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementWorkbookPath) ? "(none)" : viewModel.Settings.LastRequirementWorkbookPath;
            viewModel.LastRequirementReportCounts = string.Format(
                CultureInfo.InvariantCulture,
                "Met {0} | Not Met {1} | Needs Human Review {2} | Not Applicable {3} | Insufficient Model Data {4} | Score {5:0.0}%",
                viewModel.Settings.LastRequirementReportMetCount,
                viewModel.Settings.LastRequirementReportNotMetCount,
                viewModel.Settings.LastRequirementReportNeedsReviewCount,
                viewModel.Settings.LastRequirementReportNotApplicableCount,
                viewModel.Settings.LastRequirementReportInsufficientDataCount,
                viewModel.Settings.LastRequirementReportMatchScore);

            viewModel.RequirementsStatus = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementsLoadStatus)
                ? "Not loaded"
                : viewModel.Settings.LastRequirementsLoadStatus;
            viewModel.RequirementsWorkbookName = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementsWorkbookName)
                ? Path.GetFileName(viewModel.Settings.LastRequirementWorkbookPath ?? string.Empty)
                : viewModel.Settings.LastRequirementsWorkbookName;
            viewModel.RequirementsWorkbookName = string.IsNullOrWhiteSpace(viewModel.RequirementsWorkbookName) ? "(not loaded)" : viewModel.RequirementsWorkbookName;
            viewModel.RequirementsRowCountText = viewModel.Settings.LastRequirementsRowCount > 0
                ? viewModel.Settings.LastRequirementsRowCount.ToString(CultureInfo.InvariantCulture) + " row(s)"
                : "(not loaded)";
            viewModel.RequirementsDisciplines = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementsDetectedDisciplines)
                ? "(not detected)"
                : viewModel.Settings.LastRequirementsDetectedDisciplines;
            viewModel.RequirementsLoadedAt = string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementsLoadedAt)
                ? "(never)"
                : viewModel.Settings.LastRequirementsLoadedAt;

            viewModel.ModelSyncStatus = string.IsNullOrWhiteSpace(viewModel.Settings.LastModelSyncStatus)
                ? "Not synced"
                : viewModel.Settings.LastModelSyncStatus;
            viewModel.ModelSyncElementCountText = viewModel.Settings.LastModelSyncElementCount > 0
                ? viewModel.Settings.LastModelSyncElementCount.ToString(CultureInfo.InvariantCulture) + " element(s)"
                : "(not synced)";
            viewModel.ModelSyncAt = string.IsNullOrWhiteSpace(viewModel.Settings.LastModelSyncAt)
                ? "(never)"
                : viewModel.Settings.LastModelSyncAt;
            viewModel.ModelSyncMessage = string.IsNullOrWhiteSpace(viewModel.Settings.LastModelSyncMessage)
                ? "(no sync notes)"
                : viewModel.Settings.LastModelSyncMessage;

            int projectId = ResolveProjectId(viewModel.Settings, viewModel.Binding);
            viewModel.HasProject = projectId > 0;
            viewModel.ProjectIdText = projectId > 0 ? projectId.ToString(CultureInfo.InvariantCulture) : "(not set)";
            viewModel.ClientIdText = ResolveIdText(viewModel.Settings.ClientId, viewModel.Binding.ClientId);
            viewModel.ProjectName = ResolveText(viewModel.Settings.ProjectDisplayName, viewModel.Binding.ProjectDisplayName, viewModel.Binding.ProjectTitle, viewModel.Binding.ProjectFolderName, "(none)");
            viewModel.ModelName = ResolveText(viewModel.Binding.ProjectTitle, viewModel.Binding.ProjectDisplayName, viewModel.Settings.ProjectDisplayName, viewModel.Binding.ProjectFolderName, "(none)");
            viewModel.ConnectionStatus = viewModel.HasProject ? "Connected" : "Not Connected";
            viewModel.ProjectSummary = viewModel.ProjectName + " / " + viewModel.ModelName;

            viewModel.LatestReportPath = viewModel.LastRequirementReportPath;
            viewModel.HasGeneratedReport = !string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementReportPath) && File.Exists(viewModel.Settings.LastRequirementReportPath);

            bool requirementsReady = string.Equals(viewModel.RequirementsStatus, "Loaded", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(viewModel.Settings.LastRequirementWorkbookPath)
                && File.Exists(viewModel.Settings.LastRequirementWorkbookPath);
            bool modelSynced = string.Equals(viewModel.ModelSyncStatus, "Synced", StringComparison.OrdinalIgnoreCase);
            viewModel.CanRunComplianceCheck = requirementsReady && modelSynced;
            viewModel.ComplianceStatus = viewModel.HasGeneratedReport
                ? "Completed"
                : viewModel.CanRunComplianceCheck
                    ? "Ready"
                    : "Waiting";
            viewModel.ComplianceDisabledReason = viewModel.CanRunComplianceCheck
                ? string.Empty
                : "Load requirements and sync model data before running compliance check.";

            if (viewModel.HasGeneratedReport)
            {
                viewModel.ComplianceDisabledReason = "Compliance check completed. Open the latest report or run again after new model data is synced.";
            }

            viewModel.BackendHealth = "Checking...";
            ApiClient api = new ApiClient(viewModel.ApiBaseUrl);
            try
            {
                viewModel.BackendHealth = await api.GetHealthAsync(cancellationToken);
                viewModel.BackendAvailable = true;
            }
            catch (Exception ex)
            {
                viewModel.BackendHealth = "Offline: " + ex.Message;
                viewModel.BackendAvailable = false;
                viewModel.ErrorMessage = ex.Message;
            }

            if (projectId > 0)
            {
                try
                {
                    ReadinessDto readiness = await api.GetReadinessAsync(projectId, cancellationToken);
                    if (readiness != null)
                    {
                        viewModel.ReadinessScore = readiness.overall_readiness.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                        viewModel.ReadinessLabel = string.IsNullOrWhiteSpace(readiness.label) ? "(not set)" : readiness.label;
                        viewModel.ReadinessDetail = BuildReadinessDetail(readiness);
                        viewModel.TopGaps = readiness.top_gaps ?? new List<ReadinessGapDto>();
                        viewModel.RecommendedActions = readiness.recommended_actions ?? new List<ReadinessActionDto>();
                        viewModel.TradeReadiness = readiness.trade_readiness ?? new List<TradeReadinessDto>();
                        viewModel.TopGapSummary = viewModel.TopGaps.Count > 0
                            ? string.Join(Environment.NewLine, viewModel.TopGaps.Take(3).Select(item => $"{item.rule_code} [{item.severity}] {item.message}"))
                            : "No top gaps returned.";
                        viewModel.TopActionSummary = viewModel.RecommendedActions.Count > 0
                            ? string.Join(Environment.NewLine, viewModel.RecommendedActions.Take(3).Select(item => $"{item.label} [{item.severity}] {item.detail}"))
                            : "No recommended actions returned.";
                    }
                }
                catch (Exception ex)
                {
                    viewModel.ReadinessScore = "(unavailable)";
                    viewModel.ReadinessLabel = "Backend error";
                    viewModel.ReadinessDetail = ex.Message;
                }

                try
                {
                    IssueListDto issues = await api.GetIssuesAsync(projectId, 25, cancellationToken);
                    if (issues != null)
                    {
                        viewModel.Issues = issues.items ?? new List<IssueDto>();
                        viewModel.OpenIssuesCount = issues.total;
                        viewModel.IssuesSummary = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} issue(s) returned. Showing {1}.",
                            issues.total,
                            viewModel.Issues.Count);
                    }
                }
                catch (Exception ex)
                {
                    viewModel.IssuesSummary = "Issues unavailable: " + ex.Message;
                }
            }
            else
            {
                viewModel.ReadinessScore = "(not connected)";
                viewModel.ReadinessLabel = "No project binding";
                viewModel.ReadinessDetail = "Bind the model to see backend readiness.";
                viewModel.IssuesSummary = "Bind the model to see issue counts.";
                viewModel.TopGapSummary = "No project selected.";
                viewModel.TopActionSummary = "No project selected.";
            }

            return viewModel;
        }

        private static int ResolveProjectId(EmaSettings settings, ProjectBinding binding)
        {
            if (settings != null && settings.ProjectId > 0)
            {
                return settings.ProjectId;
            }

            if (binding != null && binding.ProjectId > 0)
            {
                return binding.ProjectId;
            }

            return 0;
        }

        private static string ResolveIdText(params int[] values)
        {
            foreach (int value in values)
            {
                if (value > 0)
                {
                    return value.ToString(CultureInfo.InvariantCulture);
                }
            }

            return "(not set)";
        }

        private static string ResolveText(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return "(none)";
        }

        private static string BuildReadinessDetail(ReadinessDto readiness)
        {
            if (readiness == null)
            {
                return "(none)";
            }

            List<string> pieces = new List<string>();

            if (readiness.gap_summary != null && readiness.gap_summary.Count > 0)
            {
                pieces.Add("Gap summary: " + string.Join(", ", readiness.gap_summary.Select(kvp => kvp.Key + "=" + kvp.Value)));
            }

            if (readiness.trade_readiness != null && readiness.trade_readiness.Count > 0)
            {
                pieces.Add("Trade readiness: " + string.Join(" | ", readiness.trade_readiness.Take(4).Select(item => $"{item.discipline} {item.readiness:0.0}% ({item.label})")));
            }

            if (pieces.Count == 0)
            {
                pieces.Add("No readiness detail returned.");
            }

            return string.Join(Environment.NewLine, pieces);
        }
    }
}
