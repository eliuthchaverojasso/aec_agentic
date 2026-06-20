using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Core;
using EMAExtractor.Enums;
using EMAExtractor.Models;
using EMAExtractor.Services;
using EMAExtractor.UI.ViewModels;

namespace EMAExtractor.UI
{
    public class EmaDashboardWindow : Window
    {
        private static EmaDashboardWindow _instance;

        private readonly ExternalCommandData _commandData;
        private readonly Document _document;
        private EmaDashboardViewModel _viewModel;

        private EmaDashboardWindow(ExternalCommandData commandData)
        {
            _commandData = commandData;
            _document = commandData?.Application?.ActiveUIDocument?.Document;

            Title = "EMA AI Owner Requirements Workflow";
            Width = 1120;
            Height = 900;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            FontFamily = new FontFamily("Segoe UI");

            Content = BuildShell();
            Loaded += async (sender, args) => await RefreshAsync();
            Closed += (sender, args) => { if (ReferenceEquals(_instance, this)) _instance = null; };
        }

        public static void ShowWindow(ExternalCommandData commandData)
        {
            if (_instance != null)
            {
                if (_instance.WindowState == WindowState.Minimized)
                {
                    _instance.WindowState = WindowState.Normal;
                }

                _instance.Activate();
                _instance.Topmost = true;
                _instance.Topmost = false;
                _instance.Focus();
                _ = _instance.RefreshAsync();
                return;
            }

            _instance = new EmaDashboardWindow(commandData);
            _instance.Show();
        }

        private UIElement BuildShell()
        {
            EmaDashboardViewModel viewModel = _viewModel;

            StackPanel root = new StackPanel
            {
                Margin = new Thickness(24, 18, 24, 18),
                MaxWidth = 960
            };

            root.Children.Add(BuildHeader(viewModel));
            root.Children.Add(BuildWorkflowCard(
                1,
                "Load Requirements",
                "Select the Owner Requirements workbook.",
                viewModel == null ? "Not loaded" : viewModel.RequirementsStatus,
                new[]
                {
                    Tuple.Create("Workbook", DisplayText(viewModel?.RequirementsWorkbookName, "(not loaded)")),
                    Tuple.Create("Rows", DisplayText(viewModel?.RequirementsRowCountText, "(not loaded)")),
                    Tuple.Create("Disciplines", DisplayText(viewModel?.RequirementsDisciplines, "(not detected)")),
                    Tuple.Create("Loaded at", DisplayText(viewModel?.RequirementsLoadedAt, "(never)"))
                },
                "Load Requirements",
                LoadRequirements,
                false,
                null,
                false));

            root.Children.Add(BuildWorkflowCard(
                2,
                "Sync Model Data",
                "Capture current Revit model evidence.",
                viewModel == null ? "Not synced" : viewModel.ModelSyncStatus,
                new[]
                {
                    Tuple.Create("Elements", DisplayText(viewModel?.ModelSyncElementCountText, "(not synced)")),
                    Tuple.Create("Synced at", DisplayText(viewModel?.ModelSyncAt, "(never)")),
                    Tuple.Create("Note", DisplayText(viewModel?.ModelSyncMessage, "(no sync notes)"))
                },
                "Sync Model Data",
                SyncModelData,
                false,
                null,
                false));

            string complianceButtonText = viewModel != null && viewModel.HasGeneratedReport ? "Run Again" : "Run Compliance Check";
            root.Children.Add(BuildWorkflowCard(
                3,
                "Run Compliance Check",
                "Compare requirements against model evidence and generate the report.",
                viewModel == null ? "Waiting" : viewModel.ComplianceStatus,
                new[]
                {
                    Tuple.Create("Discipline", DisplayText(viewModel?.Settings?.LastRequirementsSelectedDiscipline, "(not set)")),
                    Tuple.Create("Scope", DisplayText(viewModel?.Settings?.LastRequirementsSelectedScope, "(not set)"))
                },
                complianceButtonText,
                RunComplianceCheck,
                viewModel == null || !viewModel.CanRunComplianceCheck,
                viewModel == null ? "Load requirements and sync model data first." : viewModel.ComplianceDisabledReason,
                true));

            if (viewModel != null && viewModel.HasGeneratedReport)
            {
                root.Children.Add(BuildActionStrip());
            }

            root.Children.Add(BuildAdvancedSection(viewModel));

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = root
            };
        }

