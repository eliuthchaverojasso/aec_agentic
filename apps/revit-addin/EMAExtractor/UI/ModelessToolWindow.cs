using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EMAExtractor.Models;
using EMAExtractor.Services;
using RevitDocument = Autodesk.Revit.DB.Document;

namespace EMAExtractor.UI
{
    public static class ModelessToolWindow
    {
        public static void ShowSettings()
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            Window window = CreateWindow("EMA AI Settings / Connection", 640, 560);
            StackPanel content = CreateRoot(
                "EMA AI Settings / Connection",
                "Current MVP uses file-based export. Backend ingestion is manual.");

            TextBox apiUrl = AddTextBox(content, "API Base URL", settings.ApiBaseUrl);
            TextBox dashboardUrl = AddTextBox(content, "Dashboard URL", settings.DashboardUrl);
            ComboBox environment = AddCombo(content, "Environment", new[] { "Local", "LAN", "Cloud" }, settings.GetEnvironmentLabel());
            TextBox output = AddTextBox(content, "Default Output Folder", settings.DefaultOutputFolder);
            CheckBox useLandingStructure = AddCheckBox(content, "Use landing structure for Revit exports", settings.UseLandingStructure);
            TextBox landingRoot = AddTextBox(content, "Landing Root", settings.LandingRoot);
            TextBox projectFolderName = AddTextBox(content, "Project Folder Name", settings.ProjectFolderName);
            TextBox projectDisplayName = AddTextBox(content, "Project Display Name", settings.ProjectDisplayName);
            TextBox projectCode = AddTextBox(content, "Project Code (optional)", settings.ProjectCode);
            TextBox clientCode = AddTextBox(content, "Client Code (optional)", settings.ClientCode);
            ComboBox profile = AddCombo(content, "Export Profile", new[] { "Light", "Standard", "Full" }, settings.ExportProfile);
            CheckBox autoSubmit = AddCheckBox(content, "Auto-submit to backend after export (disabled / not recommended for MVP)", settings.AutoSubmitToBackend);
            ComboBox logging = AddCombo(content, "Logging Level", new[] { "Debug", "Info", "Warning", "Error" }, settings.LoggingLevel);
            TextBlock status = AddStatus(content);

            AddActions(content,
                ActionButton("Save", () =>
                {
                    settings.ApiBaseUrl = apiUrl.Text.Trim();
                    settings.DashboardUrl = dashboardUrl.Text.Trim();
                    settings.ApplyEnvironmentProfile(environment.Text);
                    settings.DefaultOutputFolder = output.Text.Trim();
                    settings.UseLandingStructure = useLandingStructure.IsChecked == true;
                    settings.LandingRoot = landingRoot.Text.Trim();
                    settings.ProjectFolderName = projectFolderName.Text.Trim();
                    settings.ProjectDisplayName = projectDisplayName.Text.Trim();
                    settings.ProjectCode = projectCode.Text.Trim();
                    settings.ClientCode = clientCode.Text.Trim();
                    settings.ExportProfile = profile.Text;
                    settings.AutoSubmitToBackend = autoSubmit.IsChecked == true;
                    settings.LoggingLevel = logging.Text;
                    LocalConfigService.SaveSettings(settings);
                    status.Text = "Settings saved.";
                }),
                ActionButton("Browse Landing Root", () => BrowseFolderInto(landingRoot)),
                ActionButton("Browse Output", () => BrowseFolderInto(output)),
                ActionButton("Select Project", () => ShowProjectBinding(null)),
                ActionButton("Test Backend Health", async () => await TestConnectionAsync(status)),
                ActionButton("Open Settings Folder", () => OpenFolder(LoggingService.AppRoot)),
                ActionButton("Reset", () =>
                {
                    LocalConfigService.SaveSettings(new EmaSettings());
                    status.Text = "Settings reset. Reopen Settings to reload defaults.";
                }));

            window.Content = Scroll(content);
            window.Show();
        }

