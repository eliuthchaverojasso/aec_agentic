using System;
using System.IO;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Models;
using EMAExtractor.Services;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace EMAExtractor.Commands.ProjectSetup
{
    [Transaction(TransactionMode.Manual)]
    public class ConnectProjectCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                EmaSettings existing = LocalConfigService.LoadSettings();

                using (ConnectProjectForm form = new ConnectProjectForm(existing))
                {
                    WinForms.DialogResult result = form.ShowDialog();
                    return result == WinForms.DialogResult.OK ? Result.Succeeded : Result.Cancelled;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("EMA AI Connect Project", "Failed to connect project.\n\n" + ex.Message);
                LoggingService.Error("Connect Project failed.", ex);
                return Result.Failed;
            }
        }
    }

    internal class ConnectProjectForm : WinForms.Form
    {
        private readonly WinForms.TextBox apiText = new WinForms.TextBox();
        private readonly WinForms.TextBox dashboardText = new WinForms.TextBox();
        private readonly WinForms.ComboBox projectCombo = new WinForms.ComboBox();
        private readonly WinForms.ComboBox syncModeCombo = new WinForms.ComboBox();
        private readonly WinForms.TextBox landingRootText = new WinForms.TextBox();
        private readonly WinForms.Button testButton = new WinForms.Button();
        private readonly WinForms.Button refreshButton = new WinForms.Button();
        private readonly WinForms.Button browseButton = new WinForms.Button();
        private readonly WinForms.Button saveButton = new WinForms.Button();
        private readonly WinForms.Button cancelButton = new WinForms.Button();
        private readonly WinForms.Label statusLabel = new WinForms.Label();

        public ConnectProjectForm(EmaSettings existing)
        {
            Text = "Connect EMA AI Project";
            Width = 760;
            Height = 500;
            StartPosition = WinForms.FormStartPosition.CenterScreen;
            FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            WinForms.Label title = new WinForms.Label
            {
                Text = "Connect to EMA AI Backend",
                Font = new Drawing.Font("Segoe UI", 14, Drawing.FontStyle.Bold),
                Left = 20,
                Top = 20,
                Width = 700,
                Height = 30
            };

            WinForms.Label apiLabel = new WinForms.Label
            {
                Text = "API Base URL",
                Left = 20,
                Top = 70,
                Width = 160
            };

            apiText.Left = 190;
            apiText.Top = 66;
            apiText.Width = 500;
            apiText.Text = string.IsNullOrWhiteSpace(existing.ApiBaseUrl)
                ? EmaSettings.GetApiBaseUrlForEnvironment(existing.GetEnvironmentLabel())
                : existing.ApiBaseUrl;

            WinForms.Label dashLabel = new WinForms.Label
            {
                Text = "Dashboard URL",
                Left = 20,
                Top = 110,
                Width = 160
            };

            dashboardText.Left = 190;
            dashboardText.Top = 106;
            dashboardText.Width = 500;
            dashboardText.Text = string.IsNullOrWhiteSpace(existing.DashboardUrl)
                ? EmaSettings.GetDashboardUrlForEnvironment(existing.GetEnvironmentLabel())
                : existing.DashboardUrl;

            testButton.Text = "Test Connection";
            testButton.Left = 190;
            testButton.Top = 145;
            testButton.Width = 150;
            testButton.Click += (s, e) => TestConnection();

            refreshButton.Text = "Refresh Projects";
            refreshButton.Left = 350;
            refreshButton.Top = 145;
            refreshButton.Width = 150;
            refreshButton.Click += (s, e) => RefreshProjects();

            WinForms.Label projectLabel = new WinForms.Label
            {
                Text = "Project",
                Left = 20,
                Top = 195,
                Width = 160
            };

            projectCombo.Left = 190;
            projectCombo.Top = 190;
            projectCombo.Width = 500;
            projectCombo.DropDownStyle = WinForms.ComboBoxStyle.DropDownList;
            projectCombo.DisplayMember = "DisplayLabel";

            WinForms.Label syncLabel = new WinForms.Label
            {
                Text = "Sync Mode",
                Left = 20,
                Top = 235,
                Width = 160
            };

            syncModeCombo.Left = 190;
            syncModeCombo.Top = 230;
            syncModeCombo.Width = 200;
            syncModeCombo.DropDownStyle = WinForms.ComboBoxStyle.DropDownList;
            syncModeCombo.Items.Add("cloud_upload");
            syncModeCombo.Items.Add("local_landing");

            string savedSyncMode = string.IsNullOrWhiteSpace(existing.SyncMode)
                ? "cloud_upload"
                : existing.SyncMode;

            syncModeCombo.SelectedItem = savedSyncMode;
            if (syncModeCombo.SelectedItem == null)
            {
                syncModeCombo.SelectedIndex = 0;
            }

            syncModeCombo.SelectedIndexChanged += (s, e) => UpdateLandingVisibility();

            WinForms.Label landingLabel = new WinForms.Label
            {
                Text = "Landing Root",
                Left = 20,
                Top = 275,
                Width = 160
            };

            landingRootText.Left = 190;
            landingRootText.Top = 270;
            landingRootText.Width = 410;
            landingRootText.Text = string.IsNullOrWhiteSpace(existing.LandingRoot)
                ? "C:\\EMA-Landing"
                : existing.LandingRoot;

            browseButton.Text = "Browse";
            browseButton.Left = 610;
            browseButton.Top = 268;
            browseButton.Width = 80;
            browseButton.Click += (s, e) =>
            {
                using (WinForms.FolderBrowserDialog dlg = new WinForms.FolderBrowserDialog())
                {
                    dlg.Description = "Select EMA AI landing root";

                    if (Directory.Exists(landingRootText.Text))
                    {
                        dlg.SelectedPath = landingRootText.Text;
                    }

                    if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                    {
                        landingRootText.Text = dlg.SelectedPath;
                    }
                }
            };

            statusLabel.Left = 20;
            statusLabel.Top = 320;
            statusLabel.Width = 700;
            statusLabel.Height = 60;
            statusLabel.Text = "Click Test Connection, then Refresh Projects.";
            statusLabel.ForeColor = Drawing.Color.DimGray;

            saveButton.Text = "Save Connection";
            saveButton.Left = 450;
            saveButton.Top = 400;
            saveButton.Width = 130;
            saveButton.Click += (s, e) => SaveConnection();

            cancelButton.Text = "Cancel";
            cancelButton.Left = 590;
            cancelButton.Top = 400;
            cancelButton.Width = 100;
            cancelButton.Click += (s, e) =>
            {
                DialogResult = WinForms.DialogResult.Cancel;
                Close();
            };

            Controls.Add(title);
            Controls.Add(apiLabel);
            Controls.Add(apiText);
            Controls.Add(dashLabel);
            Controls.Add(dashboardText);
            Controls.Add(testButton);
            Controls.Add(refreshButton);
            Controls.Add(projectLabel);
            Controls.Add(projectCombo);
            Controls.Add(syncLabel);
            Controls.Add(syncModeCombo);
            Controls.Add(landingLabel);
            Controls.Add(landingRootText);
            Controls.Add(browseButton);
            Controls.Add(statusLabel);
            Controls.Add(saveButton);
            Controls.Add(cancelButton);

            UpdateLandingVisibility();
        }

        private void TestConnection()
        {
            SetBusy("Testing backend connection...");

            BackendProjectFetchResult result = Task
                .Run(() => BackendProjectService.TestConnectionAsync(apiText.Text))
                .GetAwaiter()
                .GetResult();

            statusLabel.Text = result.Message;
            statusLabel.ForeColor = result.Ok ? Drawing.Color.Green : Drawing.Color.DarkRed;
        }

        private void RefreshProjects()
        {
            SetBusy("Loading projects from backend...");

            BackendProjectFetchResult result = Task
                .Run(() => BackendProjectService.GetProjectsAsync(apiText.Text))
                .GetAwaiter()
                .GetResult();

            projectCombo.DataSource = null;
            projectCombo.Items.Clear();

            if (!result.Ok)
            {
                statusLabel.Text = result.Message;
                statusLabel.ForeColor = Drawing.Color.DarkRed;
                return;
            }

            projectCombo.DataSource = result.Projects;
            projectCombo.DisplayMember = "DisplayLabel";

            if (result.Projects.Count > 0)
            {
                projectCombo.SelectedIndex = 0;
            }

            statusLabel.Text = result.Message;
            statusLabel.ForeColor = Drawing.Color.Green;
        }

        private void SaveConnection()
        {
            BackendProjectOption selected = projectCombo.SelectedItem as BackendProjectOption;

            if (selected == null)
            {
                WinForms.MessageBox.Show(
                    "Select a project loaded from the backend first.",
                    "EMA AI",
                    WinForms.MessageBoxButtons.OK,
                    WinForms.MessageBoxIcon.Warning);
                return;
            }

            string syncMode = Convert.ToString(syncModeCombo.SelectedItem) ?? "cloud_upload";
            string apiBaseUrl = apiText.Text.Trim().TrimEnd('/');
            string dashboardUrl = dashboardText.Text.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                WinForms.MessageBox.Show(
                    "API Base URL is required.",
                    "EMA AI",
                    WinForms.MessageBoxButtons.OK,
                    WinForms.MessageBoxIcon.Warning);
                return;
            }

            if (syncMode == "local_landing" && string.IsNullOrWhiteSpace(landingRootText.Text))
            {
                WinForms.MessageBox.Show(
                    "Landing Root is required for local_landing mode.",
                    "EMA AI",
                    WinForms.MessageBoxButtons.OK,
                    WinForms.MessageBoxIcon.Warning);
                return;
            }

            EmaSettings settings = LocalConfigService.LoadSettings();
            settings.ApiBaseUrl = apiBaseUrl;
            settings.DashboardUrl = string.IsNullOrWhiteSpace(dashboardUrl) ? apiBaseUrl : dashboardUrl;
            settings.EnvironmentName = EmaSettings.NormalizeEnvironmentName(null, apiBaseUrl);
            settings.ProjectId = selected.ProjectId;
            settings.ProjectDisplayName = selected.ProjectName;
            settings.ProjectCode = selected.ProjectCode;
            settings.ClientCode = selected.ClientCode;
            settings.ProjectFolderName = selected.ProjectFolderName;
            settings.SyncMode = syncMode;
            settings.ExportProfile = "Standard";
            settings.AutoSubmitToBackend = syncMode == "cloud_upload";
            settings.UseLandingStructure = syncMode == "local_landing";
            settings.LandingRoot = syncMode == "local_landing" ? landingRootText.Text.Trim() : "";

            if (settings.UseLandingStructure)
            {
                LandingStandardService.EnsureLandingFoldersForExport(settings);
            }

            LocalConfigService.SaveSettings(settings);

            ProjectBinding binding = LocalConfigService.LoadBinding();
            binding.ProjectId = selected.ProjectId;
            binding.ClientId = selected.ClientId ?? 0;
            binding.ClientName = selected.ClientName;
            binding.ClientCode = selected.ClientCode;
            binding.ProjectTitle = selected.ProjectName;
            binding.ProjectDisplayName = selected.ProjectName;
            binding.ProjectCode = selected.ProjectCode;
            binding.ProjectFolderName = selected.ProjectFolderName;

            LocalConfigService.SaveBinding(binding);

            WinForms.MessageBox.Show(
                "Connected to EMA AI project:\n\n" +
                selected.ProjectName + "\n\n" +
                "Project ID: " + selected.ProjectId + "\n" +
                "Sync Mode: " + syncMode + "\n" +
                "API: " + apiBaseUrl,
                "EMA AI",
                WinForms.MessageBoxButtons.OK,
                WinForms.MessageBoxIcon.Information);

            DialogResult = WinForms.DialogResult.OK;
            Close();
        }

        private void SetBusy(string text)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = Drawing.Color.DimGray;
            Refresh();
        }

        private void UpdateLandingVisibility()
        {
            bool local = Convert.ToString(syncModeCombo.SelectedItem) == "local_landing";
            landingRootText.Enabled = local;
            browseButton.Enabled = local;
        }
    }
}
