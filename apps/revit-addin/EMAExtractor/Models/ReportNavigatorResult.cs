using System;
using System.Collections.Generic;
using System.IO;

namespace EMAExtractor.Models
{
    public class ReportNavigatorResult
    {
        public ReportNavigatorState State { get; set; } = ReportNavigatorState.NoReportFound;
        public ReportVisualState VisualState { get; set; } = ReportVisualState.NoReportFound;
        public ReportDataState DataState { get; set; } = ReportDataState.NoReportData;
        public string ReportPath { get; set; } = "";
        public string ReportDirectory { get; set; } = "";
        public string StatusMessage { get; set; } = "";
        public string FooterMessage { get; set; } = "";
        public string DiagnosticMessage { get; set; } = "";
        public string SearchSummary { get; set; } = "";
        public int CandidateCount { get; set; } = 0;
        public int RequirementCount { get; set; } = 0;
        public int ModelElementCount { get; set; } = 0;
        public string ReportDataMessage { get; set; } = "";
        public bool IsManualSelection { get; set; } = false;
        public bool BrowserFallbackUsed { get; set; } = false;
        public bool WebViewRuntimeUnavailable { get; set; } = false;
        public List<string> SearchRoots { get; } = new List<string>();

        public bool HasReport
        {
            get { return !string.IsNullOrWhiteSpace(ReportPath) && File.Exists(ReportPath); }
        }

        public bool HasReportData
        {
            get { return DataState == ReportDataState.ReportDataLoaded; }
        }

        public static ReportNavigatorResult CreateNoReportFound(string message, IEnumerable<string> searchRoots = null)
        {
            ReportNavigatorResult result = new ReportNavigatorResult
            {
                State = ReportNavigatorState.NoReportFound,
                VisualState = ReportVisualState.NoReportFound,
                DataState = ReportDataState.NoReportData,
                StatusMessage = string.IsNullOrWhiteSpace(message)
                    ? "Run Owner Requirements Check first. No EMA AI Owner Requirements report was found."
                    : message,
                FooterMessage = "No report found."
            };

            if (searchRoots != null)
            {
                result.SearchRoots.AddRange(searchRoots);
            }

            return result;
        }

        public static ReportNavigatorResult CreateLoading(string reportPath, bool manualSelection, string statusMessage, string footerMessage)
        {
            return new ReportNavigatorResult
            {
                State = manualSelection ? ReportNavigatorState.ManualReportSelected : ReportNavigatorState.ReportFoundLoading,
                VisualState = ReportVisualState.ReportFound,
                DataState = ReportDataState.NoReportData,
                ReportPath = reportPath ?? string.Empty,
                ReportDirectory = GetDirectory(reportPath),
                StatusMessage = statusMessage ?? string.Empty,
                FooterMessage = footerMessage ?? string.Empty,
                IsManualSelection = manualSelection
            };
        }

        public static ReportNavigatorResult CreateLoaded(string reportPath, bool manualSelection, string footerMessage)
        {
            return new ReportNavigatorResult
            {
                State = ReportNavigatorState.ReportLoadedInWebView,
                VisualState = ReportVisualState.WebViewLoaded,
                DataState = ReportDataState.ReportDataLoaded,
                ReportPath = reportPath ?? string.Empty,
                ReportDirectory = GetDirectory(reportPath),
                StatusMessage = "Loaded report inside Revit.",
                FooterMessage = footerMessage ?? "Loaded report inside Revit.",
                IsManualSelection = manualSelection
            };
        }

        public static ReportNavigatorResult CreateInvalidReportPath(string reportPath, string message)
        {
            return new ReportNavigatorResult
            {
                State = ReportNavigatorState.InvalidReportPath,
                VisualState = ReportVisualState.InvalidReportPath,
                DataState = ReportDataState.NoReportData,
                ReportPath = reportPath ?? string.Empty,
                ReportDirectory = GetDirectory(reportPath),
                StatusMessage = string.IsNullOrWhiteSpace(message)
                    ? "The selected report path does not exist or is not an EMA AI report."
                    : message,
                FooterMessage = "Invalid report path."
            };
        }

        public static ReportNavigatorResult CreateWebViewFallback(string reportPath, bool runtimeUnavailable, string diagnosticMessage, string footerMessage)
        {
            return new ReportNavigatorResult
            {
                State = runtimeUnavailable
                    ? ReportNavigatorState.WebViewRuntimeUnavailable
                    : ReportNavigatorState.WebViewFailedOpenedInBrowser,
                VisualState = runtimeUnavailable
                    ? ReportVisualState.WebViewUnavailable
                    : ReportVisualState.BrowserFallbackOpened,
                DataState = ReportDataState.NoReportData,
                ReportPath = reportPath ?? string.Empty,
                ReportDirectory = GetDirectory(reportPath),
                StatusMessage = runtimeUnavailable
                    ? "WebView2 is not available on this machine. Opened the report in the default browser."
                    : "Unable to load the report inside Revit. Opened it in the default browser.",
                FooterMessage = footerMessage ?? "Opened report in the default browser.",
                DiagnosticMessage = diagnosticMessage ?? string.Empty,
                BrowserFallbackUsed = true,
                WebViewRuntimeUnavailable = runtimeUnavailable
            };
        }

        public static ReportNavigatorResult CreateBrowserFallbackOpened(string reportPath, string footerMessage)
        {
            return new ReportNavigatorResult
            {
                State = ReportNavigatorState.BrowserFallbackOpened,
                VisualState = ReportVisualState.BrowserFallbackOpened,
                DataState = ReportDataState.NoReportData,
                ReportPath = reportPath ?? string.Empty,
                ReportDirectory = GetDirectory(reportPath),
                StatusMessage = footerMessage ?? "Opened current report in the default browser.",
                FooterMessage = footerMessage ?? "Opened current report in the default browser.",
                BrowserFallbackUsed = true
            };
        }

        private static string GetDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(path) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
