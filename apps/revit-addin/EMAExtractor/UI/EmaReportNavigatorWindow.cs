using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using EMAExtractor.Models;
using EMAExtractor.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace EMAExtractor.UI
{
    public class EmaReportNavigatorWindow : Window
    {
        private static EmaReportNavigatorWindow _instance;

        private TextBox _reportPathBox;
        private Border _reportPathBorder;
        private TextBlock _stateTitleText;
        private TextBlock _stateBodyText;
        private TextBlock _footerText;
        private TextBlock _statusText;
        private TextBlock _visualStateLabel;
        private TextBlock _dataStateLabel;
        private WebView2 _webView;
        private Border _stateCard;
        private Button _reloadLatestButton;
        private Button _browseButton;
        private Button _openBrowserButton;
        private TabControl _viewerTabs;

        private ReportNavigatorResult _currentResult = new ReportNavigatorResult();
        private ReportNavigatorState _currentState = ReportNavigatorState.NoReportFound;
        private ReportVisualState _visualState = ReportVisualState.NoReportFound;
        private ReportDataState _reportDataState = ReportDataState.NoReportData;
        private bool _browserInitializationAttempted;
        private bool _browserAvailable;
        private bool _navigationInProgress;
        private bool _usingStringNavigationFallback;
        private string _currentReportPath = "";
        private string _pendingNavigationPath = "";
        private string _lastDiagnosticMessage = "";
        private string _initialTabName = "report";
        private string _initialQuestion = "";
        private string _initialLaunchMessage = "";
        private string _loadedTaxonomyPath = "";

        // Ask EMA AI panel
        private ComboBox _modelComboBox;
        private ComboBox _scopeComboBox;
        private TextBlock _privacyLabel;
        private TextBlock _availabilityLabel;
        private TextBlock _reportDataLabel;
        private TextBlock _chatSessionTitle;
        private ListBox _recentChatsList;
        private ScrollViewer _chatTranscriptScrollViewer;
        private StackPanel _chatTranscriptPanel;
        private TextBox _askInputBox;
        private TextBox _answerBox;
        private TextBox _referencesBox;
        private Button _askButton;
        private Button _copyAnswerButton;
        private Button _clearAskButton;
        private Button _newChatButton;
        private TextBlock _ragStatusLabel;
        private readonly ReportRagService _ragService = new ReportRagService();
        private List<AiModelOption> _modelOptions = new List<AiModelOption>();
        private List<AskChatSession> _recentChatSessions = new List<AskChatSession>();
        private AskChatSession _activeChatSession;
        private CancellationTokenSource _askCts;

        // Audit / Taxonomy panels
        private TextBlock _auditResultText;
        private TextBlock _taxonomyText;
        private DataGrid _taxonomyGrid;
        private TextBox _taxonomySearchBox;
        private ComboBox _taxonomyFamilyFilter;
        private ComboBox _taxonomyValidationFilter;
        private ComboBox _taxonomyCloseableFilter;
        private TextBlock _taxonomyStatusLabel;
        private Button _taxonomyLoadButton;
        private List<TaxonomyMatrixRow> _taxonomyRows = new List<TaxonomyMatrixRow>();

        private EmaReportNavigatorWindow(ReportNavigatorResult discoveryResult, string initialTabName = "report", string initialQuestion = "", string initialLaunchMessage = "")
        {
            Title = "EMA AI Report Navigator";
            Width = 1320;
            Height = 920;
            MinWidth = 980;
            MinHeight = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            FontFamily = new FontFamily("Segoe UI");
            _initialTabName = string.IsNullOrWhiteSpace(initialTabName) ? "report" : initialTabName;
            _initialQuestion = initialQuestion ?? string.Empty;
            _initialLaunchMessage = initialLaunchMessage ?? string.Empty;

            Grid root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            UIElement header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            StackPanel controls = BuildControls();
            Grid.SetRow(controls, 1);
            root.Children.Add(controls);

            Border pathPanel = BuildPathPanel();
            Grid.SetRow(pathPanel, 2);
            root.Children.Add(pathPanel);

            TabControl viewerTabs = BuildViewerTabs();
            Grid.SetRow(viewerTabs, 3);
            root.Children.Add(viewerTabs);

            Border footer = BuildFooter();
            Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            Content = root;

            Loaded += async (sender, args) =>
            {
                RefreshModels();
                await InitializeBrowserAsync();
                ApplyDiscoveryResult(
                    discoveryResult ?? ReportNavigatorService.DiscoverLatestReport(LocalConfigService.LoadSettings()),
                    forceManualSelection: false,
                    autoOpenBrowserOnWebViewFailure: true);
                ApplyInitialTabSelection();
            };

            Closed += (sender, args) =>
            {
                if (ReferenceEquals(_instance, this))
                {
                    _instance = null;
                }
            };
        }

        public static void ShowWindow(ReportNavigatorResult discoveryResult = null)
        {
            ShowWindow(discoveryResult, "report", "", "");
        }

        public static void ShowWindow(ReportNavigatorResult discoveryResult, string initialTabName, string initialQuestion, string initialLaunchMessage)
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
                _instance.ConfigureInitialLaunch(initialTabName, initialQuestion, initialLaunchMessage);
                _instance.ApplyInitialTabSelection();
                return;
            }

            _instance = new EmaReportNavigatorWindow(
                discoveryResult ?? ReportNavigatorService.DiscoverLatestReport(LocalConfigService.LoadSettings()),
                initialTabName,
                initialQuestion,
                initialLaunchMessage);
            _instance.Show();
        }

        private void ConfigureInitialLaunch(string initialTabName, string initialQuestion, string initialLaunchMessage)
        {
            if (!string.IsNullOrWhiteSpace(initialTabName))
            {
                _initialTabName = initialTabName;
            }

            _initialQuestion = initialQuestion ?? string.Empty;
            _initialLaunchMessage = initialLaunchMessage ?? string.Empty;
        }

        private void ApplyInitialTabSelection()
        {
            if (_viewerTabs == null)
            {
                return;
            }

            string tabName = NormalizeTabName(_initialTabName);
            SelectViewerTab(tabName);

            if (string.Equals(tabName, "ask", StringComparison.OrdinalIgnoreCase))
            {
                if (_askInputBox != null && !string.IsNullOrWhiteSpace(_initialQuestion))
                {
                    _askInputBox.Text = _initialQuestion;
                }

                if (_askInputBox != null)
                {
                    Dispatcher.BeginInvoke(new Action(() => _askInputBox.Focus()));
                }
            }

            if (!string.IsNullOrWhiteSpace(_initialLaunchMessage) && _ragStatusLabel != null)
            {
                _ragStatusLabel.Text = _initialLaunchMessage;
            }
        }

        private void SelectViewerTab(string tabName)
        {
            if (_viewerTabs == null || string.IsNullOrWhiteSpace(tabName))
            {
                return;
            }

            string targetHeader = GetTabDisplayHeader(tabName);
            foreach (object item in _viewerTabs.Items)
            {
                if (item is TabItem tab && string.Equals(GetTabHeader(tab), targetHeader, StringComparison.OrdinalIgnoreCase))
                {
                    _viewerTabs.SelectedItem = tab;
                    return;
                }
            }
        }

        private static string GetTabDisplayHeader(string tabName)
        {
            switch (NormalizeTabName(tabName))
            {
                case "ask":
                    return "Ask EMA AI";
                case "taxonomy":
                    return "Taxonomy Matrix";
                case "audit":
                    return "AI Audit";
                default:
                    return "Report";
            }
        }

        private static string GetTabHeader(TabItem tab)
        {
            return tab?.Header == null ? string.Empty : tab.Header.ToString();
        }

        private static string NormalizeTabName(string tabName)
        {
            if (string.IsNullOrWhiteSpace(tabName))
            {
                return "report";
            }

            string value = tabName.Trim();
            if (value.IndexOf("ask", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "ask";
            }

            if (value.IndexOf("taxonomy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "taxonomy";
            }

            if (value.IndexOf("audit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "audit";
            }

            return "report";
        }

        private void FocusAskTab()
        {
            SelectViewerTab("ask");

            if (_askInputBox == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_askInputBox.Text) && !string.IsNullOrWhiteSpace(_initialQuestion))
            {
                _askInputBox.Text = _initialQuestion;
            }

            _askInputBox.Focus();
            _askInputBox.SelectAll();
        }

        private void CopyNavigatorSummary()
        {
            try
            {
                string summary = _ragService.BuildClipboardSummary();
                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = BuildFallbackSummaryText();
                }

                Clipboard.SetText(summary);
                SetStatus("Copied report summary.");
            }
            catch (Exception ex)
            {
                _lastDiagnosticMessage = ex.Message;
                LoggingService.Error("CopyNavigatorSummary failed.", ex);
                MessageBox.Show(this, "Could not copy the report summary: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string BuildFallbackSummaryText()
        {
            if (!string.IsNullOrWhiteSpace(_currentReportPath) && File.Exists(_currentReportPath))
            {
                return "EMA AI report data is available, but the summary could not be built.\nReport: " + _currentReportPath;
            }

            return "No report data loaded.";
        }

        private string GetReportDataBannerText()
        {
            if (_reportDataState == ReportDataState.ReportDataParseFailed)
            {
                return "Report file found, but embedded report data could not be parsed.";
            }

            if (_reportDataState != ReportDataState.ReportDataLoaded)
            {
                return "No report data loaded. Use Reload Latest or Browse to select an EMA AI report.";
            }

            string loaded = _ragService.BuildReportDataStatusMessage();
            if (_visualState == ReportVisualState.WebViewUnavailable || _visualState == ReportVisualState.BrowserFallbackOpened)
            {
                return "Visual report opened in browser. Ask EMA AI is using the loaded report data from the selected HTML file. " + loaded;
            }

            return loaded;
        }

        private string GetVisualStateDescription()
        {
            switch (_visualState)
            {
                case ReportVisualState.NoReportFound:
                    return "No report found";
                case ReportVisualState.ReportFound:
                    return "Report found";
                case ReportVisualState.WebViewLoading:
                    return "WebView loading";
                case ReportVisualState.WebViewLoaded:
                    return "WebView loaded";
                case ReportVisualState.WebViewUnavailable:
                    return "WebView unavailable";
                case ReportVisualState.BrowserFallbackOpened:
                    return "Browser fallback opened";
                case ReportVisualState.InvalidReportPath:
                    return "Invalid report path";
                default:
                    return "Unknown";
            }
        }

        private void UpdateNavigatorStateLabels()
        {
            if (_visualStateLabel != null)
            {
                _visualStateLabel.Text = "Visual state: " + GetVisualStateDescription();
            }

            if (_dataStateLabel != null)
            {
                _dataStateLabel.Text = "Data state: " + GetReportDataBannerText();
            }

            if (_reportDataLabel != null)
            {
                _reportDataLabel.Text = GetReportDataBannerText();
            }

            if (_ragStatusLabel != null && string.IsNullOrWhiteSpace(_ragStatusLabel.Text))
            {
                _ragStatusLabel.Text = GetReportDataBannerText();
            }

            if (_askButton != null)
            {
                _askButton.IsEnabled = !string.IsNullOrWhiteSpace(_currentReportPath) && File.Exists(_currentReportPath);
            }
        }

        private void LoadReportDataFromCurrentPath()
        {
            if (string.IsNullOrWhiteSpace(_currentReportPath) || !File.Exists(_currentReportPath))
            {
                _reportDataState = ReportDataState.NoReportData;
                _currentResult.DataState = ReportDataState.NoReportData;
                _currentResult.ReportDataMessage = string.Empty;
                RefreshRecentChatsList();
                UpdateNavigatorStateLabels();
                return;
            }

            bool loaded = _ragService.LoadFromHtmlFile(_currentReportPath);
            _reportDataState = _ragService.DataState;
            _currentResult.DataState = _ragService.DataState;
            _currentResult.RequirementCount = _ragService.RequirementCount;
            _currentResult.ModelElementCount = _ragService.ModelElementCount;
            _currentResult.ReportDataMessage = _ragService.BuildReportDataStatusMessage();
            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = GetReportDataBannerText();
            }
            RefreshRecentChatsList(_activeChatSession?.SessionId);
            UpdateNavigatorStateLabels();

            if (!loaded && _reportDataState == ReportDataState.ReportDataParseFailed)
            {
                SetStatus(_ragService.LastErrorMessage);
            }
        }

        private UIElement BuildHeader()
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Owner Requirements Report Navigator",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Review the latest EMA AI report directly inside Revit. Reload Latest refreshes the newest report; Browse loads a specific report first in WebView2.",
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            WrapPanel chips = new WrapPanel();
            chips.Children.Add(BuildChip("Mode", "Revit-native viewer"));
            chips.Children.Add(BuildChip("Fallback", "Browser available"));
            chips.Children.Add(BuildChip("Report", "Latest HTML"));
            panel.Children.Add(chips);

            return new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 16, 18, 16),
                Margin = new Thickness(0, 0, 0, 14),
                Child = panel
            };
        }

        private StackPanel BuildControls()
        {
            StackPanel panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };

            _reloadLatestButton = CreateButton("Reload Latest", ReloadLatestReport);
            _browseButton = CreateButton("Browse...", BrowseForReport);
            _openBrowserButton = CreateButton("Open in Browser", OpenCurrentReportInBrowser);
            _openBrowserButton.IsEnabled = false;

            panel.Children.Add(_reloadLatestButton);
            panel.Children.Add(_browseButton);
            panel.Children.Add(_openBrowserButton);

            return panel;
        }

        private Border BuildPathPanel()
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(new TextBlock
            {
                Text = "Current Report Path",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 10, 0)
            });

            _reportPathBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Colors.White)
            };

            _reportPathBox = new TextBox
            {
                IsReadOnly = true,
                MinHeight = 28,
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderThickness = new Thickness(0),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };
            _reportPathBorder.Child = _reportPathBox;

            Grid.SetColumn(_reportPathBorder, 1);
            grid.Children.Add(_reportPathBorder);

            return new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12),
                Child = grid
            };
        }

        private TabControl BuildViewerTabs()
        {
            var tabs = new TabControl
            {
                Margin = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252))
            };
            _viewerTabs = tabs;

            // Tab 1: Report viewer
            Grid reportGrid = new Grid();
            _webView = new WebView2 { Visibility = Visibility.Hidden };
            _webView.NavigationCompleted += OnWebViewNavigationCompleted;
            _stateCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Child = BuildStateContent()
            };
            reportGrid.Children.Add(_webView);
            reportGrid.Children.Add(_stateCard);

            tabs.Items.Add(new TabItem { Header = "Report", Content = reportGrid });
            tabs.Items.Add(new TabItem { Header = "Ask EMA AI", Content = BuildAskEmaAiPanel() });
            tabs.Items.Add(new TabItem { Header = "Taxonomy Matrix", Content = BuildTaxonomyMatrixPanel() });
            tabs.Items.Add(new TabItem { Header = "AI Audit", Content = BuildAiAuditPanel() });

            return tabs;
        }

        private UIElement BuildAskEmaAiPanel()
        {
            Grid root = new Grid { Margin = new Thickness(12) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            UIElement sidebar = BuildRecentChatsSidebar();
            Grid.SetColumn(sidebar, 0);
            root.Children.Add(sidebar);

            Border mainCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(12, 0, 0, 0),
                Child = BuildAskChatMainPanel()
            };
            Grid.SetColumn(mainCard, 1);
            root.Children.Add(mainCard);

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = root
            };
        }

        private UIElement BuildRecentChatsSidebar()
        {
            StackPanel panel = new StackPanel();

            panel.Children.Add(new TextBlock
            {
                Text = "Recent chats",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Saved locally for the current report. Start a fresh thread or resume a previous one.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            _newChatButton = CreateButton("New Chat", StartNewChatSession);
            _newChatButton.Margin = new Thickness(0, 0, 0, 10);
            _newChatButton.MinWidth = 112;
            panel.Children.Add(_newChatButton);

            _recentChatsList = new ListBox
            {
                MinHeight = 300,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                DisplayMemberPath = "DisplayText"
            };
            _recentChatsList.SelectionChanged += (sender, args) =>
            {
                AskChatSession session = _recentChatsList.SelectedItem as AskChatSession;
                if (session != null)
                {
                    SelectChatSession(session);
                }
            };
            panel.Children.Add(_recentChatsList);

            panel.Children.Add(new TextBlock
            {
                Text = "Recent chats keep the report context, question history, and grounded replies together.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 10, 0, 0)
            });

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14),
                Child = panel
            };
        }

        private UIElement BuildAskChatMainPanel()
        {
            StackPanel outer = new StackPanel();

            _chatSessionTitle = new TextBlock
            {
                Text = "Current chat",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            outer.Children.Add(_chatSessionTitle);

            outer.Children.Add(new TextBlock
            {
                Text = "Grounded questions use the loaded report data. New chats create a fresh thread without losing the loaded report context.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            var modelRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
            modelRow.Children.Add(new TextBlock
            {
                Text = "Model:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            });

            _modelComboBox = new ComboBox
            {
                MinWidth = 360,
                MaxWidth = 580,
                VerticalContentAlignment = VerticalAlignment.Center,
                DisplayMemberPath = "DisplayText"
            };
            _modelComboBox.SelectionChanged += (s, e) => UpdateModelBadges();
            modelRow.Children.Add(_modelComboBox);

            var refreshBtn = CreateButton("Refresh Models", RefreshModels);
            refreshBtn.Margin = new Thickness(8, 0, 0, 0);
            refreshBtn.MinWidth = 92;
            modelRow.Children.Add(refreshBtn);
            outer.Children.Add(modelRow);

            _privacyLabel = new TextBlock
            {
                Text = "Select a model above.",
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 3),
                TextWrapping = TextWrapping.Wrap
            };
            outer.Children.Add(_privacyLabel);

            _availabilityLabel = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            outer.Children.Add(_availabilityLabel);

            _reportDataLabel = new TextBlock
            {
                Text = "No report data loaded. Use Reload Latest or Browse to select an EMA AI report.",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            outer.Children.Add(_reportDataLabel);

            var scopeRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            scopeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            scopeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            scopeRow.Children.Add(new TextBlock
            {
                Text = "Context:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            });

            _scopeComboBox = new ComboBox
            {
                MinWidth = 240,
                MaxWidth = 420,
                VerticalContentAlignment = VerticalAlignment.Center,
                ItemsSource = new[]
                {
                    "Summary",
                    "Current Filtered View",
                    "Current Discipline",
                    "Selected Requirement",
                    "Key Issues"
                },
                SelectedIndex = 0
            };
            Grid.SetColumn(_scopeComboBox, 1);
            scopeRow.Children.Add(_scopeComboBox);
            outer.Children.Add(scopeRow);

            Border transcriptBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };
            StackPanel transcriptOuter = new StackPanel();
            transcriptOuter.Children.Add(new TextBlock
            {
                Text = "Chat",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            _chatTranscriptScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MinHeight = 180,
                MaxHeight = 310,
                Background = new SolidColorBrush(Colors.Transparent)
            };
            _chatTranscriptPanel = new StackPanel();
            _chatTranscriptScrollViewer.Content = _chatTranscriptPanel;
            transcriptOuter.Children.Add(_chatTranscriptScrollViewer);
            transcriptBorder.Child = transcriptOuter;
            outer.Children.Add(transcriptBorder);

            var questionRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            questionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            questionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _askInputBox = new TextBox
            {
                MinHeight = 60,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 13,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Text = string.IsNullOrWhiteSpace(_initialQuestion) ? string.Empty : _initialQuestion
            };
            _askInputBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter &&
                    (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0)
                {
                    _ = AskEmaAiAsync();
                    e.Handled = true;
                }
            };

            _askButton = CreateButton("Send", () => _ = AskEmaAiAsync());
            _askButton.Margin = new Thickness(8, 0, 0, 0);
            _askButton.MinWidth = 88;

            Grid.SetColumn(_askInputBox, 0);
            Grid.SetColumn(_askButton, 1);
            questionRow.Children.Add(_askInputBox);
            questionRow.Children.Add(_askButton);
            outer.Children.Add(questionRow);

            var answerBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(2)
            };
            var answerStack = new StackPanel();
            answerStack.Children.Add(new TextBlock
            {
                Text = "Latest answer",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(10, 10, 10, 0)
            });
            _answerBox = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Padding = new Thickness(10),
                FontSize = 13,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = "No report loaded. Open or reload a report, then ask a question about it.",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                MinHeight = 110
            };
            answerStack.Children.Add(_answerBox);
            answerBorder.Child = answerStack;
            outer.Children.Add(answerBorder);

            var referenceBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var referenceStack = new StackPanel();
            referenceStack.Children.Add(new TextBlock
            {
                Text = "References",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            _referencesBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 70,
                Padding = new Thickness(8),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Text = "References will appear here after you ask a question."
            };
            referenceStack.Children.Add(_referencesBox);
            referenceBorder.Child = referenceStack;
            outer.Children.Add(referenceBorder);

            var buttonRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            _copyAnswerButton = CreateButton("Copy Answer", CopyAnswer);
            _copyAnswerButton.MinWidth = 100;
            _clearAskButton = CreateButton("Clear Input", ClearAsk);
            _clearAskButton.MinWidth = 84;
            _clearAskButton.Margin = new Thickness(8, 0, 0, 0);
            buttonRow.Children.Add(_copyAnswerButton);
            buttonRow.Children.Add(_clearAskButton);
            outer.Children.Add(buttonRow);

            _ragStatusLabel = new TextBlock
            {
                Text = "Deterministic summaries are always available once report data is loaded.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            outer.Children.Add(_ragStatusLabel);

            var suggestPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            string[] suggestions =
            {
                "What is Not Met?",
                "Show key issues",
                "Plumbing requirements",
                "Electrical requirements",
                "Tell me about row 606",
                "Why is Row 548 Needs Human Review?"
            };
            foreach (string sug in suggestions)
            {
                string captured = sug;
                Button sugBtn = new Button
                {
                    Content = captured,
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(8, 3, 8, 3),
                    FontSize = 11,
                    Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225))
                };
                sugBtn.Click += (s, e) =>
                {
                    if (_askInputBox != null)
                    {
                        _askInputBox.Text = captured;
                    }

                    _ = AskEmaAiAsync();
                };
                suggestPanel.Children.Add(sugBtn);
            }
            outer.Children.Add(suggestPanel);

            return outer;
        }

        private UIElement BuildTaxonomyMatrixPanel()
        {
            var outer = new StackPanel { Margin = new Thickness(12) };

            outer.Children.Add(new TextBlock
            {
                Text = "Requirement Type Taxonomy Matrix",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            outer.Children.Add(new TextBlock
            {
                Text = "Loaded from data/taxonomy/requirement_type_matrix.json. Use the filters to inspect the deterministic rule matrix independently from the visual report.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var filterRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            _taxonomySearchBox = new TextBox
            {
                MinWidth = 220,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                ToolTip = "Search requirement type, display name, or evidence text"
            };
            _taxonomySearchBox.TextChanged += (s, e) => ApplyTaxonomyFilter();
            filterRow.Children.Add(_taxonomySearchBox);

            _taxonomyFamilyFilter = new ComboBox
            {
                MinWidth = 170,
                Margin = new Thickness(0, 0, 8, 8),
                ItemsSource = new[] { "All Families", "electrical", "lighting", "mechanical", "plumbing", "technology", "specification", "manual_or_drawing_review" },
                SelectedIndex = 0
            };
            _taxonomyFamilyFilter.SelectionChanged += (s, e) => ApplyTaxonomyFilter();
            filterRow.Children.Add(_taxonomyFamilyFilter);

            _taxonomyValidationFilter = new ComboBox
            {
                MinWidth = 150,
                Margin = new Thickness(0, 0, 8, 8),
                ItemsSource = new[] { "All Validation", "Model", "Specification", "Drawing", "Hybrid" },
                SelectedIndex = 0
            };
            _taxonomyValidationFilter.SelectionChanged += (s, e) => ApplyTaxonomyFilter();
            filterRow.Children.Add(_taxonomyValidationFilter);

            _taxonomyCloseableFilter = new ComboBox
            {
                MinWidth = 160,
                Margin = new Thickness(0, 0, 8, 8),
                ItemsSource = new[] { "All Closeability", "Model Closeable", "Not Model Closeable" },
                SelectedIndex = 0
            };
            _taxonomyCloseableFilter.SelectionChanged += (s, e) => ApplyTaxonomyFilter();
            filterRow.Children.Add(_taxonomyCloseableFilter);

            _taxonomyLoadButton = CreateButton("Reload Matrix", LoadTaxonomyMatrix);
            _taxonomyLoadButton.Margin = new Thickness(0, 0, 8, 8);
            _taxonomyLoadButton.MinWidth = 110;
            filterRow.Children.Add(_taxonomyLoadButton);
            outer.Children.Add(filterRow);

            _taxonomyStatusLabel = new TextBlock
            {
                Text = "Taxonomy matrix not loaded yet.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            outer.Children.Add(_taxonomyStatusLabel);

            _taxonomyGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                RowHeaderWidth = 0,
                MinHeight = 320
            };
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Requirement Type", Binding = new Binding("RequirementType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Display Name", Binding = new Binding("DisplayName"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Family", Binding = new Binding("Family"), Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Validation", Binding = new Binding("ValidationType"), Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Model Closeable", Binding = new Binding("ModelCloseable"), Width = new DataGridLength(0.9, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Priority", Binding = new Binding("Priority"), Width = new DataGridLength(0.6, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Direct Evidence", Binding = new Binding("DirectEvidence"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Supporting Context", Binding = new Binding("SupportingContext"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Missing Evidence", Binding = new Binding("MissingEvidence"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Expected Categories", Binding = new Binding("ExpectedCategories"), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Expected Parameters", Binding = new Binding("ExpectedParameters"), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            _taxonomyGrid.Columns.Add(new DataGridTextColumn { Header = "Excluded Categories", Binding = new Binding("ExcludedCategories"), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            outer.Children.Add(_taxonomyGrid);

            _taxonomyText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 8, 0, 0)
            };
            outer.Children.Add(_taxonomyText);

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = outer
            };
        }

        private UIElement BuildAiAuditPanel()
        {
            var outer = new StackPanel { Margin = new Thickness(12) };

            outer.Children.Add(new TextBlock
            {
                Text = "AI Audit — Requirement Classification Coverage",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            outer.Children.Add(new TextBlock
            {
                Text = "Runs RequirementMatrixAuditService on the currently loaded report. Shows unknown counts, scope warnings, review rows, and recommended type updates.",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var runBtn = CreateButton("Run AI Audit", RunAiAudit);
            runBtn.MinWidth = 100;
            outer.Children.Add(runBtn);

            _auditResultText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Margin = new Thickness(0, 10, 0, 0),
                Text = "Load a report, then click 'Run AI Audit'."
            };
            outer.Children.Add(_auditResultText);

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new Border { Padding = new Thickness(4), Child = outer }
            };
        }

        private UIElement BuildStateContent()
        {
            StackPanel stack = new StackPanel { Orientation = Orientation.Vertical };
            _stateTitleText = new TextBlock
            {
                Text = "Run Owner Requirements Check first.",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            _stateBodyText = new TextBlock
            {
                Text = "No EMA AI Owner Requirements report was found.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                FontSize = 13
            };
            _visualStateLabel = new TextBlock
            {
                Text = "Visual state: No report found",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Margin = new Thickness(0, 14, 0, 4)
            };
            _dataStateLabel = new TextBlock
            {
                Text = "Data state: No report data",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 8)
            };

            WrapPanel actionRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            Button reloadButton = CreateButton("Reload Latest", ReloadLatestReport);
            reloadButton.Margin = new Thickness(0, 0, 8, 8);
            Button openBrowserButton = CreateButton("Open in Browser", OpenCurrentReportInBrowser);
            openBrowserButton.Margin = new Thickness(0, 0, 8, 8);
            Button askButton = CreateButton("Ask EMA AI", FocusAskTab);
            askButton.Margin = new Thickness(0, 0, 8, 8);
            Button copySummaryButton = CreateButton("Copy Summary", CopyNavigatorSummary);
            copySummaryButton.Margin = new Thickness(0, 0, 8, 8);
            actionRow.Children.Add(reloadButton);
            actionRow.Children.Add(openBrowserButton);
            actionRow.Children.Add(askButton);
            actionRow.Children.Add(copySummaryButton);

            stack.Children.Add(_stateTitleText);
            stack.Children.Add(_stateBodyText);
            stack.Children.Add(_visualStateLabel);
            stack.Children.Add(_dataStateLabel);
            stack.Children.Add(actionRow);
            return stack;
        }

        private Border BuildFooter()
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _footerText = new TextBlock
            {
                Text = "No report found.",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_footerText, 0);
            grid.Children.Add(_footerText);

            _statusText = new TextBlock
            {
                Text = "Ready.",
                Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0),
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(_statusText, 1);
            grid.Children.Add(_statusText);

            return new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 12, 0, 0),
                Child = grid
            };
        }

        private async Task InitializeBrowserAsync()
        {
            if (_browserInitializationAttempted)
            {
                return;
            }

            _browserInitializationAttempted = true;

            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EMA AI",
                    "WebView2");
                Directory.CreateDirectory(userDataFolder);

                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(environment);
                _browserAvailable = true;

                if (_webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                    _webView.CoreWebView2.Settings.IsStatusBarEnabled = true;
                }
            }
            catch (Exception ex)
            {
                _browserAvailable = false;
                _lastDiagnosticMessage = ex.Message;
                LoggingService.Error("Report navigator WebView2 initialization failed.", ex);
            }
        }

        private void ReloadLatestReport()
        {
            ApplyDiscoveryResult(ReportNavigatorService.DiscoverLatestReport(LocalConfigService.LoadSettings()), forceManualSelection: false, autoOpenBrowserOnWebViewFailure: true);
        }

        private void BrowseForReport()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select Owner Requirements Report",
                Filter = "Owner Requirements HTML (*.html;*.htm)|*.html;*.htm|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = GetInitialBrowseDirectory()
            };

            bool? result = dialog.ShowDialog();
            if (result != true)
            {
                SetFooter("Browse cancelled.");
                return;
            }

            if (!IsValidReportPath(dialog.FileName))
            {
                ApplyInvalidSelection(dialog.FileName);
                return;
            }

            ApplyDiscoveryResult(
                new ReportNavigatorResult
                {
                    State = ReportNavigatorState.ManualReportSelected,
                    ReportPath = dialog.FileName,
                    ReportDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty,
                    StatusMessage = "Loading selected Owner Requirements report...",
                    FooterMessage = "Manual report selected.",
                    IsManualSelection = true,
                    SearchSummary = "Manual report selected."
                },
                forceManualSelection: true,
                autoOpenBrowserOnWebViewFailure: true);
        }

        private void OpenCurrentReportInBrowser()
        {
            if (string.IsNullOrWhiteSpace(_currentReportPath) || !File.Exists(_currentReportPath))
            {
                SetFooter("No report is selected.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(_currentReportPath) { UseShellExecute = true });
                _currentState = ReportNavigatorState.BrowserFallbackOpened;
                _visualState = ReportVisualState.BrowserFallbackOpened;
                SetFooter("Opened current report in default browser.");
                SetStatus("Opened current report in default browser.");
                UpdateNavigatorStateLabels();
            }
            catch (Exception ex)
            {
                SetFooter("Could not open the current report in the browser.");
                _lastDiagnosticMessage = ex.Message;
                LoggingService.Error("Failed to open report in browser.", ex);
                MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyDiscoveryResult(
            ReportNavigatorResult result,
            bool forceManualSelection,
            bool autoOpenBrowserOnWebViewFailure)
        {
            _currentResult = result ?? ReportNavigatorResult.CreateNoReportFound("Run Owner Requirements Check first. No EMA AI Owner Requirements report was found.");

            if (_currentResult.State == ReportNavigatorState.NoReportFound || !_currentResult.HasReport && string.IsNullOrWhiteSpace(_currentResult.ReportPath))
            {
                ShowNoReportState(_currentResult.StatusMessage, _currentResult.SearchSummary);
                return;
            }

            if (_currentResult.State == ReportNavigatorState.InvalidReportPath)
            {
                ShowInvalidPathState(_currentResult.ReportPath, _currentResult.StatusMessage);
                return;
            }

            string reportPath = _currentResult.ReportPath;
            if (!IsValidReportPath(reportPath))
            {
                ShowInvalidPathState(reportPath, "The selected report path does not exist or is not an EMA AI report.");
                return;
            }

            _currentReportPath = Path.GetFullPath(reportPath);
            UpdatePathBox(_currentReportPath, validPath: true);
            _openBrowserButton.IsEnabled = true;

            LoadReportDataFromCurrentPath();

            if (forceManualSelection || _currentResult.IsManualSelection || _currentResult.State == ReportNavigatorState.ManualReportSelected)
            {
                ShowLoadingState(
                    "Loading selected Owner Requirements report...",
                    "Manual report selected.",
                    ReportNavigatorState.ManualReportSelected);
            }
            else
            {
                ShowLoadingState(
                    "Loading latest Owner Requirements report...",
                    string.IsNullOrWhiteSpace(_currentResult.SearchSummary) ? "Found latest report." : _currentResult.SearchSummary,
                    ReportNavigatorState.ReportFoundLoading);
            }

            LoadReportIntoWebView(_currentReportPath, autoOpenBrowserOnWebViewFailure);
        }

        private void LoadReportIntoWebView(string reportPath, bool autoOpenBrowserOnWebViewFailure)
        {
            if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
            {
                ShowInvalidPathState(reportPath, "The selected report path does not exist or is not an EMA AI report.");
                return;
            }

            if (!_browserAvailable || _webView.CoreWebView2 == null)
            {
                HandleWebViewFailure(reportPath, true, autoOpenBrowserOnWebViewFailure, _lastDiagnosticMessage);
                return;
            }

            try
            {
                _navigationInProgress = true;
                _pendingNavigationPath = Path.GetFullPath(reportPath);
                Uri reportUri = new Uri(Path.GetFullPath(_pendingNavigationPath));
                _webView.Visibility = Visibility.Hidden;
                _stateCard.Visibility = Visibility.Visible;
                _visualState = ReportVisualState.WebViewLoading;
                _stateTitleText.Text = "Loading latest Owner Requirements report...";
                _stateBodyText.Text = "Opening " + Path.GetFileName(_pendingNavigationPath) + " inside Revit.";
                SetFooter("Loading report inside Revit...");
                _webView.CoreWebView2.Navigate(reportUri.AbsoluteUri);
            }
            catch (Exception ex)
            {
                if (TryNavigateToStringFallback(reportPath, ex.Message))
                {
                    return;
                }

                HandleWebViewFailure(reportPath, false, autoOpenBrowserOnWebViewFailure, ex.Message);
            }
        }

        private void OnWebViewNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (_navigationInProgress)
            {
                _navigationInProgress = false;
            }

            if (args == null)
            {
                return;
            }

            if (args.IsSuccess && !string.IsNullOrWhiteSpace(_pendingNavigationPath) && File.Exists(_pendingNavigationPath))
            {
                _currentReportPath = _pendingNavigationPath;
                ShowLoadedState(_currentReportPath);
                return;
            }

            string failure = args.WebErrorStatus.ToString();
            if (!_usingStringNavigationFallback && !string.IsNullOrWhiteSpace(_pendingNavigationPath) && File.Exists(_pendingNavigationPath))
            {
                if (TryNavigateToStringFallback(_pendingNavigationPath, failure))
                {
                    return;
                }
            }

            HandleWebViewFailure(
                _pendingNavigationPath,
                runtimeUnavailable: false,
                autoOpenBrowserOnFailure: true,
                diagnosticMessage: failure);
        }

        private void HandleWebViewFailure(
            string reportPath,
            bool runtimeUnavailable,
            bool autoOpenBrowserOnFailure,
            string diagnosticMessage)
        {
            _usingStringNavigationFallback = false;
            _lastDiagnosticMessage = diagnosticMessage ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(reportPath) && File.Exists(reportPath) && autoOpenBrowserOnFailure)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    _lastDiagnosticMessage = string.IsNullOrWhiteSpace(_lastDiagnosticMessage) ? ex.Message : _lastDiagnosticMessage + " | " + ex.Message;
                    LoggingService.Error("Report navigator browser fallback failed.", ex);
                }
            }

            if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
            {
                ShowNoReportState("Run Owner Requirements Check first. No EMA AI Owner Requirements report was found.", "");
                return;
            }

            ReportNavigatorResult fallback = ReportNavigatorResult.CreateWebViewFallback(
                reportPath,
                runtimeUnavailable,
                diagnosticMessage,
                runtimeUnavailable
                    ? "WebView2 runtime unavailable. Opened report in browser."
                    : "Unable to load report in Revit. Opened report in browser.");

            fallback.DiagnosticMessage = _lastDiagnosticMessage;
            ShowBrowserFallbackState(fallback);
        }

        private void ShowLoadedState(string reportPath)
        {
            _currentState = ReportNavigatorState.ReportLoadedInWebView;
            _visualState = ReportVisualState.WebViewLoaded;
            _usingStringNavigationFallback = false;
            _currentReportPath = reportPath ?? string.Empty;
            UpdatePathBox(_currentReportPath, validPath: true);
            _webView.Visibility = Visibility.Visible;
            _stateCard.Visibility = Visibility.Collapsed;
            SetFooter("Loaded report inside Revit.");
            SetStatus("Loaded report inside Revit.");
            _openBrowserButton.IsEnabled = true;
            _reportDataState = _ragService.DataState;
            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = GetReportDataBannerText();
            }
            UpdateNavigatorStateLabels();
        }

        private void RefreshModels()
        {
            try
            {
                _modelOptions = OpenCodeModelConfigService.LoadModels();
                if (_modelComboBox == null) return;

                _modelComboBox.ItemsSource = null;
                _modelComboBox.ItemsSource = _modelOptions;

                // Select default model or deterministic
                int defaultIndex = 0;
                for (int i = 0; i < _modelOptions.Count; i++)
                {
                    if (_modelOptions[i].IsDefault)
                    {
                        defaultIndex = i;
                        break;
                    }
                }
                if (_modelComboBox.Items.Count > 0)
                    _modelComboBox.SelectedIndex = defaultIndex;

                UpdateModelBadges();
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to load model options.", ex);
            }
        }

        private void UpdateModelBadges()
        {
            if (_modelComboBox == null || _privacyLabel == null) return;

            AiModelOption opt = _modelComboBox.SelectedItem as AiModelOption;
            if (opt == null)
            {
                _privacyLabel.Text = "No model selected.";
                _availabilityLabel.Text = "";
                return;
            }

            _privacyLabel.Text = opt.PrivacyMessage ?? "";
            _availabilityLabel.Text = (string.IsNullOrWhiteSpace(opt.AvailabilityMessage) ? "" : opt.AvailabilityMessage) +
                (opt.IsDeterministic ? " Deterministic fallback always available." : "");

            Color privacyColor = opt.IsLocal || opt.IsDeterministic
                ? Color.FromRgb(21, 128, 61)
                : Color.FromRgb(180, 83, 9);
            _privacyLabel.Foreground = new SolidColorBrush(privacyColor);

            if (_reportDataLabel != null)
            {
                _reportDataLabel.Text = GetReportDataBannerText();
            }
        }

        private async Task AskEmaAiAsync()
        {
            if (_askInputBox == null || _answerBox == null)
            {
                return;
            }

            string question = _askInputBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(question))
            {
                return;
            }

            _askButton.IsEnabled = false;
            string scope = _scopeComboBox?.SelectedItem as string ?? "Summary";
            string dataBanner = GetReportDataBannerText();

            if (_reportDataLabel != null)
            {
                _reportDataLabel.Text = dataBanner;
            }

            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = dataBanner;
            }

            try
            {
                if (_activeChatSession == null || !SamePath(_activeChatSession.ReportPath, _currentReportPath))
                {
                    RefreshRecentChatsList();
                }

                if (_activeChatSession == null && !string.IsNullOrWhiteSpace(_currentReportPath) && File.Exists(_currentReportPath))
                {
                    StartNewChatSession();
                }

                if (_activeChatSession != null)
                {
                    _activeChatSession.ContextScope = scope;
                    _activeChatSession.ReportName = Path.GetFileName(_currentReportPath ?? string.Empty);
                    AskChatHistoryService.EnsureSessionTitle(_activeChatSession, question);
                    AskChatHistoryService.AppendMessage(_activeChatSession, "user", question);
                    AskChatHistoryService.SaveSession(_activeChatSession);
                    RenderActiveChatTranscript();
                    RefreshRecentChatsList(_activeChatSession.SessionId);
                }

                RagQueryResult ragResult = _ragService.Query(question);
                AiModelOption selectedModel = _modelComboBox?.SelectedItem as AiModelOption;
                string referencesText = ragResult.SourceRows.Count > 0
                    ? "Rows: " + string.Join(", ", ragResult.SourceRows)
                    : "No explicit row references were returned.";
                string assistantText;

                if (!ragResult.Success)
                {
                    assistantText = ragResult.Answer ?? "No report loaded. Open a report first.";
                    _answerBox.Text = assistantText;
                    _answerBox.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    if (_referencesBox != null)
                    {
                        _referencesBox.Text = string.IsNullOrWhiteSpace(ragResult.ErrorMessage)
                            ? "No references available."
                            : ragResult.ErrorMessage;
                    }
                    _ragStatusLabel.Text = ragResult.Answer ?? dataBanner;
                }
                else
                {
                    bool useAi = selectedModel != null && !selectedModel.IsDeterministic &&
                                 !string.IsNullOrWhiteSpace(selectedModel.BaseUrl) &&
                                 _reportDataState == ReportDataState.ReportDataLoaded;

                    if (!useAi)
                    {
                        assistantText = ragResult.Answer;
                        _answerBox.Text = assistantText;
                        _answerBox.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                        string rowCite = ragResult.SourceRows.Count > 0
                            ? " (Source rows: " + string.Join(", ", ragResult.SourceRows) + ")"
                            : "";
                        _ragStatusLabel.Text = "Deterministic answer." + rowCite;
                        if (_referencesBox != null)
                        {
                            _referencesBox.Text = referencesText;
                        }
                    }
                    else
                    {
                        _ragStatusLabel.Text = "Sending to " + selectedModel.ProviderName + "...";

                        string systemPrompt =
                            "You are EMA AI, an expert BIM/VDC Owner Requirements analyst. " +
                            "Answer ONLY from the report context provided. " +
                            "Never invent requirements, row numbers, or evidence not present in the context. " +
                            "Cite row numbers when relevant. " +
                            "Do not claim compliance certification. " +
                            "This is a first-pass evidence review, not a legal determination.";

                        string chatHistory = BuildConversationHistory(_activeChatSession, 6);
                        string userPromptWithContext =
                            "Context scope: " + scope + Environment.NewLine +
                            "Report data state: " + dataBanner + Environment.NewLine +
                            (string.IsNullOrWhiteSpace(chatHistory)
                                ? string.Empty
                                : "Recent chat history:\n" + chatHistory + Environment.NewLine + Environment.NewLine) +
                            "Report context (deterministic analysis):\n" + ragResult.Answer +
                            "\n\nUser question: " + question;

                        int timeoutMs = selectedModel.TimeoutMs > 0 ? selectedModel.TimeoutMs : 45_000;
                        using (var cts = new CancellationTokenSource(timeoutMs))
                        {
                            _askCts = cts;
                            var provider = new OpenAiCompatibleProvider(selectedModel);

                            AiCompletionResult aiResult = await provider.CompleteAsync(
                                systemPrompt, userPromptWithContext, 512, cts.Token);

                            if (aiResult.Success && !string.IsNullOrWhiteSpace(aiResult.Content))
                            {
                                assistantText = aiResult.Content.Trim();
                                _answerBox.Text = assistantText;
                                _answerBox.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                                _ragStatusLabel.Text = "AI answer via " + selectedModel.ProviderName + ". Grounded in report data.";
                                if (_referencesBox != null)
                                {
                                    _referencesBox.Text = referencesText;
                                }
                            }
                            else
                            {
                                assistantText = ragResult.Answer + Environment.NewLine + Environment.NewLine + "[AI provider unavailable. Showing deterministic answer.]";
                                _answerBox.Text = assistantText;
                                _answerBox.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                                _ragStatusLabel.Text = "Model not reachable. Deterministic fallback used. " + (aiResult.ErrorMessage ?? string.Empty);
                                _availabilityLabel.Text = "Model not reachable. Deterministic fallback used.";
                                if (_referencesBox != null)
                                {
                                    _referencesBox.Text = referencesText;
                                }
                            }
                        }
                    }
                }

                if (_activeChatSession != null)
                {
                    AskChatHistoryService.AppendMessage(_activeChatSession, "assistant", assistantText);
                    _activeChatSession.ModelDisplayName = selectedModel?.DisplayText ?? _activeChatSession.ModelDisplayName;
                    _activeChatSession.ContextScope = scope;
                    AskChatHistoryService.SaveSession(_activeChatSession);
                    RefreshRecentChatsList(_activeChatSession.SessionId);
                    RenderActiveChatTranscript();
                }
            }
            catch (OperationCanceledException)
            {
                if (_ragStatusLabel != null)
                {
                    _ragStatusLabel.Text = "Request timed out. Showing deterministic answer.";
                }

                if (_referencesBox != null)
                {
                    _referencesBox.Text = "Request timed out before references could be built.";
                }
            }
            catch (Exception ex)
            {
                if (_ragStatusLabel != null)
                {
                    _ragStatusLabel.Text = "Error: " + ex.Message;
                }

                LoggingService.Error("Ask EMA AI error.", ex);
            }
            finally
            {
                _askButton.IsEnabled = true;
                _askCts = null;
            }
        }

        private void CopyAnswer()
        {
            if (_answerBox != null && !string.IsNullOrWhiteSpace(_answerBox.Text))
            {
                try { Clipboard.SetText(_answerBox.Text); } catch { }
                if (_ragStatusLabel != null) _ragStatusLabel.Text = "Answer copied to clipboard.";
            }
        }

        private void ClearAsk()
        {
            if (_askInputBox != null) _askInputBox.Text = "";
            if (_ragStatusLabel != null) _ragStatusLabel.Text = "Input cleared.";
            _askCts?.Cancel();
        }

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string BuildConversationHistory(AskChatSession session, int maxMessages)
        {
            if (session == null || session.Messages == null || session.Messages.Count == 0)
            {
                return string.Empty;
            }

            int take = Math.Max(1, maxMessages);
            List<AskChatMessage> recent = session.Messages
                .Where(message => message != null)
                .Skip(Math.Max(0, session.Messages.Count - take))
                .ToList();

            if (recent.Count > 0 && recent.Last().IsUser)
            {
                recent = recent.Take(recent.Count - 1).ToList();
            }

            List<string> lines = new List<string>();
            foreach (AskChatMessage message in recent)
            {
                string role = message.IsUser ? "User" : "EMA AI";
                lines.Add(role + ": " + message.Content);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void StartNewChatSession()
        {
            if (string.IsNullOrWhiteSpace(_currentReportPath) || !File.Exists(_currentReportPath))
            {
                if (_ragStatusLabel != null)
                {
                    _ragStatusLabel.Text = "Open a report before starting a new chat.";
                }

                return;
            }

            AskChatSession session = AskChatHistoryService.CreateSession(
                _currentReportPath,
                Path.GetFileName(_currentReportPath),
                "New chat",
                _scopeComboBox?.SelectedItem as string ?? "Summary");

            _activeChatSession = session;
            AskChatHistoryService.SaveSession(session);
            RefreshRecentChatsList(session.SessionId);
            RenderActiveChatTranscript();

            if (_askInputBox != null)
            {
                _askInputBox.Text = string.Empty;
                Dispatcher.BeginInvoke(new Action(() => _askInputBox.Focus()));
            }

            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = "New chat started.";
            }
        }

        private void RefreshRecentChatsList(string selectSessionId = null)
        {
            if (_recentChatsList == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentReportPath) || !File.Exists(_currentReportPath))
            {
                _recentChatSessions = new List<AskChatSession>();
                _recentChatsList.ItemsSource = _recentChatSessions;
                _activeChatSession = null;
                UpdateChatSessionHeader();
                RenderActiveChatTranscript();
                return;
            }

            string activeSessionId = selectSessionId;
            if (string.IsNullOrWhiteSpace(activeSessionId) && _activeChatSession != null &&
                SamePath(_activeChatSession.ReportPath, _currentReportPath))
            {
                activeSessionId = _activeChatSession.SessionId;
            }

            _recentChatSessions = AskChatHistoryService.GetRecentSessionsForReport(_currentReportPath, 8, activeSessionId);

            if (_recentChatSessions.Count == 0)
            {
                _activeChatSession = AskChatHistoryService.CreateSession(
                    _currentReportPath,
                    Path.GetFileName(_currentReportPath),
                    "New chat",
                    _scopeComboBox?.SelectedItem as string ?? "Summary");
                AskChatHistoryService.SaveSession(_activeChatSession);
                _recentChatSessions = new List<AskChatSession> { _activeChatSession };
            }

            _recentChatsList.ItemsSource = null;
            _recentChatsList.ItemsSource = _recentChatSessions;

            AskChatSession selected = null;
            if (!string.IsNullOrWhiteSpace(activeSessionId))
            {
                selected = _recentChatSessions.FirstOrDefault(session => string.Equals(session.SessionId, activeSessionId, StringComparison.OrdinalIgnoreCase));
            }

            if (selected == null)
            {
                selected = _recentChatSessions.FirstOrDefault();
            }

            if (selected != null)
            {
                _recentChatsList.SelectedItem = selected;
                _activeChatSession = selected;
            }

            UpdateChatSessionHeader();
            RenderActiveChatTranscript();
        }

        private void SelectChatSession(AskChatSession session)
        {
            if (session == null)
            {
                return;
            }

            _activeChatSession = session;
            if (_scopeComboBox != null && !string.IsNullOrWhiteSpace(session.ContextScope))
            {
                _scopeComboBox.SelectedItem = session.ContextScope;
            }

            UpdateChatSessionHeader();
            RenderActiveChatTranscript();
            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = "Loaded chat: " + (string.IsNullOrWhiteSpace(session.Title) ? "New chat" : session.Title);
            }
        }

        private void UpdateChatSessionHeader()
        {
            if (_chatSessionTitle == null)
            {
                return;
            }

            if (_activeChatSession == null)
            {
                _chatSessionTitle.Text = "Current chat";
                return;
            }

            string title = string.IsNullOrWhiteSpace(_activeChatSession.Title) ? "New chat" : _activeChatSession.Title.Trim();
            string reportLabel = string.IsNullOrWhiteSpace(_activeChatSession.ReportName)
                ? Path.GetFileName(_activeChatSession.ReportPath ?? string.Empty)
                : _activeChatSession.ReportName;
            if (string.IsNullOrWhiteSpace(reportLabel))
            {
                reportLabel = "loaded report";
            }

            _chatSessionTitle.Text = title + "  |  " + reportLabel;
        }

        private void RenderActiveChatTranscript()
        {
            if (_chatTranscriptPanel == null)
            {
                return;
            }

            _chatTranscriptPanel.Children.Clear();

            if (_activeChatSession == null || _activeChatSession.Messages == null || _activeChatSession.Messages.Count == 0)
            {
                _chatTranscriptPanel.Children.Add(BuildTranscriptIntroCard());
                if (_answerBox != null)
                {
                    _answerBox.Text = "No messages yet. Start a new chat and ask a grounded question about the loaded report.";
                    _answerBox.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                }

                if (_referencesBox != null)
                {
                    _referencesBox.Text = "References will appear here after you ask a question.";
                }

                return;
            }

            foreach (AskChatMessage message in _activeChatSession.Messages)
            {
                _chatTranscriptPanel.Children.Add(BuildChatBubble(message));
            }

            AskChatMessage lastAssistant = _activeChatSession.Messages.LastOrDefault(item => item != null && !item.IsUser);
            if (_answerBox != null)
            {
                _answerBox.Text = lastAssistant?.Content ?? "No assistant response yet.";
                _answerBox.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            }

            if (_referencesBox != null)
            {
                _referencesBox.Text = "Recent chat messages are kept locally for this report. References from the last answer will appear here after you ask.";
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_chatTranscriptScrollViewer != null)
                {
                    _chatTranscriptScrollViewer.ScrollToEnd();
                }
            }));
        }

        private Border BuildTranscriptIntroCard()
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Chat ready",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text = GetReportDataBannerText() + Environment.NewLine + "Ask about row numbers, missing evidence, or the key issues in this report.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
            });

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10),
                Child = stack
            };
        }

        private Border BuildChatBubble(AskChatMessage message)
        {
            bool isUser = message != null && message.IsUser;
            var bubbleStack = new StackPanel();
            bubbleStack.Children.Add(new TextBlock
            {
                Text = isUser ? "You" : "EMA AI",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = isUser ? new SolidColorBrush(Color.FromRgb(191, 219, 254)) : new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            bubbleStack.Children.Add(new TextBlock
            {
                Text = message?.Content ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                Foreground = isUser ? Brushes.White : new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                FontSize = 13
            });
            bubbleStack.Children.Add(new TextBlock
            {
                Text = message == null ? string.Empty : message.CreatedAtUtc.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture),
                FontSize = 10,
                Foreground = isUser ? new SolidColorBrush(Color.FromRgb(219, 234, 254)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 6, 0, 0)
            });

            return new Border
            {
                Background = isUser ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Colors.White),
                BorderBrush = isUser ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12),
                Margin = isUser ? new Thickness(72, 0, 0, 10) : new Thickness(0, 0, 72, 10),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 720,
                Child = bubbleStack
            };
        }

        private void LoadTaxonomyMatrix()
        {
            if (_taxonomyText == null || _taxonomyGrid == null || _taxonomyStatusLabel == null)
            {
                return;
            }

            try
            {
                string appRoot = LoggingService.AppRoot ?? AppDomain.CurrentDomain.BaseDirectory;
                string matrixPath = Path.Combine(appRoot, "data", "taxonomy", "requirement_type_matrix.json");
                _loadedTaxonomyPath = matrixPath;

                if (!File.Exists(matrixPath))
                {
                    _taxonomyRows = new List<TaxonomyMatrixRow>();
                    _taxonomyGrid.ItemsSource = _taxonomyRows;
                    _taxonomyStatusLabel.Text = "Taxonomy matrix not found. Run taxonomy setup or load the runtime matrix.";
                    _taxonomyText.Text = _taxonomyStatusLabel.Text;
                    return;
                }

                string json = File.ReadAllText(matrixPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    List<TaxonomyMatrixRow> rows = new List<TaxonomyMatrixRow>();

                    if (root.TryGetProperty("requirement_types", out JsonElement types) &&
                        types.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement t in types.EnumerateArray())
                        {
                            bool fallbackAllowed = t.TryGetProperty("fallback_allowed", out JsonElement fallbackEl) && fallbackEl.ValueKind == JsonValueKind.True;
                            bool modelOnlyMet = t.TryGetProperty("allows_model_only_met", out JsonElement modelOnlyEl) && modelOnlyEl.ValueKind == JsonValueKind.True;
                            rows.Add(new TaxonomyMatrixRow
                            {
                                RequirementType = GetJsonString(t, "id"),
                                DisplayName = GetJsonString(t, "display_name"),
                                Family = GetJsonString(t, "family"),
                                ValidationType = GetJsonString(t, "validation_type"),
                                ModelCloseable = fallbackAllowed ? "No" : "Yes",
                                Priority = GetJsonInt(t, "priority").ToString(CultureInfo.InvariantCulture),
                                DirectEvidence = JoinJsonStringList(t, "direct_closing_evidence"),
                                SupportingContext = JoinJsonStringList(t, "supporting_context"),
                                MissingEvidence = JoinJsonStringList(t, "missing_direct_evidence"),
                                ExpectedCategories = JoinJsonStringList(t, "allowed_categories"),
                                ExpectedParameters = JoinJsonStringList(t, "trigger_keywords"),
                                ExcludedCategories = JoinJsonStringList(t, "excluded_categories"),
                                Notes = (fallbackAllowed ? "Fallback allowed." : "Deterministic close available.") + (modelOnlyMet ? " Model-only Met can occur." : string.Empty)
                            });
                        }
                    }

                    _taxonomyRows = rows;
                    _taxonomyGrid.ItemsSource = _taxonomyRows;
                    ApplyTaxonomyFilter();
                    _taxonomyStatusLabel.Text = "Loaded " + rows.Count.ToString(CultureInfo.InvariantCulture) + " requirement type rows from " + matrixPath;
                    _taxonomyText.Text = _taxonomyStatusLabel.Text;
                }
            }
            catch (Exception ex)
            {
                _taxonomyRows = new List<TaxonomyMatrixRow>();
                _taxonomyGrid.ItemsSource = _taxonomyRows;
                _taxonomyStatusLabel.Text = "Error loading taxonomy matrix: " + ex.Message;
                _taxonomyText.Text = _taxonomyStatusLabel.Text;
            }
        }

        private void ApplyTaxonomyFilter()
        {
            if (_taxonomyGrid == null || _taxonomyRows == null)
            {
                return;
            }

            IEnumerable<TaxonomyMatrixRow> filtered = _taxonomyRows;

            string search = NormalizeFilterText(_taxonomySearchBox?.Text);
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(row =>
                    NormalizeFilterText(string.Join(" ",
                        row.RequirementType,
                        row.DisplayName,
                        row.Family,
                        row.ValidationType,
                        row.ModelCloseable,
                        row.DirectEvidence,
                        row.SupportingContext,
                        row.MissingEvidence,
                        row.ExpectedCategories,
                        row.ExpectedParameters,
                        row.ExcludedCategories,
                        row.Notes)).Contains(search));
            }

            string family = _taxonomyFamilyFilter?.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(family) && !string.Equals(family, "All Families", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(row => string.Equals(row.Family, family, StringComparison.OrdinalIgnoreCase));
            }

            string validation = _taxonomyValidationFilter?.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(validation) && !string.Equals(validation, "All Validation", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(row => string.Equals(row.ValidationType, validation, StringComparison.OrdinalIgnoreCase));
            }

            string closeable = _taxonomyCloseableFilter?.SelectedItem as string;
            if (string.Equals(closeable, "Model Closeable", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(row => string.Equals(row.ModelCloseable, "Yes", StringComparison.OrdinalIgnoreCase));
            }
            else if (string.Equals(closeable, "Not Model Closeable", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(row => string.Equals(row.ModelCloseable, "No", StringComparison.OrdinalIgnoreCase));
            }

            List<TaxonomyMatrixRow> rows = filtered.ToList();
            _taxonomyGrid.ItemsSource = rows;

            if (_taxonomyStatusLabel != null)
            {
                _taxonomyStatusLabel.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Showing {0} of {1} requirement type rows.",
                    rows.Count,
                    _taxonomyRows.Count);
            }
        }

        private static string NormalizeFilterText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static string GetJsonString(JsonElement element, string key)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            if (element.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }

            string camel = char.ToLowerInvariant(key[0]) + key.Substring(1);
            if (element.TryGetProperty(camel, out JsonElement camelValue) && camelValue.ValueKind == JsonValueKind.String)
            {
                return camelValue.GetString() ?? string.Empty;
            }

            string snake = CamelToSnake(key);
            if (!string.Equals(snake, key, StringComparison.OrdinalIgnoreCase) &&
                element.TryGetProperty(snake, out JsonElement snakeValue) &&
                snakeValue.ValueKind == JsonValueKind.String)
            {
                return snakeValue.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private static string CamelToSnake(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c) && i > 0 && value[i - 1] != '_')
                {
                    chars.Append('_');
                }

                chars.Append(char.ToLowerInvariant(c));
            }

            return chars.ToString();
        }

        private static int GetJsonInt(JsonElement element, string key)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            if (element.TryGetProperty(key, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n))
                {
                    return n;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    return parsed;
                }
            }

            return 0;
        }

        private static string JoinJsonStringList(JsonElement element, string key)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(key, out JsonElement value))
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        parts.Add(item.GetString());
                    }
                }
            }
            else if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                parts.Add(value.GetString());
            }

            return string.Join(", ", parts);
        }

        private void RunAiAudit()
        {
            if (_auditResultText == null) return;
            try
            {
                string appRoot = LoggingService.AppRoot ?? AppDomain.CurrentDomain.BaseDirectory;
                string matrixPath = Path.Combine(appRoot, "data", "taxonomy", "requirement_type_matrix.json");

                MatrixAuditReport report = RequirementMatrixAuditService.AuditFromReportHtml(
                    _currentReportPath, matrixPath);

                if (report == null)
                {
                    _auditResultText.Text = "Load a report first. The audit needs a report HTML file with embedded JSON.";
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== AI Audit Report ===");
                sb.AppendLine($"Total requirements:     {report.TotalRequirements}");
                sb.AppendLine($"Known types:            {report.KnownTypeCount}");
                sb.AppendLine($"Unknown/ambiguous:      {report.UnknownAmbiguousCount}");
                sb.AppendLine($"Scope warnings:         {report.ScopeWarningCount}");
                sb.AppendLine($"False Met risk:         {report.FalseMetRiskCount}");
                sb.AppendLine($"Generic next actions:   {report.GenericNextActionCount}");
                sb.AppendLine($"Not Met wrong reasons:  {report.NotMetWrongReasonCount}");
                sb.AppendLine($"Contradictions:         {report.ContradictionCount}");
                sb.AppendLine($"Taxonomy coverage:      {report.TaxonomyCoveragePercent:F1}%");
                sb.AppendLine();

                if (report.RowsToReview.Count > 0)
                {
                    sb.AppendLine("--- Rows to Review ---");
                    sb.AppendLine(string.Join(", ", report.RowsToReview.Take(24)));
                    sb.AppendLine();
                }

                if (report.RecommendedTypeUpdates.Count > 0)
                {
                    sb.AppendLine("--- Recommended Type Updates ---");
                    foreach (string item in report.RecommendedTypeUpdates.Take(12))
                    {
                        sb.AppendLine("  - " + item);
                    }
                    sb.AppendLine();
                }

                if (report.CandidateScopeWarnings.Count > 0)
                {
                    sb.AppendLine("--- Candidate Scope Warnings ---");
                    foreach (string item in report.CandidateScopeWarnings.Take(12))
                    {
                        sb.AppendLine("  - " + item);
                    }
                    sb.AppendLine();
                }

                if (report.ScopeWarningEntries.Count > 0)
                {
                    sb.AppendLine("--- Scope Warning Rows ---");
                    foreach (MatrixAuditEntry w in report.ScopeWarningEntries.Take(10))
                    {
                        sb.AppendLine($"  Row {w.SourceRow}: {w.AssignedType}");
                        if (!string.IsNullOrWhiteSpace(w.ScopeWarningReason))
                            sb.AppendLine($"    Reason: {w.ScopeWarningReason}");
                        if (!string.IsNullOrWhiteSpace(w.Reasoning))
                            sb.AppendLine($"    Reasoning: {w.Reasoning}");
                        if (!string.IsNullOrWhiteSpace(w.NextBestAction))
                            sb.AppendLine($"    Next action: {w.NextBestAction}");
                    }
                    sb.AppendLine();
                }

                if (report.FalseMetRiskCount > 0)
                {
                    sb.AppendLine("--- False Met Risk Candidates ---");
                    foreach (MatrixAuditEntry entry in report.FalseMetRiskEntries.Take(10))
                    {
                        sb.AppendLine($"  Row {entry.SourceRow}: {entry.AssignedType} | {entry.EvidenceAlignment}");
                        if (!string.IsNullOrWhiteSpace(entry.WhyNotModelCloseable))
                            sb.AppendLine($"    Why not model closeable: {entry.WhyNotModelCloseable}");
                        if (!string.IsNullOrWhiteSpace(entry.NextBestAction))
                            sb.AppendLine($"    Next action: {entry.NextBestAction}");
                    }
                    sb.AppendLine();
                }

                if (report.GenericNextActionEntries.Count > 0)
                {
                    sb.AppendLine("--- Generic Next Action Candidates ---");
                    foreach (MatrixAuditEntry entry in report.GenericNextActionEntries.Take(10))
                    {
                        sb.AppendLine($"  Row {entry.SourceRow}: {entry.AssignedType}");
                        if (!string.IsNullOrWhiteSpace(entry.NextBestAction))
                        {
                            sb.AppendLine($"    Next action: {entry.NextBestAction}");
                        }
                    }
                    sb.AppendLine();
                }

                if (report.NotMetWrongReasonEntries.Count > 0)
                {
                    sb.AppendLine("--- Not Met Wrong Reason Candidates ---");
                    foreach (MatrixAuditEntry entry in report.NotMetWrongReasonEntries.Take(10))
                    {
                        sb.AppendLine($"  Row {entry.SourceRow}: {entry.AssignedType}");
                        if (!string.IsNullOrWhiteSpace(entry.Reasoning))
                        {
                            sb.AppendLine($"    Reasoning: {entry.Reasoning}");
                        }
                    }
                    sb.AppendLine();
                }

                if (report.ContradictionEntries.Count > 0)
                {
                    sb.AppendLine("--- Contradictions ---");
                    foreach (MatrixAuditEntry entry in report.ContradictionEntries.Take(10))
                    {
                        sb.AppendLine($"  Row {entry.SourceRow}: {entry.AssignedType}");
                        if (!string.IsNullOrWhiteSpace(entry.EvidenceAlignment))
                        {
                            sb.AppendLine($"    Evidence alignment: {entry.EvidenceAlignment}");
                        }
                        if (!string.IsNullOrWhiteSpace(entry.WhyNotModelCloseable))
                        {
                            sb.AppendLine($"    Why not model closeable: {entry.WhyNotModelCloseable}");
                        }
                    }
                    sb.AppendLine();
                }

                if (report.TypeDistribution.Count > 0)
                {
                    sb.AppendLine("--- Type Distribution (top 10) ---");
                    foreach (var kv in report.TypeDistribution.OrderByDescending(x => x.Value).Take(10))
                    {
                        sb.AppendLine($"  {kv.Value,4}x  {kv.Key}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("No compliance certification is made by this audit. It is a deterministic review aid only.");
                _auditResultText.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                _auditResultText.Text = "Audit error: " + ex.Message;
                LoggingService.Error("RunAiAudit error.", ex);
            }
        }

        private bool TryNavigateToStringFallback(string reportPath, string diagnosticMessage)
        {
            if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath) || _webView?.CoreWebView2 == null)
            {
                return false;
            }

            try
            {
                string html = File.ReadAllText(reportPath);
                if (string.IsNullOrWhiteSpace(html))
                {
                    return false;
                }

                _usingStringNavigationFallback = true;
                _navigationInProgress = true;
                _pendingNavigationPath = Path.GetFullPath(reportPath);
                _lastDiagnosticMessage = diagnosticMessage ?? string.Empty;
                _visualState = ReportVisualState.WebViewLoading;
                _webView.Visibility = Visibility.Hidden;
                _stateCard.Visibility = Visibility.Visible;
                _stateTitleText.Text = "Loading report from local file content...";
                _stateBodyText.Text = "WebView2 had trouble loading the file URI. EMA AI is retrying with embedded HTML content.";
                SetFooter("Retrying report load inside Revit...");
                _webView.CoreWebView2.NavigateToString(html);
                return true;
            }
            catch (Exception ex)
            {
                _usingStringNavigationFallback = false;
                _navigationInProgress = false;
                _lastDiagnosticMessage = ex.Message;
                LoggingService.Error("String navigation fallback failed.", ex);
                return false;
            }
        }

        private void ShowLoadingState(string title, string body, ReportNavigatorState state)
        {
            _currentState = state;
            _visualState = ReportVisualState.WebViewLoading;
            _stateTitleText.Text = string.IsNullOrWhiteSpace(title) ? "Loading report..." : title;
            _stateBodyText.Text = string.IsNullOrWhiteSpace(body) ? "" : body;
            _stateCard.Visibility = Visibility.Visible;
            _webView.Visibility = Visibility.Hidden;
            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = GetReportDataBannerText();
            }
            SetFooter(string.IsNullOrWhiteSpace(body) ? "Loading report..." : body);
            SetStatus("Loading...");
            UpdateNavigatorStateLabels();
        }

        private void ShowBrowserFallbackState(ReportNavigatorResult result)
        {
            _currentState = result.WebViewRuntimeUnavailable
                ? ReportNavigatorState.WebViewRuntimeUnavailable
                : ReportNavigatorState.WebViewFailedOpenedInBrowser;
            _visualState = result.WebViewRuntimeUnavailable
                ? ReportVisualState.WebViewUnavailable
                : ReportVisualState.BrowserFallbackOpened;
            _currentReportPath = result.ReportPath ?? string.Empty;
            UpdatePathBox(_currentReportPath, validPath: true);
            _webView.Visibility = Visibility.Hidden;
            _stateCard.Visibility = Visibility.Visible;
            _stateTitleText.Text = "Report opened in browser";
            if (_reportDataState == ReportDataState.ReportDataLoaded)
            {
                _stateBodyText.Text = "WebView2 could not render the report inside Revit, but EMA AI loaded the report data successfully.";
            }
            else if (_reportDataState == ReportDataState.ReportDataParseFailed)
            {
                _stateBodyText.Text = "WebView2 could not render the report inside Revit. The report was opened in your browser, but embedded report data could not be parsed.";
            }
            else
            {
                _stateBodyText.Text = "WebView2 could not render the report inside Revit. The report was opened in your browser.";
            }

            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = GetReportDataBannerText();
            }
            SetFooter(GetReportDataBannerText());
            SetStatus(result.StatusMessage + (string.IsNullOrWhiteSpace(result.DiagnosticMessage) ? "" : " " + result.DiagnosticMessage));
            _openBrowserButton.IsEnabled = File.Exists(_currentReportPath);
            UpdateNavigatorStateLabels();
        }

        private void ShowInvalidPathState(string reportPath, string message)
        {
            _currentState = ReportNavigatorState.InvalidReportPath;
            _visualState = ReportVisualState.InvalidReportPath;
            _reportDataState = ReportDataState.NoReportData;
            _currentReportPath = reportPath ?? string.Empty;
            UpdatePathBox(_currentReportPath, validPath: false);
            _webView.Visibility = Visibility.Hidden;
            _stateCard.Visibility = Visibility.Visible;
            _stateTitleText.Text = "Invalid report path.";
            _stateBodyText.Text = string.IsNullOrWhiteSpace(message)
                ? "The selected report path does not exist or is not an EMA AI report."
                : message;
            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = GetReportDataBannerText();
            }
            SetFooter("Invalid report path.");
            SetStatus(_stateBodyText.Text);
            _openBrowserButton.IsEnabled = false;
            RefreshRecentChatsList();
            UpdateNavigatorStateLabels();
        }

        private void ShowNoReportState(string message, string searchSummary)
        {
            _currentState = ReportNavigatorState.NoReportFound;
            _visualState = ReportVisualState.NoReportFound;
            _reportDataState = ReportDataState.NoReportData;
            _currentReportPath = string.Empty;
            UpdatePathBox(string.Empty, validPath: false);
            _openBrowserButton.IsEnabled = false;
            _webView.Visibility = Visibility.Hidden;
            _stateCard.Visibility = Visibility.Visible;
            _stateTitleText.Text = "Run Owner Requirements Check first.";
            _stateBodyText.Text = string.IsNullOrWhiteSpace(message)
                ? "No EMA AI Owner Requirements report was found."
                : message;
            if (_ragStatusLabel != null)
            {
                _ragStatusLabel.Text = GetReportDataBannerText();
            }
            SetFooter(string.IsNullOrWhiteSpace(searchSummary) ? "No report found." : searchSummary);
            SetStatus("No report found.");
            RefreshRecentChatsList();
            UpdateNavigatorStateLabels();
        }

        private void ApplyInvalidSelection(string fileName)
        {
            ShowInvalidPathState(fileName, "The selected report path does not exist or is not an EMA AI report.");
            MessageBox.Show(
                this,
                "The selected file is not a valid EMA AI Owner Requirements report.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private string GetInitialBrowseDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_currentReportPath) && File.Exists(_currentReportPath))
            {
                string currentDirectory = Path.GetDirectoryName(_currentReportPath);
                if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
                {
                    return currentDirectory;
                }
            }

            ReportNavigatorResult discovery = ReportNavigatorService.DiscoverLatestReport(LocalConfigService.LoadSettings());
            if (discovery != null && discovery.HasReport)
            {
                string discoveredDirectory = discovery.ReportDirectory;
                if (!string.IsNullOrWhiteSpace(discoveredDirectory) && Directory.Exists(discoveredDirectory))
                {
                    return discoveredDirectory;
                }
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        private void UpdatePathBox(string value, bool validPath)
        {
            _reportPathBox.Text = value ?? string.Empty;
            _reportPathBorder.BorderBrush = validPath
                ? new SolidColorBrush(Color.FromRgb(147, 197, 253))
                : new SolidColorBrush(Color.FromRgb(248, 113, 113));
            _reportPathBorder.Background = validPath
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(254, 242, 242));
        }

        private void SetFooter(string text)
        {
            _footerText.Text = string.IsNullOrWhiteSpace(text) ? "Ready." : text;
        }

        private void SetStatus(string text)
        {
            _statusText.Text = string.IsNullOrWhiteSpace(text) ? "Ready." : text;
        }

        private static bool IsValidReportPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            string fileName = Path.GetFileName(path);
            return !string.IsNullOrWhiteSpace(fileName) &&
                fileName.StartsWith("EMA_AI_Requirement_Check_", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        }

        private static Button CreateButton(string label, Action handler)
        {
            Button button = new Button
            {
                Content = label,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                MinWidth = 132
            };
            button.Click += (sender, args) => handler();
            return button;
        }

        private static Border BuildChip(string label, string value)
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
                Text = value,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            });

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 8, 8),
                Child = stack
            };
        }

        private sealed class TaxonomyMatrixRow
        {
            public string RequirementType { get; set; }
            public string DisplayName { get; set; }
            public string Family { get; set; }
            public string ValidationType { get; set; }
            public string ModelCloseable { get; set; }
            public string Priority { get; set; }
            public string DirectEvidence { get; set; }
            public string SupportingContext { get; set; }
            public string MissingEvidence { get; set; }
            public string ExpectedCategories { get; set; }
            public string ExpectedParameters { get; set; }
            public string ExcludedCategories { get; set; }
            public string Notes { get; set; }
        }
    }
}
