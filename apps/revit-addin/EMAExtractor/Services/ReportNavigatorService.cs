using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public static class ReportNavigatorService
    {
        private const string ReportPattern = "EMA_AI_Requirement_Check_*.html";
        private static readonly Regex TimestampRegex = new Regex(
            @"_(?<timestamp>\d{8}_\d{6})\.html$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static ReportNavigatorResult DiscoverLatestReport(
            EmaSettings settings = null,
            IEnumerable<string> additionalSearchRoots = null,
            bool includeStandardRoots = true)
        {
            EmaSettings effectiveSettings = settings ?? LocalConfigService.LoadSettings();
            List<string> searchRoots = BuildSearchRoots(effectiveSettings, additionalSearchRoots, includeStandardRoots).ToList();

            List<ReportCandidate> candidates = new List<ReportCandidate>();
            foreach (string root in searchRoots)
            {
                candidates.AddRange(EnumerateCandidates(root));
            }

            List<ReportCandidate> uniqueCandidates = candidates
                .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.TimestampUtc).ThenByDescending(item => item.ModifiedUtc).First())
                .ToList();

            if (uniqueCandidates.Count == 0)
            {
                ReportNavigatorResult none = ReportNavigatorResult.CreateNoReportFound(
                    "Run Owner Requirements Check first. No EMA AI Owner Requirements report was found.",
                    searchRoots);
                none.SearchSummary = string.Format(
                    CultureInfo.InvariantCulture,
                    "Searched {0} location(s).",
                    searchRoots.Count);
                return none;
            }

            ReportCandidate latest = uniqueCandidates
                .OrderByDescending(candidate => candidate.TimestampUtc)
                .ThenByDescending(candidate => candidate.ModifiedUtc)
                .ThenByDescending(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .First();

            ReportNavigatorResult result = ReportNavigatorResult.CreateLoading(
                latest.Path,
                manualSelection: false,
                statusMessage: "Loading latest Owner Requirements report...",
                footerMessage: "Found latest report and queued it for loading.");
            result.SearchRoots.AddRange(searchRoots);
            result.CandidateCount = uniqueCandidates.Count;
            result.SearchSummary = string.Format(
                CultureInfo.InvariantCulture,
                "Found {0} report(s) across {1} location(s).",
                uniqueCandidates.Count,
                searchRoots.Count);
            result.ReportDirectory = Path.GetDirectoryName(latest.Path) ?? string.Empty;
            result.ReportPath = Path.GetFullPath(latest.Path);
            return result;
        }

        private static IEnumerable<string> BuildSearchRoots(
            EmaSettings settings,
            IEnumerable<string> additionalSearchRoots,
            bool includeStandardRoots)
        {
            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddPath(roots, settings != null ? settings.LastRequirementReportPath : null);
            AddPath(roots, GetDirectorySafe(settings != null ? settings.LastRequirementReportPath : null));
            AddPath(roots, settings != null ? settings.DefaultOutputFolder : null);
            if (includeStandardRoots)
            {
                AddPath(roots, LoggingService.AppRoot);
                AddPath(roots, Path.Combine(LoggingService.AppRoot, "exports"));
                AddPath(roots, Path.Combine(Path.GetTempPath(), "EMA_AI_Report_Tests"));
                AddPath(roots, AppDomain.CurrentDomain.BaseDirectory);
                AddPath(roots, Directory.GetCurrentDirectory());
            }

            if (additionalSearchRoots != null)
            {
                foreach (string root in additionalSearchRoots)
                {
                    AddPath(roots, root);
                }
            }

            if (includeStandardRoots)
            {
                foreach (string root in ExpandAncestorRoots(AppDomain.CurrentDomain.BaseDirectory))
                {
                    AddPath(roots, root);
                }

                foreach (string root in ExpandAncestorRoots(Directory.GetCurrentDirectory()))
                {
                    AddPath(roots, root);
                }
            }

            return roots;
        }

        private static IEnumerable<string> ExpandAncestorRoots(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                yield break;
            }

            string current = path;
            while (!string.IsNullOrWhiteSpace(current))
            {
                yield return Path.Combine(current, "artifacts", "EMAExtractor", "real-data-regeneration");
                yield return Path.Combine(current, "artifacts", "EMAExtractor");
                yield return Path.Combine(current, "Pipeline", "pipeline", "landing");

                DirectoryInfo parent = Directory.GetParent(current);
                if (parent == null || string.Equals(parent.FullName, current, StringComparison.OrdinalIgnoreCase))
                {
                    yield break;
                }

                current = parent.FullName;
            }
        }

        private static void AddPath(HashSet<string> roots, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                roots.Add(fullPath);
            }
            catch
            {
                // Ignore malformed candidate paths.
            }
        }

        private static string GetDirectorySafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetDirectoryName(path);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<ReportCandidate> EnumerateCandidates(string root)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    return Enumerable.Empty<ReportCandidate>();
                }

                if (File.Exists(root))
                {
                    if (!IsReportPath(root))
                    {
                        return Enumerable.Empty<ReportCandidate>();
                    }

                    ReportCandidate candidate = CreateCandidate(Path.GetFullPath(root));
                    return candidate == null
                        ? Enumerable.Empty<ReportCandidate>()
                        : new[] { candidate };
                }

                if (!Directory.Exists(root))
                {
                    return Enumerable.Empty<ReportCandidate>();
                }

                return Directory.EnumerateFiles(root, ReportPattern, SearchOption.AllDirectories)
                    .Where(IsReportPath)
                    .Select(path => CreateCandidate(path))
                    .Where(candidate => candidate != null)
                    .ToList();
            }
            catch
            {
                return Enumerable.Empty<ReportCandidate>();
            }
        }

        private static ReportCandidate CreateCandidate(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                return new ReportCandidate
                {
                    Path = fullPath,
                    TimestampUtc = GetReportTimestampUtc(fullPath),
                    ModifiedUtc = File.Exists(fullPath) ? File.GetLastWriteTimeUtc(fullPath) : DateTime.MinValue
                };
            }
            catch
            {
                return null;
            }
        }

        private static bool IsReportPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string fileName = Path.GetFileName(path);
            return !string.IsNullOrWhiteSpace(fileName) &&
                fileName.StartsWith("EMA_AI_Requirement_Check_", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime GetReportTimestampUtc(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return DateTime.MinValue;
            }

            try
            {
                string fileName = Path.GetFileName(path);
                Match match = TimestampRegex.Match(fileName ?? string.Empty);
                if (match.Success)
                {
                    string timestamp = match.Groups["timestamp"].Value;
                    DateTime parsed;
                    if (DateTime.TryParseExact(
                        timestamp,
                        "yyyyMMdd_HHmmss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out parsed))
                    {
                        return DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime();
                    }
                }

                return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private sealed class ReportCandidate
        {
            public string Path { get; set; }
            public DateTime TimestampUtc { get; set; }
            public DateTime ModifiedUtc { get; set; }
        }
    }
}
