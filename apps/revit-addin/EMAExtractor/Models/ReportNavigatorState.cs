namespace EMAExtractor.Models
{
    public enum ReportNavigatorState
    {
        NoReportFound = 0,
        ReportFoundLoading = 1,
        ReportLoadedInWebView = 2,
        ManualReportSelected = 3,
        InvalidReportPath = 4,
        WebViewFailedOpenedInBrowser = 5,
        BrowserFallbackOpened = 6,
        WebViewRuntimeUnavailable = 7
    }

    public enum ReportVisualState
    {
        NoReportFound = 0,
        ReportFound = 1,
        WebViewLoading = 2,
        WebViewLoaded = 3,
        WebViewUnavailable = 4,
        BrowserFallbackOpened = 5,
        InvalidReportPath = 6
    }

    public enum ReportDataState
    {
        NoReportData = 0,
        ReportDataLoading = 1,
        ReportDataLoaded = 2,
        ReportDataParseFailed = 3
    }
}