        private Border BuildHeader(EmaDashboardViewModel viewModel)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Owner Requirements Workflow",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 2)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Load requirements, sync model data, run the check, and generate a report.",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Workflow breadcrumb
            stack.Children.Add(new TextBlock
            {
                Text = "Load Requirements  →  Sync Model Data  →  Run Compliance Check  →  Report  →  Ask EMA AI",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Status chips row
            WrapPanel chips = new WrapPanel();
            chips.Children.Add(BuildChip("Connection", DisplayText(viewModel?.ConnectionStatus, "Not Connected"),
                viewModel != null && viewModel.ConnectionStatus == "Connected"));
            chips.Children.Add(BuildChip("Environment", DisplayText(viewModel?.EnvironmentLabel, "Local"), true));
            chips.Children.Add(BuildChip("Project / Model", DisplayText(viewModel?.ProjectSummary, "(not set)"),
                viewModel != null && !string.IsNullOrWhiteSpace(viewModel.ProjectName) && viewModel.ProjectName != "(none)"));
            chips.Children.Add(BuildChip("Requirements", DisplayText(viewModel?.RequirementsStatus, "Not loaded"),
                viewModel != null && viewModel.RequirementsStatus == "Loaded"));
            chips.Children.Add(BuildChip("Model Sync", DisplayText(viewModel?.ModelSyncStatus, "Not synced"),
                viewModel != null && viewModel.ModelSyncStatus == "Synced"));
            chips.Children.Add(BuildChip("Report",
                viewModel != null && viewModel.HasGeneratedReport ? "Ready" : "Not generated",
                viewModel != null && viewModel.HasGeneratedReport));

            stack.Children.Add(chips);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 14),
                Child = stack
            };
        }

        private Border BuildWorkflowCard(
            int stepNumber,
            string title,
            string description,
            string status,
            Tuple<string, string>[] fields,
            string buttonText,
            Action action,
            bool disabled,
            string disabledTooltip,
            bool isPrimary)
        {
            StackPanel stack = new StackPanel();

            // Header row: step label + title + status badge
            DockPanel header = new DockPanel
            {
                LastChildFill = false,
                Margin = new Thickness(0, 0, 0, 8)
            };

            StackPanel heading = new StackPanel();
            heading.Children.Add(new TextBlock
            {
                Text = "Step " + stepNumber.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235))
            });
            heading.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 1, 0, 0)
            });
            heading.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            DockPanel.SetDock(heading, Dock.Left);
            header.Children.Add(heading);

            // Status badge with contextual color
            Color badgeBg = Color.FromRgb(239, 246, 255);
            Color badgeFg = Color.FromRgb(29, 78, 216);
            Color badgeBorder = Color.FromRgb(191, 219, 254);
            string statusLower = (status ?? "").ToLowerInvariant();
            if (statusLower == "loaded" || statusLower == "synced" || statusLower == "completed" || statusLower == "ready")
            {
                badgeBg = Color.FromRgb(220, 252, 231);
                badgeFg = Color.FromRgb(22, 101, 52);
                badgeBorder = Color.FromRgb(187, 247, 208);
            }
            else if (statusLower == "failed" || statusLower == "error")
            {
                badgeBg = Color.FromRgb(254, 226, 226);
                badgeFg = Color.FromRgb(153, 27, 27);
                badgeBorder = Color.FromRgb(252, 165, 165);
            }

            Border statusBadge = new Border
            {
                Background = new SolidColorBrush(badgeBg),
                BorderBrush = new SolidColorBrush(badgeBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = status,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(badgeFg),
                    FontWeight = FontWeights.SemiBold
                }
            };
            DockPanel.SetDock(statusBadge, Dock.Right);
            header.Children.Add(statusBadge);

            stack.Children.Add(header);

            // Compact field row using WrapPanel for inline display
            WrapPanel fieldRow = new WrapPanel
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            foreach (Tuple<string, string> field in fields)
            {
                fieldRow.Children.Add(BuildCompactField(field.Item1, field.Item2));
            }
            stack.Children.Add(fieldRow);

            // Right-aligned button
            DockPanel buttonRow = new DockPanel { LastChildFill = false };
            Button button = isPrimary ? CreateActionButton(buttonText, action) : CreateSecondaryButton(buttonText, action);
            button.IsEnabled = !disabled;
            button.MinWidth = 0;
            button.HorizontalAlignment = HorizontalAlignment.Right;
            if (disabled && !string.IsNullOrWhiteSpace(disabledTooltip))
            {
                ToolTipService.SetToolTip(button, disabledTooltip);
            }
            DockPanel.SetDock(button, Dock.Right);
            buttonRow.Children.Add(button);
            stack.Children.Add(buttonRow);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 10),
                Child = stack
            };
        }

        private Border BuildActionStrip()
        {
            DockPanel header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
            header.Children.Add(new TextBlock
            {
                Text = "Report Actions",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            });

            WrapPanel actions = new WrapPanel();
            actions.Children.Add(CreateActionButton("Open Report", OpenLastReport));
            actions.Children.Add(CreateSecondaryButton("Export PDF", ExportPdf));
            actions.Children.Add(CreateSecondaryButton("Copy Summary", CopySummary));
            actions.Children.Add(CreateSecondaryButton("Ask EMA AI", AskAboutReport));

            StackPanel stack = new StackPanel();
            stack.Children.Add(header);
            stack.Children.Add(actions);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10),
                Child = stack
            };
        }

        private Expander BuildAdvancedSection(EmaDashboardViewModel viewModel)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(BuildMiniCard("Project Binding", new[]
            {
                Tuple.Create("Binding", DisplayText(BuildBindingSummary(viewModel), "(loading)")),
                Tuple.Create("Project ID", DisplayText(viewModel?.ProjectIdText, "(not set)")),
                Tuple.Create("Client ID", DisplayText(viewModel?.ClientIdText, "(not set)")),
                Tuple.Create("Model Name", DisplayText(viewModel?.ModelName, "(not set)"))
            }));

            stack.Children.Add(BuildMiniCard("Advanced URLs", new[]
            {
                Tuple.Create("API URL", DisplayText(viewModel?.ApiBaseUrl, "(loading)")),
                Tuple.Create("Dashboard URL", DisplayText(viewModel?.DashboardUrl, "(loading)")),
                Tuple.Create("Settings File", LocalConfigService.SettingsPath)
            }));

            stack.Children.Add(BuildMiniCard("Backend Health / Readiness", new[]
            {
                Tuple.Create("Health", DisplayText(viewModel?.BackendHealth, "(loading)")),
                Tuple.Create("Readiness Score", DisplayText(viewModel?.ReadinessScore, "(loading)")),
                Tuple.Create("Readiness Label", DisplayText(viewModel?.ReadinessLabel, "(loading)")),
                Tuple.Create("Issues", DisplayText(viewModel?.IssuesSummary, "(loading)"))
            }));

            stack.Children.Add(BuildMiniCard("Last Report Snapshot", new[]
            {
                Tuple.Create("Report", DisplayText(viewModel?.LastRequirementReportPath, "(none)")),
                Tuple.Create("Generated At", DisplayText(viewModel?.LastRequirementReportGeneratedAt, "(never)")),
                Tuple.Create("Discipline", DisplayText(viewModel?.LastRequirementReportDiscipline, "(not set)")),
                Tuple.Create("Scope", DisplayText(viewModel?.LastRequirementReportScope, "(not set)")),
                Tuple.Create("Counts", DisplayText(viewModel?.LastRequirementReportCounts, "(none)"))
            }));

            WrapPanel buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            buttons.Children.Add(CreateSecondaryButton("Open Settings", () => ModelessToolWindow.ShowSettings()));
            buttons.Children.Add(CreateSecondaryButton("Connection Status", () => ModelessToolWindow.ShowConnectionStatus()));
            buttons.Children.Add(CreateSecondaryButton("Open Dashboard", OpenWebDashboard));
            buttons.Children.Add(CreateSecondaryButton("Refresh", async () => await RefreshAsync()));
            stack.Children.Add(buttons);

            return new Expander
            {
                Header = "Support / Diagnostics",
                IsExpanded = false,
                Margin = new Thickness(0, 0, 0, 12),
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 8, 0, 0),
                    Child = stack
                }
            };
        }

        private Border BuildMiniCard(string title, Tuple<string, string>[] fields)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            foreach (Tuple<string, string> field in fields)
            {
                stack.Children.Add(BuildField(field.Item1, field.Item2));
            }

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12),
                Child = stack
            };
        }

        private Border BuildField(string label, string value)
        {
            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 2)
            });
            stack.Children.Add(new TextBlock
            {
                Text = value ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            });

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 10, 10),
                Child = stack
            };
        }

        private Border BuildChip(string label, string value, bool isActive = false)
        {
            Color bg = isActive ? Color.FromRgb(239, 246, 255) : Color.FromRgb(248, 250, 252);
            Color borderColor = isActive ? Color.FromRgb(191, 219, 254) : Color.FromRgb(226, 232, 240);
            Color valueFg = isActive ? Color.FromRgb(29, 78, 216) : Color.FromRgb(51, 65, 85);

            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 1)
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 12,
                Foreground = new SolidColorBrush(valueFg),
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            });

            return new Border
            {
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 6),
                Child = stack
            };
        }

        private Border BuildCompactField(string label, string value)
        {
            StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = label + ": ",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });
            stack.Children.Add(new TextBlock
            {
                Text = value ?? "",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 300
            });

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 4),
                Child = stack
            };
        }

        private async Task RefreshAsync()
        {
            try
            {
                _viewModel = await EmaDashboardViewModel.LoadAsync(_document);
                Content = BuildShell();
            }
            catch (Exception ex)
            {
                Content = BuildErrorRoot("Unable to load EMA AI panel: " + ex.Message);
                LoggingService.Error("EMA AI panel refresh failed.", ex);
            }
        }

        private void LoadRequirements()
        {
            if (_commandData?.Application?.ActiveUIDocument == null)
            {
                TaskDialog.Show("EMA AI", "No active Revit document is available.");
                return;
            }

            RequirementCheckWorkflowService.LoadRequirements(_commandData);
            _ = RefreshAsync();
        }

        private void SyncModelData()
        {
            if (_commandData?.Application?.ActiveUIDocument == null)
            {
                TaskDialog.Show("EMA AI", "No active Revit document is available.");
                return;
            }

            ExportRunner.Run(_commandData, ExportDiscipline.All);
            _ = RefreshAsync();
        }

        private void RunComplianceCheck()
        {
            if (_commandData?.Application?.ActiveUIDocument == null)
            {
                TaskDialog.Show("EMA AI", "No active Revit document is available.");
                return;
            }

            RequirementCheckWorkflowService.RunComplianceCheck(_commandData);
            _ = RefreshAsync();
        }

        private void OpenLastReport()
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            string reportPath = settings.LastRequirementReportPath;

            if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
            {
                TaskDialog.Show("EMA AI", "No Owner Requirements report has been generated yet.");
                return;
            }

            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }

        private void ExportPdf()
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            string reportPath = settings.LastRequirementReportPath;

            if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
            {
                TaskDialog.Show("EMA AI", "Generate a report before exporting to PDF.");
                return;
            }

            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
            TaskDialog.Show("EMA AI", "The HTML report opened in your browser. Use Print > Save as PDF to export it.");
        }

        private void CopySummary()
        {
            try
            {
                EmaSettings settings = LocalConfigService.LoadSettings();
                if (string.IsNullOrWhiteSpace(settings.LastRequirementReportPath) || !File.Exists(settings.LastRequirementReportPath))
                {
                    TaskDialog.Show("EMA AI", "Generate a report before copying the summary.");
                    return;
                }

                string summary = string.IsNullOrWhiteSpace(settings.LastRequirementReportClipboardSummary)
                    ? BuildClipboardFallback(settings)
                    : settings.LastRequirementReportClipboardSummary;

                Clipboard.SetText(summary);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("EMA AI", "Could not copy summary: " + ex.Message);
            }
        }

        private static string BuildClipboardFallback(EmaSettings settings)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "EMA AI Owner Requirements Check",
                "Project: " + DisplayText(settings.ProjectDisplayName, "(not set)"),
                "Model: " + DisplayText(settings.ProjectDisplayName, "(not set)"),
                "Discipline: " + DisplayText(settings.LastRequirementReportDiscipline, "(not set)"),
                "Scope: " + DisplayText(settings.LastRequirementReportScope, "(not set)"),
                "Overall Score: " + settings.LastRequirementReportMatchScore.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%",
                "Met: " + settings.LastRequirementReportMetCount,
                "Not Met: " + settings.LastRequirementReportNotMetCount,
                "Needs Human Review: " + settings.LastRequirementReportNeedsReviewCount,
                "Insufficient Model Data: " + settings.LastRequirementReportInsufficientDataCount,
                "Not Applicable: " + settings.LastRequirementReportNotApplicableCount
            });
        }

        private void AskAboutReport()
        {
            ModelessToolWindow.ShowAskAboutReport();
        }

        private void OpenWebDashboard()
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            string url = string.IsNullOrWhiteSpace(settings.DashboardUrl)
                ? EmaSettings.GetDashboardUrlForEnvironment(settings.GetEnvironmentLabel())
                : settings.DashboardUrl;

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private static Border BuildErrorRoot(string message)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Margin = new Thickness(18),
                Padding = new Thickness(18),
                Child = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27)),
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold
                }
            };
        }

        private static string DisplayText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string BuildBindingSummary(EmaDashboardViewModel viewModel)
        {
            if (viewModel?.Binding == null)
            {
                return "(not set)";
            }

            return viewModel.Binding.DescribeModelBinding();
        }

        private Button CreateActionButton(string label, Action action)
        {
            Button button = new Button
            {
                Content = label,
                Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(14, 7, 14, 7),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            };

            button.Click += (sender, args) =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("EMA AI", ex.Message);
                    LoggingService.Error("EMA AI panel action failed.", ex);
                }
            };

            return button;
        }

        private Button CreateSecondaryButton(string label, Action action)
        {
            Button button = new Button
            {
                Content = label,
                Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                FontSize = 13
            };

            button.Click += (sender, args) =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("EMA AI", ex.Message);
                    LoggingService.Error("EMA AI panel action failed.", ex);
                }
            };

            return button;
        }
    }
}
