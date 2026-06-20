using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EMAExtractor.Models;
using Color = System.Windows.Media.Color;

namespace EMAExtractor.UI
{
    /// <summary>
    /// Polished multi-stage progress window for the compliance check.
    /// Modeless — shown on UI thread, updated via Dispatcher.BeginInvoke from background.
    /// </summary>
    public class ComplianceProgressWindow : Window
    {
        // Colors
        private static readonly Color Navy = Color.FromRgb(30, 58, 138);
        private static readonly Color Blue = Color.FromRgb(37, 99, 235);
        private static readonly Color LightBlue = Color.FromRgb(219, 234, 254);
        private static readonly Color Green = Color.FromRgb(22, 163, 74);
        private static readonly Color LightGreen = Color.FromRgb(220, 252, 231);
        private static readonly Color Amber = Color.FromRgb(217, 119, 6);
        private static readonly Color LightAmber = Color.FromRgb(254, 243, 199);
        private static readonly Color Red = Color.FromRgb(220, 38, 38);
        private static readonly Color LightRed = Color.FromRgb(254, 226, 226);
        private static readonly Color Gray50 = Color.FromRgb(248, 250, 252);
        private static readonly Color Gray100 = Color.FromRgb(241, 245, 249);
        private static readonly Color Gray200 = Color.FromRgb(226, 232, 240);
        private static readonly Color Gray400 = Color.FromRgb(148, 163, 184);
        private static readonly Color Gray500 = Color.FromRgb(100, 116, 139);
        private static readonly Color Gray700 = Color.FromRgb(51, 65, 85);
        private static readonly Color Gray900 = Color.FromRgb(15, 23, 42);

        // Controls
        private readonly ProgressBar _overallProgress;
        private readonly TextBlock _overallPercentText;
        private readonly ProgressBar _stageProgress;
        private readonly TextBlock _stagePercentText;
        private readonly TextBlock _activityMessage;
        private readonly StackPanel _stageChecklist;
        private readonly TextBlock _countsText;
        private readonly TextBlock _timingText;
        private readonly Button _cancelButton;
        private readonly StackPanel _finalActionsPanel;
        private readonly StackPanel _detailPanel;
        private readonly Expander _detailExpander;

        private string _reportPath;
        private string _clipboardSummary;

        public bool CancelRequested { get; private set; }

