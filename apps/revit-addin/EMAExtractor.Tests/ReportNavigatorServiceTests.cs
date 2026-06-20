using System;
using System.IO;
using EMAExtractor.Models;
using EMAExtractor.Services;
using Xunit;

namespace EMAExtractor.Tests
{
    public class ReportNavigatorServiceTests
    {
        [Fact]
        public void DiscoverLatestReport_PicksNewestMatchingHtmlAcrossSearchRoots()
        {
            string root = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Navigator_Tests", Guid.NewGuid().ToString("N"));
            string olderRoot = Path.Combine(root, "older");
            string newerRoot = Path.Combine(root, "newer");
            Directory.CreateDirectory(olderRoot);
            Directory.CreateDirectory(newerRoot);

            string olderPath = Path.Combine(olderRoot, "EMA_AI_Requirement_Check_Project_All_20240101_010101.html");
            string newerPath = Path.Combine(newerRoot, "EMA_AI_Requirement_Check_Project_All_20260608_093632.html");

            File.WriteAllText(olderPath, "<html><body>older</body></html>");
            File.WriteAllText(newerPath, "<html><body>newer</body></html>");

            ReportNavigatorResult result = ReportNavigatorService.DiscoverLatestReport(
                settings: new EmaSettings(),
                additionalSearchRoots: new[] { olderRoot, newerRoot, Path.Combine(root, "missing") },
                includeStandardRoots: false);

            Assert.True(result.HasReport);
            Assert.Equal(Path.GetFullPath(newerPath), result.ReportPath);
            Assert.Contains("Found 2 report(s)", result.SearchSummary);
            Assert.Contains(olderRoot, result.SearchRoots);
            Assert.Contains(newerRoot, result.SearchRoots);
        }

        [Fact]
        public void DiscoverLatestReport_ReturnsFriendlyMessageWhenNoReportsExist()
        {
            string root = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Navigator_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            ReportNavigatorResult result = ReportNavigatorService.DiscoverLatestReport(
                settings: new EmaSettings(),
                additionalSearchRoots: new[] { root, Path.Combine(root, "missing") },
                includeStandardRoots: false);

            Assert.False(result.HasReport);
            Assert.Equal("", result.ReportPath);
            Assert.Contains("Run Owner Requirements Check first", result.StatusMessage);
            Assert.Contains("Searched", result.SearchSummary);
        }

        [Fact]
        public void DiscoverLatestReport_HandlesMissingDirectoriesWithoutThrowing()
        {
            string missingRoot = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Navigator_Tests", Guid.NewGuid().ToString("N"), "missing");

            ReportNavigatorResult result = ReportNavigatorService.DiscoverLatestReport(
                settings: new EmaSettings(),
                additionalSearchRoots: new[] { missingRoot },
                includeStandardRoots: false);

            Assert.False(result.HasReport);
            Assert.Empty(result.ReportPath);
        }

