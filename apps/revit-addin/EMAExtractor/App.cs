using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using System.Windows.Media.Imaging;

namespace EMAExtractor
{
    public class App : IExternalApplication
    {
        private const string TabName = "EMA AI";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                TryCreateRibbonTab(application, TabName);

                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                CreateOwnerRequirementsWorkflowPanel(application, assemblyPath);
                CreateReportPanel(application, assemblyPath);
                CreateAskEmaAiPanel(application, assemblyPath);
                CreateSupportPanel(application, assemblyPath);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("EMA AI Startup Error", ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private void CreateOwnerRequirementsWorkflowPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = application.CreateRibbonPanel(TabName, "Owner Requirements Workflow");

            AddPushButton(
                panel,
                "OpenWorkflow",
                "Open\nWorkflow",
                assemblyPath,
                "EMAExtractor.Commands.Dashboard.OpenEmaPanelCommand",
                "Open the EMA AI Owner Requirements Workflow panel.",
                "ask_ai_32.png");

            AddPushButton(
                panel,
                "LoadOwnerRequirements",
                "Load\nRequirements",
                assemblyPath,
                "EMAExtractor.Commands.RequirementsReadiness.LoadRequirementsCommand",
                "Select the Owner Requirements workbook for this project.",
                "load_project_rules_32.png");

            AddPushButton(
                panel,
                "SyncModelData",
                "Sync Model\nData",
                assemblyPath,
                "EMAExtractor.Commands.SyncExport.SyncModelDataCommand",
                "Capture the current Revit model evidence for the requirements check.",
                "quick_sync_32.png");

            AddPushButton(
                panel,
                "RunComplianceCheck",
                "Run\nCheck",
                assemblyPath,
                "EMAExtractor.Commands.RequirementsReadiness.CheckRequirementsCommand",
                "Compare loaded Owner Requirements against synced Revit model evidence and generate a review report.",
                "run_compliance_32.png");
        }

        private void CreateReportPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = application.CreateRibbonPanel(TabName, "Report");

            AddPushButton(
                panel,
                "OpenReportNavigator",
                "Report\nNavigator",
                assemblyPath,
                "EMAExtractor.Commands.Reports.OpenReportNavigatorCommand",
                "Open the latest EMA AI Owner Requirements report inside Revit.",
                "open_issues_panel_32.png");

            AddPushButton(
                panel,
                "OpenReport",
                "Open\nBrowser",
                assemblyPath,
                "EMAExtractor.Commands.RequirementsReadiness.OpenLastRequirementReportCommand",
                "Open the most recent Owner Requirements review report in the default browser.",
                "project_info_32.png");

            AddPushButton(
                panel,
                "ExportPdf",
                "Export PDF",
                assemblyPath,
                "EMAExtractor.Commands.RequirementsReadiness.ExportPdfCommand",
                "Export or print the latest report as PDF.",
                "export_32.png");

            AddPushButton(
                panel,
                "CopySummary",
                "Copy\nSummary",
                assemblyPath,
                "EMAExtractor.Commands.RequirementsReadiness.CopyRequirementSummaryCommand",
                "Copy the latest report summary to the clipboard.",
                "mark_reviewed_32.png");
        }

        private void CreateAskEmaAiPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = application.CreateRibbonPanel(TabName, "Ask EMA AI");

            AddPushButton(
                panel,
                "AskAboutReport",
                "Ask\nReport",
                assemblyPath,
                "EMAExtractor.Commands.Ai.AskAboutReportCommand",
                "Ask EMA AI questions about the latest report, key issues, evidence, and next actions.",
                "ask_ai_32.png");

            AddPushButton(
                panel,
                "ExplainSelectedIssue",
                "Explain\nIssue",
                assemblyPath,
                "EMAExtractor.Commands.Ai.ExplainSelectedIssueCommand",
                "View a detailed explanation of the top key issue including status, evidence, reasoning, and next action.",
                "open_issues_panel_32.png");
        }

        private void CreateSupportPanel(UIControlledApplication application, string assemblyPath)
        {
            RibbonPanel panel = application.CreateRibbonPanel(TabName, "Support");

            AddPushButton(
                panel,
                "Diagnostics",
                "Diagnostics",
                assemblyPath,
                "EMAExtractor.Commands.Settings.DiagnosticsCommand",
                "View connection, settings, project binding, and support information.",
                "settings_32.png");

            AddPushButton(
                panel,
                "Settings",
                "Settings",
                assemblyPath,
                "EMAExtractor.Commands.Settings.SettingsCommand",
                "Configure Local, LAN, or Cloud environment profile.",
                "settings_32.png");

            AddPushButton(
                panel,
                "OpenDashboard",
                "Dashboard",
                assemblyPath,
                "EMAExtractor.Commands.Help.OpenDashboardCommand",
                "Open the optional EMA AI dashboard in a browser.",
                "project_info_32.png");
        }

        private void TryCreateRibbonTab(UIControlledApplication application, string tabName)
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch
            {
                // Tab already exists — safe to ignore.
            }
        }

        private PushButton AddPushButton(
            RibbonPanel panel,
            string internalName,
            string buttonText,
            string assemblyPath,
            string className,
            string tooltip,
            string iconFileName)
        {
            PushButtonData data = new PushButtonData(
                internalName,
                buttonText,
                assemblyPath,
                className);

            PushButton button = panel.AddItem(data) as PushButton;

            if (button != null)
            {
                button.ToolTip = tooltip;

                BitmapImage icon = LoadIcon(iconFileName);
                if (icon != null)
                {
                    button.LargeImage = icon;
                    button.Image = icon;
                }
            }

            return button;
        }

        private BitmapImage LoadIcon(string iconFileName)
        {
            try
            {
                string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string iconPath = Path.Combine(assemblyDirectory, "Resources", "Icons", iconFileName);

                if (!File.Exists(iconPath))
                {
                    return null;
                }

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