        public ComplianceProgressWindow(string discipline, string scope)
        {
            Title = "Running Owner Requirements Check";
            Width = 680;
            Height = 720;
            MinWidth = 560;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            Topmost = true;
            Background = new SolidColorBrush(Gray50);
            FontFamily = new FontFamily("Segoe UI");

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // === Header ===
            Border header = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Gray200),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 18, 24, 18)
            };

            StackPanel headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "Running Owner Requirements Check",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Gray900)
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Comparing loaded requirements against synced Revit model evidence.",
                FontSize = 13,
                Foreground = new SolidColorBrush(Gray500),
                Margin = new Thickness(0, 4, 0, 0)
            });

            // Context chips
            WrapPanel chips = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            chips.Children.Add(BuildChip("Discipline", discipline));
            chips.Children.Add(BuildChip("Scope", scope));
            headerStack.Children.Add(chips);

            header.Child = headerStack;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // === Body (scrollable) ===
            StackPanel body = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };

            // Overall progress
            body.Children.Add(MakeLabel("Overall progress"));
            Grid overallRow = new Grid();
            overallRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            overallRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _overallProgress = new ProgressBar
            {
                Height = 20,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = new SolidColorBrush(Blue),
                Background = new SolidColorBrush(Gray100)
            };
            _overallPercentText = new TextBlock
            {
                Text = "0%",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Gray700),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                MinWidth = 40
            };
            Grid.SetColumn(_overallProgress, 0);
            Grid.SetColumn(_overallPercentText, 1);
            overallRow.Children.Add(_overallProgress);
            overallRow.Children.Add(_overallPercentText);
            body.Children.Add(overallRow);

            // Current stage progress
            body.Children.Add(MakeLabel("Current stage", 12));
            Grid stageRow = new Grid();
            stageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _stageProgress = new ProgressBar
            {
                Height = 14,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                Background = new SolidColorBrush(Gray100)
            };
            _stagePercentText = new TextBlock
            {
                Text = "0%",
                FontSize = 12,
                Foreground = new SolidColorBrush(Gray500),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                MinWidth = 40
            };
            Grid.SetColumn(_stageProgress, 0);
            Grid.SetColumn(_stagePercentText, 1);
            stageRow.Children.Add(_stageProgress);
            stageRow.Children.Add(_stagePercentText);
            body.Children.Add(stageRow);

            // Activity message
            _activityMessage = new TextBlock
            {
                Text = "Initializing...",
                FontSize = 13,
                Foreground = new SolidColorBrush(Blue),
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            };
            body.Children.Add(_activityMessage);

            // Stage checklist
            body.Children.Add(MakeLabel("Stages", 14));
            _stageChecklist = new StackPanel();
            List<StageInfo> defaultStages = RequirementCheckProgress.BuildDefaultStages();
            foreach (StageInfo stage in defaultStages)
            {
                _stageChecklist.Children.Add(BuildStageRow(stage));
            }
            Border checklistBorder = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Gray200),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Child = _stageChecklist
            };
            body.Children.Add(checklistBorder);

            // Counts
            _countsText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = new SolidColorBrush(Gray700),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            };
            body.Children.Add(_countsText);

            // Timing
            _timingText = new TextBlock
            {
                Text = "Elapsed: 00:00",
                FontSize = 12,
                Foreground = new SolidColorBrush(Gray500),
                Margin = new Thickness(0, 4, 0, 0)
            };
            body.Children.Add(_timingText);

            // Final actions (hidden until complete)
            _finalActionsPanel = new StackPanel
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 14, 0, 0)
            };
            body.Children.Add(_finalActionsPanel);

            // Advanced detail expander
            _detailPanel = new StackPanel();
            _detailExpander = new Expander
            {
                Header = "Advanced details",
                IsExpanded = false,
                Margin = new Thickness(0, 12, 0, 0),
                Content = new Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Gray200),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Child = _detailPanel
                }
            };
            body.Children.Add(_detailExpander);

            ScrollViewer scroll = new ScrollViewer
            {
                Content = body,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scroll, 1);
            mainGrid.Children.Add(scroll);

            // === Footer ===
            Border footer = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Gray200),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 12, 24, 12)
            };

            DockPanel footerDock = new DockPanel { LastChildFill = false };
            _cancelButton = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(16, 8, 16, 8),
                MinWidth = 100,
                Background = new SolidColorBrush(Colors.White),
                Foreground = new SolidColorBrush(Gray700),
                BorderBrush = new SolidColorBrush(Gray200)
            };
            _cancelButton.Click += (s, e) =>
            {
                CancelRequested = true;
                _cancelButton.IsEnabled = false;
                _cancelButton.Content = "Cancelling...";
            };
            DockPanel.SetDock(_cancelButton, Dock.Right);
            footerDock.Children.Add(_cancelButton);

            footer.Child = footerDock;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            Content = mainGrid;
        }

        /// <summary>
        /// Thread-safe progress update. Called from any thread — marshals to UI dispatcher.
        /// </summary>
        public void UpdateProgress(RequirementCheckProgress progress)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateProgress(progress)));
                return;
            }

            if (progress == null) return;
            progress.Clamp();

            _overallProgress.Value = progress.OverallPercent;
            _overallPercentText.Text = progress.OverallPercent.ToString("0") + "%";
            _stageProgress.IsIndeterminate = progress.IsIndeterminate;
            _stageProgress.Value = progress.StagePercent;
            _stagePercentText.Text = progress.IsIndeterminate ? "..." : progress.StagePercent.ToString("0") + "%";

            if (!string.IsNullOrWhiteSpace(progress.Message))
            {
                _activityMessage.Text = progress.Message;
            }

            // Update stage checklist
            if (progress.Stages != null && progress.Stages.Count > 0)
            {
                _stageChecklist.Children.Clear();
                foreach (StageInfo stage in progress.Stages)
                {
                    _stageChecklist.Children.Add(BuildStageRow(stage));
                }
            }

            // Counts
            List<string> counts = new List<string>();
            if (progress.TotalRequirements > 0)
            {
                counts.Add(string.Format("Requirements: {0} / {1}", progress.ProcessedRequirements, progress.TotalRequirements));
            }
            if (progress.IndexedElements > 0)
            {
                counts.Add(string.Format("Model elements indexed: {0}", progress.IndexedElements));
            }
            if (progress.KeyIssuesFound > 0)
            {
                counts.Add(string.Format("Key issues found: {0}", progress.KeyIssuesFound));
            }
            if (!string.IsNullOrWhiteSpace(progress.Discipline))
            {
                counts.Add("Discipline: " + progress.Discipline);
            }
            _countsText.Text = string.Join("   |   ", counts);

            // Timing
            string elapsed = progress.Elapsed.ToString(@"mm\:ss");
            string timing = "Elapsed: " + elapsed;
            if (progress.EstimatedRemaining.HasValue)
            {
                timing += "   |   Remaining: ~" + progress.EstimatedRemaining.Value.ToString(@"mm\:ss");
            }
            _timingText.Text = timing;

            // Cancel state
            _cancelButton.IsEnabled = progress.CanCancel && !CancelRequested;

            // Update detail lines
            if (progress.DetailLines != null && progress.DetailLines.Count > 0)
            {
                _detailPanel.Children.Clear();
                foreach (string line in progress.DetailLines)
                {
                    _detailPanel.Children.Add(new TextBlock
                    {
                        Text = line,
                        FontSize = 11,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = new SolidColorBrush(Gray500),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 1, 0, 1)
                    });
                }
            }
        }

        /// <summary>
        /// Shows the completion state with final actions.
        /// </summary>
        public void ShowComplete(
            string reportPath,
            string clipboardSummary,
            int metCount, int notMetCount, int reviewCount,
            int insufficientCount, int notApplicableCount,
            double overallScore, int keyIssueCount,
            List<string> warnings)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ShowComplete(
                    reportPath, clipboardSummary,
                    metCount, notMetCount, reviewCount,
                    insufficientCount, notApplicableCount,
                    overallScore, keyIssueCount, warnings)));
                return;
            }

            _reportPath = reportPath;
            _clipboardSummary = clipboardSummary;

            Title = "Report Ready";
            _activityMessage.Text = "Compliance check complete. Report generated.";
            _activityMessage.Foreground = new SolidColorBrush(Green);
            _overallProgress.Value = 100;
            _overallPercentText.Text = "100%";
            _stageProgress.Value = 100;
            _stagePercentText.Text = "100%";
            _cancelButton.Content = "Close";
            _cancelButton.IsEnabled = true;
            _cancelButton.Click -= CancelClick;
            _cancelButton.Click += (s, e) => Close();

            // Summary card
            _finalActionsPanel.Visibility = Visibility.Visible;
            _finalActionsPanel.Children.Clear();

            Border summaryCard = new Border
            {
                Background = new SolidColorBrush(LightGreen),
                BorderBrush = new SolidColorBrush(Green),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };

            StackPanel summaryStack = new StackPanel();
            summaryStack.Children.Add(new TextBlock
            {
                Text = "Report Ready",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(20, 83, 45))
            });
            summaryStack.Children.Add(new TextBlock
            {
                Text = string.Format("Overall Score: {0:0.0}%   |   Key Issues: {1}", overallScore, keyIssueCount),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(20, 83, 45)),
                Margin = new Thickness(0, 4, 0, 6)
            });

            WrapPanel statusChips = new WrapPanel();
            statusChips.Children.Add(BuildResultChip("Met", metCount, LightGreen, Green));
            statusChips.Children.Add(BuildResultChip("Not Met", notMetCount, LightRed, Red));
            statusChips.Children.Add(BuildResultChip("Needs Review", reviewCount, LightAmber, Amber));
            statusChips.Children.Add(BuildResultChip("Insufficient Data", insufficientCount, LightBlue, Blue));
            statusChips.Children.Add(BuildResultChip("Not Applicable", notApplicableCount, Gray100, Gray500));
            summaryStack.Children.Add(statusChips);

            summaryCard.Child = summaryStack;
            _finalActionsPanel.Children.Add(summaryCard);

            // Warnings
            if (warnings != null && warnings.Count > 0)
            {
                Border warningCard = new Border
                {
                    Background = new SolidColorBrush(LightAmber),
                    BorderBrush = new SolidColorBrush(Amber),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                StackPanel warningStack = new StackPanel();
                warningStack.Children.Add(new TextBlock
                {
                    Text = "Some results may require review:",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14)),
                    Margin = new Thickness(0, 0, 0, 4)
                });
                foreach (string w in warnings.Take(5))
                {
                    warningStack.Children.Add(new TextBlock
                    {
                        Text = "• " + w,
                        Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14)),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12,
                        Margin = new Thickness(4, 1, 0, 1)
                    });
                }
                warningCard.Child = warningStack;
                _finalActionsPanel.Children.Add(warningCard);
            }

            // Action buttons
            WrapPanel actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            actions.Children.Add(MakePrimaryButton("Open Report", () =>
            {
                try { Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show("Could not open report: " + ex.Message, "EMA AI"); }
            }));
            actions.Children.Add(MakeSecondaryButton("Export PDF", () =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true });
                    MessageBox.Show("The HTML report opened in your browser. Use Print > Save as PDF to export.", "EMA AI");
                }
                catch (Exception ex) { MessageBox.Show("Could not open report: " + ex.Message, "EMA AI"); }
            }));
            actions.Children.Add(MakeSecondaryButton("Copy Summary", () =>
            {
                try { Clipboard.SetText(_clipboardSummary ?? "No summary available."); }
                catch (Exception ex) { MessageBox.Show("Could not copy: " + ex.Message, "EMA AI"); }
            }));
            actions.Children.Add(MakeSecondaryButton("Ask EMA AI", () => ModelessToolWindow.ShowAskAboutReport()));
            _finalActionsPanel.Children.Add(actions);
        }

        /// <summary>Shows the cancelled state.</summary>
        public void ShowCancelled()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ShowCancelled));
                return;
            }

            Title = "Compliance Check Cancelled";
            _activityMessage.Text = "Compliance check cancelled. No final report was generated.";
            _activityMessage.Foreground = new SolidColorBrush(Amber);
            _cancelButton.Content = "Close";
            _cancelButton.IsEnabled = true;
            _cancelButton.Click -= CancelClick;
            _cancelButton.Click += (s, e) => Close();
        }

        /// <summary>Shows the error state.</summary>
        public void ShowError(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ShowError(message)));
                return;
            }

            Title = "Compliance Check Failed";
            _activityMessage.Text = "Error: " + message;
            _activityMessage.Foreground = new SolidColorBrush(Red);
            _cancelButton.Content = "Close";
            _cancelButton.IsEnabled = true;
            _cancelButton.Click -= CancelClick;
            _cancelButton.Click += (s, e) => Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) { }

        private static Border BuildChip(string label, string value)
        {
            StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = label + ": ",
                FontSize = 12,
                Foreground = new SolidColorBrush(Gray500)
            });
            stack.Children.Add(new TextBlock
            {
                Text = value ?? "",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Gray700)
            });
            return new Border
            {
                Background = new SolidColorBrush(Gray100),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Child = stack
            };
        }

        private static Border BuildResultChip(string label, int count, Color bg, Color fg)
        {
            return new Border
            {
                Background = new SolidColorBrush(bg),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 4),
                Child = new TextBlock
                {
                    Text = label + ": " + count,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(fg)
                }
            };
        }

        private static StackPanel BuildStageRow(StageInfo stage)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };

            string icon;
            Color color;
            switch (stage.Status)
            {
                case StageStatus.Complete:
                    icon = "✓";
                    color = Green;
                    break;
                case StageStatus.Running:
                    icon = "▶";
                    color = Blue;
                    break;
                case StageStatus.Warning:
                    icon = "⚠";
                    color = Amber;
                    break;
                case StageStatus.Failed:
                    icon = "✗";
                    color = Red;
                    break;
                default:
                    icon = "○";
                    color = Gray400;
                    break;
            }

            row.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 13,
                Width = 20,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = stage.Name,
                FontSize = 13,
                Foreground = new SolidColorBrush(stage.Status == StageStatus.Waiting ? Gray400 : Gray900),
                FontWeight = stage.Status == StageStatus.Running ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            });
            if (stage.ElapsedMs > 0 && stage.Status == StageStatus.Complete)
            {
                row.Children.Add(new TextBlock
                {
                    Text = string.Format("  ({0:0.0}s)", stage.ElapsedMs / 1000.0),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Gray400),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            return row;
        }

        private static TextBlock MakeLabel(string text, double topMargin = 0)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Gray500),
                Margin = new Thickness(0, topMargin, 0, 4)
            };
        }

        private static Button MakePrimaryButton(string label, Action onClick)
        {
            Button btn = new Button
            {
                Content = label,
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Blue),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Blue),
                MinWidth = 120,
                FontWeight = FontWeights.SemiBold
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private static Button MakeSecondaryButton(string label, Action onClick)
        {
            Button btn = new Button
            {
                Content = label,
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Colors.White),
                Foreground = new SolidColorBrush(Blue),
                BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                MinWidth = 100
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }
    }
}