        [Fact]
        public void DiscoverLatestReport_PicksNewestReportEvenWhenSavedPathExists()
        {
            string root = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Navigator_Tests", Guid.NewGuid().ToString("N"));
            string savedPath = Path.Combine(root, "saved", "EMA_AI_Requirement_Check_Saved_All_20260608_113126.html");
            string newerPath = Path.Combine(root, "landing", "EMA_AI_Requirement_Check_Newer_All_20260608_114500.html");

            Directory.CreateDirectory(Path.GetDirectoryName(savedPath));
            Directory.CreateDirectory(Path.GetDirectoryName(newerPath));
            File.WriteAllText(savedPath, "<html>saved</html>");
            File.WriteAllText(newerPath, "<html>newer</html>");

            EmaSettings settings = new EmaSettings
            {
                LastRequirementReportPath = savedPath,
                DefaultOutputFolder = Path.Combine(root, "output")
            };

            ReportNavigatorResult result = ReportNavigatorService.DiscoverLatestReport(
                settings: settings,
                additionalSearchRoots: new[] { Path.GetDirectoryName(newerPath) },
                includeStandardRoots: false);

            Assert.Equal(ReportNavigatorState.ReportFoundLoading, result.State);
            Assert.Equal(Path.GetFullPath(newerPath), result.ReportPath);
            Assert.DoesNotContain("saved report path", result.SearchSummary, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DiscoverLatestReport_PicksNewestPipelineLandingReportWhenItIsNewer()
        {
            string root = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Navigator_Tests", Guid.NewGuid().ToString("N"));
            string artifactRoot = Path.Combine(root, "artifacts", "EMAExtractor");
            string landingRoot = Path.Combine(root, "Pipeline", "pipeline", "landing");
            Directory.CreateDirectory(artifactRoot);
            Directory.CreateDirectory(landingRoot);

            string olderArtifact = Path.Combine(artifactRoot, "EMA_AI_Requirement_Check_Project_All_20260608_093632.html");
            string newerLanding = Path.Combine(landingRoot, "EMA_AI_Requirement_Check_Project_All_20260608_113126.html");
            File.WriteAllText(olderArtifact, "<html>older</html>");
            File.WriteAllText(newerLanding, "<html>newer</html>");

            File.SetLastWriteTimeUtc(olderArtifact, new DateTime(2026, 6, 8, 9, 36, 32, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newerLanding, new DateTime(2026, 6, 8, 11, 31, 26, DateTimeKind.Utc));

            ReportNavigatorResult result = ReportNavigatorService.DiscoverLatestReport(
                settings: new EmaSettings(),
                additionalSearchRoots: new[] { artifactRoot, landingRoot },
                includeStandardRoots: false);

            Assert.Equal(Path.GetFullPath(newerLanding), result.ReportPath);
            Assert.Equal(ReportNavigatorState.ReportFoundLoading, result.State);
        }

        [Fact]
        public void DiscoverLatestReport_FallsBackToWriteTimeWhenTimestampMissing()
        {
            string root = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Navigator_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string older = Path.Combine(root, "EMA_AI_Requirement_Check_Project_All.html");
            string newer = Path.Combine(root, "EMA_AI_Requirement_Check_Project_All_2.html");
            File.WriteAllText(older, "<html>older</html>");
            File.WriteAllText(newer, "<html>newer</html>");

            File.SetLastWriteTimeUtc(older, new DateTime(2026, 6, 8, 9, 36, 32, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newer, new DateTime(2026, 6, 8, 11, 31, 26, DateTimeKind.Utc));

            ReportNavigatorResult result = ReportNavigatorService.DiscoverLatestReport(
                settings: new EmaSettings(),
                additionalSearchRoots: new[] { root },
                includeStandardRoots: false);

            Assert.Equal(Path.GetFullPath(newer), result.ReportPath);
        }

        [Fact]
        public void DiscoverLatestReport_IgnoresNonReportHtmlFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Navigator_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            File.WriteAllText(Path.Combine(root, "random.html"), "<html>random</html>");
            string report = Path.Combine(root, "EMA_AI_Requirement_Check_Project_All_20260608_113126.html");
            File.WriteAllText(report, "<html>report</html>");

            ReportNavigatorResult result = ReportNavigatorService.DiscoverLatestReport(
                settings: new EmaSettings(),
                additionalSearchRoots: new[] { root },
                includeStandardRoots: false);

            Assert.Equal(Path.GetFullPath(report), result.ReportPath);
        }

        [Fact]
        public void ReportNavigatorResultFactories_DoNotMapValidReportsToNoReportFound()
        {
            string path = Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Navigator_Tests", Guid.NewGuid().ToString("N"), "EMA_AI_Requirement_Check_Test_All_20260608_113126.html");

            ReportNavigatorResult loading = ReportNavigatorResult.CreateLoading(path, false, "Loading latest Owner Requirements report...", "Queued");
            ReportNavigatorResult manual = ReportNavigatorResult.CreateLoading(path, true, "Loading selected Owner Requirements report...", "Queued");
            ReportNavigatorResult fallback = ReportNavigatorResult.CreateWebViewFallback(path, false, "Navigation failed", "Opened in browser");
            ReportNavigatorResult invalid = ReportNavigatorResult.CreateInvalidReportPath(path, "Invalid");

            Assert.NotEqual(ReportNavigatorState.NoReportFound, loading.State);
            Assert.NotEqual(ReportNavigatorState.NoReportFound, manual.State);
            Assert.NotEqual(ReportNavigatorState.NoReportFound, fallback.State);
            Assert.Equal(ReportNavigatorState.InvalidReportPath, invalid.State);
        }
    }
}
