using System;
using System.IO;
using System.Text.Json;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    /// <summary>
    /// Reads a project_binding.json file produced by the EMA AI web app,
    /// validates it, and merges the values into EmaSettings and ProjectBinding.
    /// No Revit API references — fully unit-testable.
    /// </summary>
    public static class ConnectProjectService
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // Accept snake_case keys from the web-app-generated binding file
            // (e.g. "project_id" → ProjectId, "sync_mode" → SyncMode).
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public static ConnectionResult Apply(string jsonPath)
        {
            // ----------------------------------------------------------------
            // 1. Read file
            // ----------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                return Fail("File not found: " + jsonPath);

            ProjectBindingFile bindingFile;
            try
            {
                string json = File.ReadAllText(jsonPath);
                bindingFile = JsonSerializer.Deserialize<ProjectBindingFile>(json, Options);
            }
            catch (Exception ex)
            {
                return Fail("Could not parse project binding file: " + ex.Message);
            }

            if (bindingFile == null)
                return Fail("Project binding file was empty or invalid.");

            // ----------------------------------------------------------------
            // 2. Derive SyncMode when missing
            // ----------------------------------------------------------------
            string syncMode = (bindingFile.SyncMode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(syncMode))
            {
                syncMode = !string.IsNullOrWhiteSpace(bindingFile.LandingRoot)
                    ? "local_landing"
                    : "cloud_upload";
            }

            // ----------------------------------------------------------------
            // 3. Validate
            // ----------------------------------------------------------------
            if (bindingFile.ProjectId <= 0)
                return Fail("project_id must be greater than 0. Check your project_binding.json.");

            if (syncMode == "local_landing" && string.IsNullOrWhiteSpace(bindingFile.LandingRoot))
                return Fail("landing_root is required when sync_mode is 'local_landing'. Check your project_binding.json.");

            if (syncMode == "cloud_upload" && string.IsNullOrWhiteSpace(bindingFile.ApiBaseUrl))
                return Fail("api_base_url is required when sync_mode is 'cloud_upload'. Check your project_binding.json.");

            // ----------------------------------------------------------------
            // 4. Merge into EmaSettings
            // ----------------------------------------------------------------
            EmaSettings settings = LocalConfigService.LoadSettings();
            settings.ProjectId           = bindingFile.ProjectId;
            settings.SyncMode            = syncMode;
            settings.UseLandingStructure = syncMode == "local_landing";
            settings.ClientCode          = bindingFile.ClientCode ?? settings.ClientCode;
            settings.ProjectCode         = bindingFile.ProjectCode ?? settings.ProjectCode;
            settings.ProjectDisplayName  = bindingFile.ProjectDisplayName ?? settings.ProjectDisplayName;
            settings.ProjectFolderName   = bindingFile.ProjectFolderName ?? settings.ProjectFolderName;

            // Only overwrite LandingRoot if the binding file provides one
            if (!string.IsNullOrWhiteSpace(bindingFile.LandingRoot))
                settings.LandingRoot = bindingFile.LandingRoot;

            if (!string.IsNullOrWhiteSpace(bindingFile.ApiBaseUrl))
                settings.ApiBaseUrl = bindingFile.ApiBaseUrl;
            if (!string.IsNullOrWhiteSpace(bindingFile.DashboardUrl))
                settings.DashboardUrl = bindingFile.DashboardUrl;
            if (!string.IsNullOrWhiteSpace(bindingFile.EnvironmentName))
                settings.EnvironmentName = EmaSettings.NormalizeEnvironmentName(bindingFile.EnvironmentName, settings.ApiBaseUrl);

            LocalConfigService.SaveSettings(settings);

            // ----------------------------------------------------------------
            // 5. Merge into ProjectBinding
            // ----------------------------------------------------------------
            ProjectBinding binding = LocalConfigService.LoadBinding();
            binding.ProjectId          = bindingFile.ProjectId;
            binding.ModelId            = bindingFile.ModelId;
            binding.ClientId           = bindingFile.ClientId;
            binding.ClientCode         = bindingFile.ClientCode ?? binding.ClientCode;
            binding.ProjectCode        = bindingFile.ProjectCode ?? binding.ProjectCode;
            binding.ProjectDisplayName = bindingFile.ProjectDisplayName ?? binding.ProjectDisplayName;
            binding.ProjectFolderName  = bindingFile.ProjectFolderName ?? binding.ProjectFolderName;
            binding.ProjectSlug        = LandingStandardService.NormalizeSlug(
                string.IsNullOrWhiteSpace(bindingFile.ProjectFolderName)
                    ? binding.ProjectFolderName
                    : bindingFile.ProjectFolderName);

            LocalConfigService.SaveBinding(binding);

            LoggingService.Info(
                string.Format("ConnectProject applied: project_id={0}, sync_mode={1}, project={2}",
                    bindingFile.ProjectId, syncMode, bindingFile.ProjectDisplayName));

            string projectName = string.IsNullOrWhiteSpace(bindingFile.ProjectDisplayName)
                ? "Project " + bindingFile.ProjectId
                : bindingFile.ProjectDisplayName;

            return new ConnectionResult
            {
                Success     = true,
                Message     = string.Format("Connected to '{0}' ({1}).", projectName, syncMode),
                ProjectName = projectName,
                SyncMode    = syncMode
            };
        }

        private static ConnectionResult Fail(string message)
        {
            LoggingService.Error("ConnectProject failed: " + message);
            return new ConnectionResult { Success = false, Message = message };
        }
    }
}
