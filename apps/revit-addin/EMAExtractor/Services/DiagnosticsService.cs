using System.Reflection;
using Autodesk.Revit.DB;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public static class DiagnosticsService
    {
        public static string BuildDiagnostics(Document document = null)
        {
            EmaSettings settings = LocalConfigService.LoadSettings();
            ProjectBinding binding = ProjectBindingService.Load(document);
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

            return
                "EMA AI Diagnostics\n" +
                $"Add-in version: {version}\n" +
                $"Active document: {(document == null ? "No active document" : document.Title)}\n" +
                $"Revit version: {(document == null ? "Unknown" : document.Application.VersionName)}\n" +
                $"API URL: {settings.ApiBaseUrl}\n" +
                $"Environment: {settings.EnvironmentName}\n" +
                $"Export profile: {settings.ExportProfile}\n" +
                $"Use landing structure: {settings.UseLandingStructure}\n" +
                $"Landing root: {settings.LandingRoot}\n" +
                $"Project folder: {settings.ProjectFolderName}\n" +
                $"Project slug: {LandingStandardService.GetProjectSlug(settings)}\n" +
                $"Last export: {settings.LastExportPath}\n" +
                $"Project ID: {binding.ProjectId}\n" +
                $"Model ID: {binding.ModelId}\n" +
                $"Client ID: {binding.ClientId}\n" +
                $"Milestone: {binding.CurrentMilestone}\n" +
                $"Settings: {LocalConfigService.SettingsPath}\n" +
                $"Binding: {LocalConfigService.BindingPath}\n" +
                $"Logs: {LoggingService.CurrentLogPath}";
        }
    }
}