        public static void ShowProjectBinding(RevitDocument document)
        {
            ProjectBinding binding = ProjectBindingService.Load(document);
            EmaSettings settings = LocalConfigService.LoadSettings();
            Window window = CreateWindow("EMA AI Select Project / Binding", 680, 600);
            StackPanel content = CreateRoot(
                "Select Project / Binding",
                "Bind this Revit model to a web project. The selected project controls the Revit Exports folder and backend upload target.");

            AddReadOnly(content, "Current Revit Document", document != null ? document.Title : "No active document");
            TextBox apiBaseUrl = AddTextBox(content, "API Base URL", string.IsNullOrWhiteSpace(settings.ApiBaseUrl) ? EmaSettings.GetApiBaseUrlForEnvironment(settings.GetEnvironmentLabel()) : settings.ApiBaseUrl);
            TextBox dashboardUrl = AddTextBox(content, "Dashboard URL", string.IsNullOrWhiteSpace(settings.DashboardUrl) ? EmaSettings.GetDashboardUrlForEnvironment(settings.GetEnvironmentLabel()) : settings.DashboardUrl);
            TextBox landingRootBox = AddTextBox(content, "Local Landing Root", string.IsNullOrWhiteSpace(settings.LandingRoot) ? settings.DefaultOutputFolder : settings.LandingRoot);
            ComboBox detectedProjects = AddCombo(content, "Detected Landing Project Folders", LandingStandardService.DetectProjectFolders(string.IsNullOrWhiteSpace(settings.LandingRoot) ? settings.DefaultOutputFolder : settings.LandingRoot).DefaultIfEmpty("").ToArray(), settings.ProjectFolderName);
            TextBox projectFolderName = AddTextBox(content, "Manual Project Folder Name", FirstValue(settings.ProjectFolderName, binding.ProjectFolderName));
            TextBox projectDisplayName = AddTextBox(content, "Project Display Name", FirstValue(settings.ProjectDisplayName, binding.ProjectDisplayName, binding.ProjectTitle));
            TextBox clientCode = AddTextBox(content, "Client Code (optional)", FirstValue(settings.ClientCode, binding.ClientCode));
            TextBox projectCode = AddTextBox(content, "Project Code (optional)", FirstValue(settings.ProjectCode, binding.ProjectCode));
            TextBox clientId = AddTextBox(content, "Client ID", binding.ClientId.ToString());
            TextBox projectId = AddTextBox(content, "Project ID", binding.ProjectId.ToString());
            TextBox modelId = AddTextBox(content, "Model ID", binding.ModelId.ToString());
            TextBox clientName = AddTextBox(content, "Client Name", binding.ClientName);
            TextBox projectTitle = AddTextBox(content, "Project Title", binding.ProjectTitle);
            TextBox milestone = AddTextBox(content, "Current Milestone", binding.CurrentMilestone);
            TextBlock preview = AddMonospace(content, BuildRevitExportsPreview(settings, projectFolderName.Text));
            ComboBox webProjects = AddCombo(content, "Web Projects from API", new[] { "" }, "");
            TextBlock webStatus = AddStatus(content);
            TextBlock status = AddStatus(content);
            detectedProjects.SelectionChanged += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(detectedProjects.Text))
                {
                    projectFolderName.Text = detectedProjects.Text;
                    if (string.IsNullOrWhiteSpace(projectDisplayName.Text))
                    {
                        projectDisplayName.Text = detectedProjects.Text;
                    }

                    preview.Text = BuildRevitExportsPreview(settings, projectFolderName.Text);
                }
            };
            projectFolderName.TextChanged += (sender, args) =>
            {
                EmaSettings previewSettings = LocalConfigService.LoadSettings();
                previewSettings.LandingRoot = string.IsNullOrWhiteSpace(landingRootBox.Text) ? previewSettings.DefaultOutputFolder : landingRootBox.Text.Trim();
                previewSettings.UseLandingStructure = true;
                preview.Text = BuildRevitExportsPreview(previewSettings, projectFolderName.Text);
            };

            webProjects.SelectionChanged += (sender, args) =>
            {
                ProjectDto selectedProject = webProjects.SelectedItem as ProjectDto;
                if (selectedProject == null)
                {
                    return;
                }

                projectId.Text = selectedProject.id.ToString();
                projectTitle.Text = selectedProject.DisplayName;
                projectFolderName.Text = selectedProject.FolderName;
                projectDisplayName.Text = projectFolderName.Text;

                if (selectedProject.client_id.HasValue)
                {
                    clientId.Text = selectedProject.client_id.Value.ToString();
                }

                clientName.Text = selectedProject.client_name ?? "";
                projectCode.Text = string.IsNullOrWhiteSpace(selectedProject.project_code) ? projectFolderName.Text : selectedProject.project_code;
                clientCode.Text = string.IsNullOrWhiteSpace(selectedProject.client_code) ? clientName.Text : selectedProject.client_code;
            };

            AddActions(content,
                ActionButton("Load Web Projects", async () =>
                {
                    try
                    {
                        settings.ApiBaseUrl = apiBaseUrl.Text.Trim();
                        LocalConfigService.SaveSettings(settings);

                        ApiClient api = new ApiClient(settings.ApiBaseUrl);
                        var projects = await api.GetProjectsAsync();

                        webProjects.Items.Clear();
                        foreach (ProjectDto project in projects)
                        {
                            webProjects.Items.Add(project);
                        }

                        webProjects.DisplayMemberPath = "DisplayName";
                        webStatus.Text = $"Loaded {projects.Count} project(s) from {settings.ApiBaseUrl}";
                    }
                    catch (Exception ex)
                    {
                        webStatus.Text = "Failed to load web projects: " + ex.Message;
                        LoggingService.Error("Failed to load web projects.", ex);
                    }
                }),
                ActionButton("Save Binding", () =>
                {
                    binding.RevitDocumentTitle = document != null ? document.Title : binding.RevitDocumentTitle;
                    binding.ProjectFolderName = projectFolderName.Text.Trim();
                    binding.ProjectDisplayName = projectDisplayName.Text.Trim();
                    binding.ClientCode = clientCode.Text.Trim();
                    binding.ProjectCode = projectCode.Text.Trim();
                    binding.ProjectSlug = LandingStandardService.NormalizeSlug(projectFolderName.Text);
                    binding.ClientId = ParseInt(clientId.Text, 0);
                    binding.ProjectId = ParseInt(projectId.Text, 0);
                    binding.ModelId = ParseInt(modelId.Text, 0);
                    binding.ClientName = clientName.Text.Trim();
                    binding.ProjectTitle = projectTitle.Text.Trim();
                    binding.CurrentMilestone = milestone.Text.Trim();
                    ProjectBindingService.Save(binding);
                    settings.ApiBaseUrl = apiBaseUrl.Text.Trim();
                    settings.DashboardUrl = dashboardUrl.Text.Trim();
                    settings.EnvironmentName = EmaSettings.NormalizeEnvironmentName(null, settings.ApiBaseUrl);
                    settings.LandingRoot = landingRootBox.Text.Trim();
                    settings.DefaultOutputFolder = string.IsNullOrWhiteSpace(settings.LandingRoot) ? settings.DefaultOutputFolder : settings.LandingRoot;
                    settings.UseLandingStructure = true;
                    settings.AutoSubmitToBackend = true;
                    settings.SyncMode = "cloud_upload";

                    settings.ProjectFolderName = binding.ProjectFolderName;
                    settings.ProjectDisplayName = binding.ProjectDisplayName;
                    settings.ProjectCode = binding.ProjectCode;
                    settings.ClientCode = binding.ClientCode;
                    settings.ProjectId = binding.ProjectId;
                    settings.ClientId = binding.ClientId;
                    settings.ModelId = binding.ModelId;

                    LandingStandardService.EnsureLandingFoldersForExport(settings);
                    LocalConfigService.SaveSettings(settings);
                    status.Text = "Binding saved. " + binding.DescribeModelBinding();
                }),
                ActionButton("Open Project Folder", () => OpenFolderIfExists(Path.Combine(landingRootBox.Text.Trim(), projectFolderName.Text.Trim()))),
                ActionButton("Open Revit Exports", () => OpenFolderIfExists(Path.Combine(landingRootBox.Text.Trim(), projectFolderName.Text.Trim(), LandingStandardService.RevitExportsFolderName))),
                ActionButton("Clear Binding", () =>
                {
                    ProjectBindingService.Clear();
                    status.Text = "Binding cleared.";
                }),
                ActionButton("Cancel", () =>
                {
                    window.Close();
                }));

            window.Content = Scroll(content);
            window.Show();
        }

        public static void ShowProjectInfo(RevitDocument document)
        {
            ProjectBinding binding = ProjectBindingService.Load(document);
            EmaSettings settings = LocalConfigService.LoadSettings();
            ShowInfoWindow(
                "EMA AI Project Info",
                "Current local model binding and backend target.",
                new[]
                {
                    Field("Revit Document", document != null ? document.Title : "No active document"),
                    Field("API URL", settings.ApiBaseUrl),
                    Field("Client", $"{binding.ClientName} / client_id {binding.ClientId}"),
                    Field("Project", $"{binding.ProjectTitle} / project_id {binding.ProjectId}"),
                    Field("Model Binding", binding.DescribeModelBinding()),
                    Field("Milestone", binding.CurrentMilestone),
                    Field("Landing Root", settings.LandingRoot),
                    Field("Project Folder", settings.ProjectFolderName),
                    Field("Revit Exports", LandingStandardService.GetRevitExportsFolder(settings)),
                    Field("Binding File", LocalConfigService.BindingPath)
                },
                new[]
                {
                    ActionButton("Edit Binding", () => ShowProjectBinding(document)),
                    ActionButton("Open Dashboard", OpenDashboard)
                });
        }

        public static void ShowSyncStatus()
        {
            ProjectBinding binding = ProjectBindingService.Load();
            EmaSettings settings = LocalConfigService.LoadSettings();
            Window window = CreateWindow("EMA AI Sync Status", 720, 560);
            StackPanel content = CreateRoot("Sync Status", "Local landing validation, backend health, and export status. File contents are not opened.");
            TextBlock body = AddMonospace(content, "Loading sync status...");
            AddActions(content, ActionButton("Refresh", async () => await LoadSyncStatusAsync(body, binding, settings)));
            window.Content = Scroll(content);
            window.Show();
            _ = LoadSyncStatusAsync(body, binding, settings);
        }

        public static void ShowLandingValidation()
        {
            ShowInfoWindow(
                "EMA AI Landing Validation",
                "Safe local validator. It checks folders and counts filenames only.",
                new[] { AddDetachedMonospace(LandingStandardService.BuildValidationReport(LocalConfigService.LoadSettings())) },
                new[] { ActionButton("Open Landing Folder", OpenLandingFolder) });
        }

        public static void ShowLastExport()
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            ShowInfoWindow(
                "EMA AI Last Export",
                "Latest export recorded in local settings.",
                new[]
                {
                    Field("JSON", settings.LastExportPath),
                    Field("Metadata", settings.LastExportMetadataPath),
                    Field("Exported At", settings.LastExportedAt),
                    Field("Discipline", settings.LastExportDiscipline),
                    Field("Scope", settings.LastExportScope)
                },
                new[] { ActionButton("Open Revit Exports", OpenRevitExportsFolder) });
        }

        public static void ShowBackendHealth()
        {
            Window window = CreateWindow("EMA AI Backend Health", 640, 360);
            StackPanel content = CreateRoot("Backend Health", "Runs GET /health only. No ingestion or mutation is performed.");
            TextBlock body = AddMonospace(content, "Checking backend health...");
            AddActions(content, ActionButton("Refresh", async () => await TestConnectionAsync(body)));
            window.Content = Scroll(content);
            window.Show();
            _ = TestConnectionAsync(body);
        }

        public static void ShowAiDeferred()
        {
            ShowAskAboutReport();
        }

        public static void ShowAskAboutReport()
        {
            ReportNavigatorResult discovery = ReportNavigatorService.DiscoverLatestReport(LocalConfigService.LoadSettings());
            EmaReportNavigatorWindow.ShowWindow(
                discovery,
                "ask",
                string.Empty,
                "Ask EMA AI is ready. Select or start a new chat about the loaded report.");
        }

        public static void ShowExplainSelectedIssue()
        {
            ReportNavigatorResult discovery = ReportNavigatorService.DiscoverLatestReport(LocalConfigService.LoadSettings());
            EmaReportNavigatorWindow.ShowWindow(
                discovery,
                "ask",
                string.Empty,
                "Select a requirement row first, then EMA AI can explain the selected issue.");
        }

        public static void ShowConnectionStatus()
        {
            EmaSettings settings   = LocalConfigService.LoadSettings();
            ProjectBinding binding = LocalConfigService.LoadBinding();

            bool connected =
                settings.ProjectId > 0 ||
                (!string.IsNullOrWhiteSpace(settings.ProjectFolderName) && settings.UseLandingStructure);

            string status      = connected ? "Connected" : "Not Connected";
            string projectName = FirstValue(settings.ProjectDisplayName, binding.ProjectDisplayName, "(none)");
            string projectCode = FirstValue(settings.ProjectCode,  binding.ProjectCode,  "(none)");
            string clientCode  = FirstValue(settings.ClientCode,   binding.ClientCode,   "(none)");
            string syncMode    = string.IsNullOrWhiteSpace(settings.SyncMode) ? "(not set)" : settings.SyncMode;
            string apiUrl      = string.IsNullOrWhiteSpace(settings.ApiBaseUrl)  ? "(not set)" : settings.ApiBaseUrl;
            string landingRoot = string.IsNullOrWhiteSpace(settings.LandingRoot) ? "(not set)" : settings.LandingRoot;
            string lastExport  = string.IsNullOrWhiteSpace(settings.LastExportPath)  ? "(none)"  : settings.LastExportPath;
            string lastAt      = string.IsNullOrWhiteSpace(settings.LastExportedAt)  ? "(never)" : settings.LastExportedAt;
            string projectId   = settings.ProjectId > 0 ? settings.ProjectId.ToString() : "(not set)";

            ShowInfoWindow(
                "EMA AI Connection Status",
                "Current project connection and sync configuration.",
                new FrameworkElement[]
                {
                    Field("Status",         status),
                    Field("Project Name",   projectName),
                    Field("Project Code",   projectCode),
                    Field("Client Code",    clientCode),
                    Field("Project ID",     projectId),
                    Field("Model Binding",   binding.DescribeModelBinding()),
                    Field("Sync Mode",      syncMode),
                    Field("API URL",        apiUrl),
                    Field("Landing Root",   landingRoot),
                    Field("Last Export",    lastExport),
                    Field("Last Export At", lastAt),
                    Field("Settings File",  LocalConfigService.SettingsPath),
                    Field("Binding File",   LocalConfigService.BindingPath)
                },
                new[]
                {
                    ActionButton("Close", () => { })
                });
        }

        public static void ShowConnectProjectResult(ConnectionResult result)
        {
            if (result == null)
                return;

            if (result.Success)
            {
                ShowInfoWindow(
                    "EMA AI — Project Connected",
                    result.Message,
                    new FrameworkElement[]
                    {
                        Field("Project",   result.ProjectName),
                        Field("Sync Mode", result.SyncMode),
                        Field("Next Step", "Run 'Sync Model Data' to export the Revit model.")
                    },
                    new[] { ActionButton("Close", () => { }) });
            }
            else
            {
                TaskDialogLikeInfo("EMA AI — Connect Project Failed", result.Message);
            }
        }

        public static void ShowNamingStandard()
        {
            ShowInfoWindow(
                "EMA AI Naming Standard",
                "Landing data can arrive by manual local placement, Revit plugin export, or future web upload.",
                new[] { AddDetachedMonospace(BuildNamingStandardText()) },
                new[] { ActionButton("Open Landing Folder", OpenLandingFolder) });
        }

        public static void OpenLandingFolder()
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            OpenFolderIfExists(settings.LandingRoot);
        }

        public static void OpenRevitExportsFolder()
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            OpenFolderIfExists(LandingStandardService.GetRevitExportsFolder(settings));
        }

        public static void OpenDrawingsFolder()
        {
            OpenLandingCategoryFolder(LandingStandardService.DrawingsFolderName);
        }

        public static void OpenOwnerRequirementsFolder()
        {
            OpenLandingCategoryFolder(LandingStandardService.OwnerRequirementsFolderName);
        }

        public static void OpenSpecificationsFolder()
        {
            OpenLandingCategoryFolder(LandingStandardService.SpecificationsFolderName);
        }

        public static void ShowExportComplete(
            int elementCount,
            string outputPath,
            string metadataPath,
            UploadResult uploadResult = null)
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            string syncMode = string.IsNullOrWhiteSpace(settings.SyncMode) ? "(not set)" : settings.SyncMode;
            string uploadStatus;
            string nextStep;

            if (uploadResult == null)
            {
                uploadStatus = settings.SyncMode == "cloud_upload"
                    ? "Backend upload was not attempted."
                    : "Backend upload is not part of the current sync mode.";
                nextStep = "Open the EMA AI Panel, then load Owner Requirements and run the compliance check.";
            }
            else if (uploadResult.Success)
            {
                uploadStatus = string.Format("Backend upload succeeded (HTTP {0}).", uploadResult.StatusCode);
                if (!string.IsNullOrWhiteSpace(uploadResult.ResponseBody))
                {
                    uploadStatus += Environment.NewLine + "Response: " + uploadResult.ResponseBody;
                }

                nextStep = "Open the EMA AI Panel, then load Owner Requirements and run the compliance check.";
            }
            else
            {
                uploadStatus = string.Format("Backend upload failed (HTTP {0}).", uploadResult.StatusCode);
                if (!string.IsNullOrWhiteSpace(uploadResult.Message))
                {
                    uploadStatus += Environment.NewLine + uploadResult.Message;
                }
                if (!string.IsNullOrWhiteSpace(uploadResult.ResponseBody))
                {
                    uploadStatus += Environment.NewLine + "Response: " + uploadResult.ResponseBody;
                }

                nextStep = "Keep the local export, then load Owner Requirements and run the compliance check from the EMA AI Panel.";
            }

            ShowInfoWindow(
                "EMA AI Export Complete",
                "Local export finished. Upload status is reported separately so failures are not mistaken for a completed backend sync.",
                new FrameworkElement[]
                {
                    Field("Element Count",  elementCount.ToString()),
                    Field("JSON Path",      outputPath),
                    Field("Metadata Path",  metadataPath),
                    Field("Sync Mode",      syncMode),
                    Field("Local Export",    "Created successfully."),
                    Field("Backend Upload",  uploadStatus),
                    Field("Next Step",      nextStep)
                },
                new[]
                {
                    ActionButton("Open Folder",    () => OpenFolder(Path.GetDirectoryName(outputPath))),
                    ActionButton("Copy JSON Path", () => Clipboard.SetText(outputPath)),
                    ActionButton("Close",          () => { })
                });
        }

        public static void ShowReadinessPreview()
        {
            ProjectBinding binding = ProjectBindingService.Load();
            Window window = CreateWindow("EMA AI Readiness Preview", 760, 640);
            StackPanel content = CreateRoot("Readiness Preview", "Deliverable readiness, gap summary, top gaps, and recommended actions from the backend.");
            TextBlock body = AddMonospace(content, "Loading readiness...");
            AddActions(content,
                ActionButton("Refresh", async () => await LoadReadinessAsync(body, binding)),
                ActionButton("Open Dashboard", OpenDashboard));
            window.Content = Scroll(content);
            window.Show();
            _ = LoadReadinessAsync(body, binding);
        }

        public static void ShowIssuesPanel()
        {
            ProjectBinding binding = ProjectBindingService.Load();
            Window window = CreateWindow("EMA AI Issues Panel", 820, 640);
            StackPanel content = CreateRoot("Issues Panel", "Issue review surface for QA/QC findings. Model actions are ExternalEvent-ready.");
            TextBlock body = AddMonospace(content, "Loading issues...");
            AddActions(content,
                ActionButton("Refresh", async () => await LoadIssuesAsync(body, binding)),
                ActionButton("Open Dashboard", OpenDashboard));
            window.Content = Scroll(content);
            window.Show();
            _ = LoadIssuesAsync(body, binding);
        }

        public static void ShowExportJobs()
        {
            ShowInfoWindow(
                "EMA AI Export Jobs",
                "Current in-session export jobs and progress.",
                ExportJobService.GetJobs().Take(10).Select(job =>
                    Field(job.JobId.Substring(0, 8), $"{job.Discipline} | {job.Status} | {job.Phase} | {job.ProgressPercent}% | {job.OutputPath}")),
                new[] { ActionButton("Open Export Folder", () => OpenFolder(LocalConfigService.LoadSettings().DefaultOutputFolder)) });
        }

        public static void ShowRequirementsCoverage()
        {
            ProjectBinding binding = ProjectBindingService.Load();
            ShowInfoWindow(
                "EMA AI Requirements Coverage",
                "Requirement coverage is driven by backend readiness and compliance data.",
                new[]
                {
                    Field("Project ID", binding.ProjectId.ToString()),
                    Field("Client ID", binding.ClientId.ToString()),
                    Field("Coverage API", $"/api/v1/projects/{binding.ProjectId}/compliance"),
                    Field("Evidence Status", "Covered / Missing / Needs Review / Blocked / Not Applicable"),
                    Field("Workflow", "Requirement -> Evidence -> Gap -> Action")
                },
                new[] { ActionButton("Open Readiness", () => ShowReadinessPreview()) });
        }

        public static void ShowModelHealth()
        {
            ShowInfoWindow(
                "EMA AI Model Health",
                "QA/QC support view. Readiness remains the primary delivery story.",
                new[]
                {
                    Field("R001", "Element Without Level"),
                    Field("R002", "Unconnected Fixture"),
                    Field("R003", "Fixture Missing Circuit"),
                    Field("R004", "Panel Without Source"),
                    Field("Action", "Open Issues Panel to review findings")
                },
                new[] { ActionButton("Open Issues", ShowIssuesPanel) });
        }

        public static void ShowRuleResults()
        {
            ShowInfoWindow(
                "EMA AI Rule Results",
                "Rule results are generated during backend ingestion and exposed as issues.",
                new[]
                {
                    Field("Core Rules", "R001, R002, R003, R004"),
                    Field("Rule Engine", "Backend modular rule registry foundation"),
                    Field("Result Source", "GET /api/v1/issues"),
                    Field("Next Step", "Filter issues by rule code and severity")
                },
                new[] { ActionButton("Open Issues", ShowIssuesPanel) });
        }

        public static void ShowDiagnostics(RevitDocument document)
        {
            Window window = CreateWindow("EMA AI Diagnostics / Logs", 720, 560);
            StackPanel content = CreateRoot("Diagnostics / Logs", "Pilot support information for troubleshooting the add-in.");
            TextBlock body = AddMonospace(content, DiagnosticsService.BuildDiagnostics(document));
            AddActions(content,
                ActionButton("Copy Diagnostics", () => Clipboard.SetText(body.Text)),
                ActionButton("Open Logs", LoggingService.OpenLogsFolder),
                ActionButton("Test Backend", async () => await TestConnectionAsync(body)));
            window.Content = Scroll(content);
            window.Show();
        }

        private static async Task TestConnectionAsync(TextBlock status)
        {
            try
            {
                string health = await new ApiClient().GetHealthAsync();
                status.Text = "Backend reachable: " + health;
                LoggingService.Info("Backend health check succeeded.");
            }
            catch (Exception ex)
            {
                status.Text = "Backend unavailable: " + ex.Message;
                LoggingService.Error("Backend health check failed.", ex);
            }
        }

        private static async Task LoadSyncStatusAsync(TextBlock body, ProjectBinding binding, EmaSettings settings)
        {
            string localReport = LandingStandardService.BuildValidationReport(settings);
            try
            {
                ApiClient api = new ApiClient();
                string health = await api.GetHealthAsync();
                body.Text =
                    localReport + Environment.NewLine +
                    $"Backend health: {health}{Environment.NewLine}" +
                    $"Project ID: {binding.ProjectId}{Environment.NewLine}" +
                    "Backend ingestion status: not_submitted by plugin";
            }
            catch (Exception ex)
            {
                body.Text = localReport + Environment.NewLine + "Backend health unavailable: " + ex.Message;
                LoggingService.Error("Sync status load failed.", ex);
            }
        }

        private static async Task LoadReadinessAsync(TextBlock body, ProjectBinding binding)
        {
            try
            {
                ReadinessDto readiness = await new ApiClient().GetReadinessAsync(binding.ProjectId);
                body.Text =
                    $"Overall readiness: {readiness.overall_readiness}% ({readiness.label}){Environment.NewLine}" +
                    $"Gap summary: {string.Join(", ", readiness.gap_summary.Select(kvp => kvp.Key + "=" + kvp.Value))}{Environment.NewLine}{Environment.NewLine}" +
                    "Top gaps:" + Environment.NewLine +
                    string.Join(Environment.NewLine, readiness.top_gaps.Take(6).Select(g => $"- {g.rule_code} [{g.severity}] {g.discipline}: {g.message}")) +
                    Environment.NewLine + Environment.NewLine +
                    "Recommended actions:" + Environment.NewLine +
                    string.Join(Environment.NewLine, readiness.recommended_actions.Take(6).Select(a => $"- {a.label} [{a.severity}] {a.detail}")) +
                    Environment.NewLine + Environment.NewLine +
                    "Trade readiness:" + Environment.NewLine +
                    string.Join(Environment.NewLine, readiness.trade_readiness.Select(t => $"- {t.discipline}: {t.readiness}% ({t.label})"));
            }
            catch (Exception ex)
            {
                body.Text = "Readiness is not available. Bind the model and confirm backend is running. " + ex.Message;
                LoggingService.Error("Readiness load failed.", ex);
            }
        }

        private static async Task LoadIssuesAsync(TextBlock body, ProjectBinding binding)
        {
            try
            {
                IssueListDto issues = await new ApiClient().GetIssuesAsync(binding.ProjectId, 25);
                body.Text =
                    $"Total issues: {issues.total}{Environment.NewLine}{Environment.NewLine}" +
                    string.Join(Environment.NewLine, issues.items.Select(i =>
                        $"#{i.id} | {i.severity} | {i.rule_code} | {i.status} | {i.element_unique_id} | {i.message}"));
            }
            catch (Exception ex)
            {
                body.Text = "Could not load issues: " + ex.Message;
                LoggingService.Error("Issues load failed.", ex);
            }
        }

        private static Window CreateWindow(string title, double width, double height)
        {
            return new Window
            {
                Title = title,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize,
                Topmost = false
            };
        }

        private static StackPanel CreateRoot(string title, string subtitle)
        {
            StackPanel content = new StackPanel { Margin = new Thickness(16) };
            content.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 4)
            });
            content.Children.Add(new TextBlock
            {
                Text = subtitle,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(100, 116, 139),
                Margin = new Thickness(0, 0, 0, 16)
            });
            return content;
        }

        private static ScrollViewer Scroll(UIElement child)
        {
            return new ScrollViewer { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private static TextBox AddTextBox(StackPanel content, string label, string value)
        {
            AddLabel(content, label);
            TextBox box = new TextBox { Text = value ?? "", Margin = new Thickness(0, 0, 0, 10), MinHeight = 26 };
            content.Children.Add(box);
            return box;
        }

        private static ComboBox AddCombo(StackPanel content, string label, string[] values, string selected)
        {
            AddLabel(content, label);
            ComboBox combo = new ComboBox { IsEditable = false, Margin = new Thickness(0, 0, 0, 10), MinHeight = 26 };
            foreach (string value in values)
            {
                combo.Items.Add(value);
            }
            combo.SelectedItem = values.Contains(selected) ? selected : values.FirstOrDefault();
            content.Children.Add(combo);
            return combo;
        }

        private static CheckBox AddCheckBox(StackPanel content, string label, bool isChecked)
        {
            CheckBox box = new CheckBox { Content = label, IsChecked = isChecked, Margin = new Thickness(0, 0, 0, 10) };
            content.Children.Add(box);
            return box;
        }

        private static TextBlock AddStatus(StackPanel content)
        {
            TextBlock status = AddMonospace(content, "");
            status.Foreground = Brush(15, 118, 110);
            return status;
        }

        private static TextBlock AddMonospace(StackPanel content, string text)
        {
            TextBlock block = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brush(30, 41, 59),
                Margin = new Thickness(0, 6, 0, 10)
            };
            content.Children.Add(block);
            return block;
        }

        private static TextBlock AddDetachedMonospace(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brush(30, 41, 59),
                Margin = new Thickness(0, 6, 0, 10)
            };
        }

        private static void AddLabel(StackPanel content, string label)
        {
            content.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(71, 85, 105),
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        private static void AddReadOnly(StackPanel content, string label, string value)
        {
            content.Children.Add(Field(label, value));
        }

        private static void AddActions(StackPanel content, params Button[] buttons)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            foreach (Button button in buttons)
            {
                row.Children.Add(button);
            }
            content.Children.Add(row);
        }

        private static void ShowInfoWindow(
            string title,
            string subtitle,
            IEnumerable<FrameworkElement> fields,
            IEnumerable<Button> actions)
        {
            Window window = CreateWindow(title, 620, 520);
            StackPanel content = CreateRoot(title, subtitle);
            foreach (FrameworkElement field in fields)
            {
                content.Children.Add(field);
            }
            AddActions(content, actions.ToArray());
            window.Content = Scroll(content);
            window.Show();
        }

        private static FrameworkElement Field(string label, string value)
        {
            Grid grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(71, 85, 105)
            };
            TextBlock valueBlock = new TextBlock
            {
                Text = value ?? "",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(15, 23, 42)
            };

            Grid.SetColumn(labelBlock, 0);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
            return grid;
        }

        private static Button ActionButton(string label, Action onClick)
        {
            Button button = new Button
            {
                Content = label,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                MinWidth = 96
            };
            button.Click += (sender, args) => onClick();
            return button;
        }

        private static Button ActionButton(string label, Func<Task> onClick)
        {
            Button button = ActionButton(label, () => { });
            button.Click += async (sender, args) =>
            {
                button.IsEnabled = false;
                try
                {
                    await onClick();
                }
                finally
                {
                    button.IsEnabled = true;
                }
            };
            return button;
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static string FirstValue(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return "";
        }

        private static string BuildRevitExportsPreview(EmaSettings settings, string projectFolderName)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.LandingRoot) || string.IsNullOrWhiteSpace(projectFolderName))
            {
                return "Preview: configure Landing Root and Project Folder Name.";
            }

            return "Preview: " + Path.Combine(settings.LandingRoot, projectFolderName, LandingStandardService.RevitExportsFolderName);
        }

        private static string BuildNamingStandardText()
        {
            return
                "Project folder standard:\n" +
                "landing/<PROJECT_DISPLAY_NAME>/\n" +
                "  Drawings/\n" +
                "  Owner Requirements/\n" +
                "  Specifications/\n" +
                "  Revit Exports/\n" +
                "  landing_manifest.json\n\n" +
                "Revit export JSON:\n" +
                "<project_slug>__revit_export__<discipline>__<scope>__<yyyyMMdd_HHmmss>.json\n\n" +
                "Revit export metadata:\n" +
                "<same_export_filename_without_json>.meta.json\n\n" +
                "Owner Requirements:\n" +
                "<project_slug>__owner_requirements__<client_or_district>__<yyyyMMdd>.xlsx\n\n" +
                "Drawings:\n" +
                "<project_slug>__drawings__<discipline_code>__<sheet_set_or_package>__<yyyyMMdd_or_unknown>.pdf\n\n" +
                "Specifications:\n" +
                "<project_slug>__specification__<csi_section>__<section_title_slug>.pdf\n\n" +
                "Paths:\n" +
                "A. Manual local placement into Drawings, Owner Requirements, Specifications, or Revit Exports.\n" +
                "B. Revit plugin export writes JSON plus .meta.json into Revit Exports.\n" +
                "C. Future web app upload should target the same categories or ADLS equivalent.\n\n" +
                "MVP note: keep original files. Use manifest/index metadata later to map original_filename to standardized_filename.";
        }

        private static void BrowseFolderInto(TextBox textBox)
        {
            using (System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.SelectedPath = textBox.Text;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    textBox.Text = dialog.SelectedPath;
                }
            }
        }

        private static void OpenLandingCategoryFolder(string categoryFolderName)
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            string projectFolder = LandingStandardService.GetLandingProjectFolder(settings);
            OpenFolderIfExists(string.IsNullOrWhiteSpace(projectFolder) ? "" : Path.Combine(projectFolder, categoryFolderName));
        }

        private static void OpenFolderIfExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Folder is not configured or does not exist:\n" + (path ?? ""), "EMA AI");
                return;
            }

            OpenFolder(path);
        }

        private static void TaskDialogLikeInfo(string title, string message)
        {
            MessageBox.Show(message, title);
        }

        private static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private static void OpenDashboard()
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            string url = settings.DashboardUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = EmaSettings.GetDashboardUrlForEnvironment(settings.GetEnvironmentLabel());
            }
            OpenUrl(url);
        }

        private static void OpenFolder(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
