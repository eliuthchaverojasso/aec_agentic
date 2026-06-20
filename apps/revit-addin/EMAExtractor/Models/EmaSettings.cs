using System;
using System.IO;

namespace EMAExtractor.Models
{
    public class EmaSettings
    {
        public string ApiBaseUrl { get; set; } = "http://ema-ai-demo.shokworks.io:8010";
        public string DashboardUrl { get; set; } = "http://ema-ai-demo.shokworks.io:5173";
        public string EnvironmentName { get; set; } = "Cloud";
        public string DefaultOutputFolder { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EMA AI", "exports");
        public string ExportProfile { get; set; } = "Standard";
        public bool AutoSubmitToBackend { get; set; } = false;
        public bool UseLandingStructure { get; set; } = false;
        public string LandingRoot { get; set; } = "";
        public string ProjectFolderName { get; set; } = "";
        public string ProjectDisplayName { get; set; } = "";
        public string ProjectCode { get; set; } = "";
        public string ClientCode { get; set; } = "";
        public string LastExportPath { get; set; } = "";
        public string LastExportMetadataPath { get; set; } = "";
        public string LastExportedAt { get; set; } = "";
        public string LastExportDiscipline { get; set; } = "";
        public string LastExportScope { get; set; } = "";
        public string LastRequirementReportPath { get; set; } = "";
        public string LastRequirementReportGeneratedAt { get; set; } = "";
        public string LastRequirementReportDiscipline { get; set; } = "";
        public string LastRequirementReportScope { get; set; } = "";
        public string LastRequirementReportClipboardSummary { get; set; } = "";
        public string LastRequirementWorkbookPath { get; set; } = "";
        public string LastRequirementsWorkbookName { get; set; } = "";
        public string LastRequirementsLoadStatus { get; set; } = "Not loaded";
        public string LastRequirementsLoadedAt { get; set; } = "";
        public int LastRequirementsRowCount { get; set; } = 0;
        public string LastRequirementsDetectedDisciplines { get; set; } = "";
        public string LastRequirementsSelectedDiscipline { get; set; } = "";
        public string LastRequirementsSelectedScope { get; set; } = "";
        public string LastModelSyncStatus { get; set; } = "Not synced";
        public int LastModelSyncElementCount { get; set; } = 0;
        public string LastModelSyncAt { get; set; } = "";
        public string LastModelSyncPath { get; set; } = "";
        public string LastModelSyncMessage { get; set; } = "";
        public int LastRequirementReportMetCount { get; set; } = 0;
        public int LastRequirementReportNotMetCount { get; set; } = 0;
        public int LastRequirementReportNeedsReviewCount { get; set; } = 0;
        public int LastRequirementReportNotApplicableCount { get; set; } = 0;
        public int LastRequirementReportInsufficientDataCount { get; set; } = 0;
        public double LastRequirementReportMatchScore { get; set; } = 0.0;
        public string LoggingLevel { get; set; } = "Info";
        public int ClientId { get; set; } = 0;
        public int ModelId { get; set; } = 0;
        public int ProjectId { get; set; } = 0;
        public string SyncMode { get; set; } = "local_landing";

        public void Normalize()
        {
            EnvironmentName = NormalizeEnvironmentName(EnvironmentName, ApiBaseUrl);

            if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            {
                ApiBaseUrl = GetApiBaseUrlForEnvironment(EnvironmentName);
            }

            if (string.IsNullOrWhiteSpace(DashboardUrl))
            {
                DashboardUrl = GetDashboardUrlForEnvironment(EnvironmentName);
            }

            LastRequirementsLoadStatus = string.IsNullOrWhiteSpace(LastRequirementsLoadStatus)
                ? "Not loaded"
                : LastRequirementsLoadStatus.Trim();
            LastModelSyncStatus = string.IsNullOrWhiteSpace(LastModelSyncStatus)
                ? "Not synced"
                : LastModelSyncStatus.Trim();
        }

        public void ApplyEnvironmentProfile(string profile)
        {
            EnvironmentName = NormalizeEnvironmentName(profile, ApiBaseUrl);
            ApiBaseUrl = GetApiBaseUrlForEnvironment(EnvironmentName);
            DashboardUrl = GetDashboardUrlForEnvironment(EnvironmentName);
        }

        public string GetEnvironmentLabel()
        {
            return NormalizeEnvironmentName(EnvironmentName, ApiBaseUrl);
        }

        public bool IsEnvironmentProfile(string profile)
        {
            return string.Equals(GetEnvironmentLabel(), NormalizeEnvironmentName(profile, null), StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeEnvironmentName(string value, string apiBaseUrl)
        {
            string candidate = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

            if (candidate.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("LAN", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("Cloud", StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Equals("LAN", StringComparison.OrdinalIgnoreCase) ? "LAN" : char.ToUpperInvariant(candidate[0]) + candidate.Substring(1).ToLowerInvariant();
            }

            if (candidate.Equals("Azure Pilot", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("Production", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("Remote", StringComparison.OrdinalIgnoreCase))
            {
                return "Cloud";
            }

            string source = string.IsNullOrWhiteSpace(apiBaseUrl) ? string.Empty : apiBaseUrl;
            if (source.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Local";
            }

            if (source.IndexOf("192.168.1.66", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "LAN";
            }

            if (source.IndexOf("192.168.1.69", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "LAN";
            }

            return "Cloud";
        }

        public static string GetApiBaseUrlForEnvironment(string environmentName)
        {
            switch (NormalizeEnvironmentName(environmentName, null))
            {
                case "Local":
                    return "http://localhost:8010";
                case "LAN":
                    return "http://192.168.1.69:8010";
                default:
                    return "http://ema-ai-demo.shokworks.io:8010";
            }
        }

        public static string GetDashboardUrlForEnvironment(string environmentName)
        {
            switch (NormalizeEnvironmentName(environmentName, null))
            {
                case "Local":
                    return "http://localhost:5173";
                case "LAN":
                    return "http://192.168.1.69:5173";
                default:
                    return "http://ema-ai-demo.shokworks.io:5173";
            }
        }
    }
}
