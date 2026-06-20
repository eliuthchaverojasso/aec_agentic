using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EMAExtractor.Enums;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public static class LandingStandardService
    {
        public const string DrawingsFolderName = "Drawings";
        public const string OwnerRequirementsFolderName = "Owner Requirements";
        public const string SpecificationsFolderName = "Specifications";
        public const string RevitExportsFolderName = "Revit Exports";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string NormalizeSlug(string value)
        {
            string working = string.IsNullOrWhiteSpace(value) ? "ema_project" : value.Trim().ToLowerInvariant();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                working = working.Replace(invalid.ToString(), "");
            }

            working = Regex.Replace(working, @"\s+", "_");
            working = Regex.Replace(working, @"[^a-z0-9_]+", "_");
            working = Regex.Replace(working, @"_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(working) ? "ema_project" : working;
        }

        public static string GetProjectSlug(EmaSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.ProjectFolderName))
            {
                return NormalizeSlug(settings.ProjectFolderName);
            }

            if (!string.IsNullOrWhiteSpace(settings.ProjectDisplayName))
            {
                return NormalizeSlug(settings.ProjectDisplayName);
            }

            return "ema_project";
        }

        public static string GetLandingProjectFolder(EmaSettings settings)
        {
            if (settings == null ||
                string.IsNullOrWhiteSpace(settings.LandingRoot) ||
                string.IsNullOrWhiteSpace(settings.ProjectFolderName))
            {
                return "";
            }

            return Path.Combine(settings.LandingRoot, settings.ProjectFolderName);
        }

        public static string GetRevitExportsFolder(EmaSettings settings)
        {
            string projectFolder = GetLandingProjectFolder(settings);
            return string.IsNullOrWhiteSpace(projectFolder)
                ? ""
                : Path.Combine(projectFolder, RevitExportsFolderName);
        }

        public static string ResolveExportFolder(EmaSettings settings)
        {
            if (settings != null && settings.UseLandingStructure)
            {
                string revitExportsFolder = GetRevitExportsFolder(settings);
                if (!string.IsNullOrWhiteSpace(revitExportsFolder))
                {
                    return revitExportsFolder;
                }
            }

            if (settings != null && !string.IsNullOrWhiteSpace(settings.DefaultOutputFolder))
            {
                return settings.DefaultOutputFolder;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EMA AI",
                "exports");
        }

        public static string BuildExportFileName(
            EmaSettings settings,
            ExportDiscipline discipline,
            ExportScope scope,
            DateTime exportedAt)
        {
            string projectSlug = GetProjectSlug(settings ?? new EmaSettings());
            string disciplineSlug = NormalizeSlug(discipline.ToString());
            string scopeSlug = NormalizeSlug(scope.ToString());
            string timestamp = exportedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return $"{projectSlug}__revit_export__{disciplineSlug}__{scopeSlug}__{timestamp}.json";
        }

        public static string EnsureUniquePath(string folder, string fileName)
        {
            string path = Path.Combine(folder, fileName);
            if (!File.Exists(path))
            {
                return path;
            }

            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            for (int version = 2; version < 1000; version++)
            {
                string candidate = Path.Combine(folder, $"{name}__v{version}{extension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(folder, $"{name}__{Guid.NewGuid().ToString("N").Substring(0, 8)}{extension}");
        }

        public static string GetMetadataPath(string outputPath)
        {
            string folder = Path.GetDirectoryName(outputPath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(outputPath) + ".meta.json";
            return Path.Combine(folder, fileName);
        }

        public static string GetRelativeLandingPath(EmaSettings settings, string path)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.LandingRoot) || string.IsNullOrWhiteSpace(path))
            {
                return "";
            }

            string root = Path.GetFullPath(settings.LandingRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return fullPath.Substring(root.Length);
        }

        public static void WriteExportMetadata(
            EmaSettings settings,
            string outputPath,
            string metadataPath,
            ExportDiscipline discipline,
            ExportScope scope,
            DateTime exportedAt,
            int elementCount,
            string revitDocumentTitle)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                ["client_code"] = settings.ClientCode ?? "",
                ["project_code"] = settings.ProjectCode ?? "",
                ["project_display_name"] = settings.ProjectDisplayName ?? "",
                ["project_folder_name"] = settings.ProjectFolderName ?? "",
                ["project_slug"] = GetProjectSlug(settings),
                ["source_system"] = "EMAExtractor",
                ["upload_method"] = "RevitPlugin",
                ["document_category"] = "Revit Export",
                ["discipline"] = NormalizeSlug(discipline.ToString()),
                ["scope"] = NormalizeSlug(scope.ToString()),
                ["exported_at"] = exportedAt.ToString("o", CultureInfo.InvariantCulture),
                ["schema_version"] = "0.2.0",
                ["export_profile"] = settings.ExportProfile ?? "Standard",
                ["api_base_url"] = settings.ApiBaseUrl ?? "",
                ["auto_submit_to_backend"] = settings.AutoSubmitToBackend,
                ["output_file"] = Path.GetFileName(outputPath),
                ["output_path"] = outputPath,
                ["relative_landing_path"] = GetRelativeLandingPath(settings, outputPath),
                ["element_count"] = elementCount,
                ["revit_document_title"] = revitDocumentTitle ?? "",
                ["environment_name"] = settings.EnvironmentName ?? "Local",
                ["backend_ingestion_status"] = settings.AutoSubmitToBackend ? "pending_submit" : "not_submitted",
                ["notes"] = settings.AutoSubmitToBackend ? "Backend upload requested by EMAExtractor." : "Backend ingestion is manual in current MVP."
            };

            File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions));
        }


        public static void UpdateBackendIngestionMetadata(
            string metadataPath,
            string status,
            string notes,
            UploadResult uploadResult)
        {
            if (string.IsNullOrWhiteSpace(metadataPath) || !File.Exists(metadataPath))
            {
                return;
            }

            try
            {
                Dictionary<string, object> metadata =
                    JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(metadataPath), JsonOptions)
                    ?? new Dictionary<string, object>();

                metadata["backend_ingestion_status"] = status ?? "";
                metadata["backend_upload_message"] = notes ?? "";
                metadata["backend_upload_updated_at"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);

                if (uploadResult != null)
                {
                    metadata["backend_upload_success"] = uploadResult.Success;
                    metadata["backend_upload_status_code"] = uploadResult.StatusCode;
                    metadata["backend_upload_url"] = uploadResult.Url ?? "";
                    metadata["backend_upload_response"] = uploadResult.ResponseBody ?? "";
                }

                File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions));
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to update export metadata backend status.", ex);
            }
        }

        public static IList<string> DetectProjectFolders(string landingRoot)
        {
            if (string.IsNullOrWhiteSpace(landingRoot) || !Directory.Exists(landingRoot))
            {
                return new List<string>();
            }

            return Directory.GetDirectories(landingRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string BuildValidationReport(EmaSettings settings)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Landing validation");
            builder.AppendLine($"Use landing structure: {settings.UseLandingStructure}");
            builder.AppendLine($"Landing root: {ValueOrMissing(settings.LandingRoot)}");
            builder.AppendLine($"Project folder: {ValueOrMissing(settings.ProjectFolderName)}");
            builder.AppendLine($"Project slug: {GetProjectSlug(settings)}");
            builder.AppendLine();

            if (string.IsNullOrWhiteSpace(settings.LandingRoot) || !Directory.Exists(settings.LandingRoot))
            {
                builder.AppendLine("Landing root exists: no");
                return builder.ToString();
            }

            builder.AppendLine("Landing root exists: yes");
            string projectFolder = GetLandingProjectFolder(settings);
            bool projectExists = !string.IsNullOrWhiteSpace(projectFolder) && Directory.Exists(projectFolder);
            builder.AppendLine($"Project folder exists: {(projectExists ? "yes" : "no")}");

            if (!projectExists)
            {
                builder.AppendLine();
                builder.AppendLine("Detected projects:");
                foreach (string project in DetectProjectFolders(settings.LandingRoot))
                {
                    builder.AppendLine($"- {project}");
                }
                return builder.ToString();
            }

            AppendFolderStatus(builder, projectFolder, DrawingsFolderName, "*.pdf;*.docx;*.xlsx");
            AppendFolderStatus(builder, projectFolder, OwnerRequirementsFolderName, "*.xlsx");
            AppendFolderStatus(builder, projectFolder, SpecificationsFolderName, "*.pdf;*.docx");
            AppendFolderStatus(builder, projectFolder, RevitExportsFolderName, "*.json");
            builder.AppendLine();
            builder.AppendLine($"Last export: {ValueOrMissing(settings.LastExportPath)}");
            builder.AppendLine($"Last export metadata: {ValueOrMissing(settings.LastExportMetadataPath)}");
            builder.AppendLine($"AutoSubmitToBackend: {settings.AutoSubmitToBackend}");
            builder.AppendLine("Manual ingestion note: Current MVP uses file-based export. Backend ingestion is manual.");
            return builder.ToString();
        }

        public static void EnsureLandingFoldersForExport(EmaSettings settings)
        {
            if (settings == null || !settings.UseLandingStructure)
            {
                return;
            }

            string projectFolder = GetLandingProjectFolder(settings);
            if (string.IsNullOrWhiteSpace(projectFolder))
            {
                return;
            }

            Directory.CreateDirectory(projectFolder);
            Directory.CreateDirectory(Path.Combine(projectFolder, DrawingsFolderName));
            Directory.CreateDirectory(Path.Combine(projectFolder, OwnerRequirementsFolderName));
            Directory.CreateDirectory(Path.Combine(projectFolder, SpecificationsFolderName));
            Directory.CreateDirectory(Path.Combine(projectFolder, RevitExportsFolderName));
        }

        /// <summary>
        /// Appends a note to the "notes" field of an existing .meta.json sidecar.
        /// Uses a safe string-replace against the known sentinel written by
        /// WriteExportMetadata. Silently no-ops on any I/O failure.
        /// </summary>
        public static void AppendMetadataNotes(string metadataPath, string note)
        {
            if (string.IsNullOrWhiteSpace(metadataPath) || !File.Exists(metadataPath))
                return;

            try
            {
                const string sentinel = "Backend ingestion is manual in current MVP.";
                string json = File.ReadAllText(metadataPath);
                if (json.Contains(sentinel))
                {
                    string updated = sentinel + " | " + note;
                    json = json.Replace(
                        "\"notes\": \"" + sentinel + "\"",
                        "\"notes\": \"" + updated + "\"");
                    File.WriteAllText(metadataPath, json);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to append metadata note.", ex);
            }
        }

        private static void AppendFolderStatus(StringBuilder builder, string projectFolder, string folderName, string patterns)
        {
            string path = Path.Combine(projectFolder, folderName);
            bool exists = Directory.Exists(path);
            int count = 0;
            if (exists)
            {
                HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string pattern in patterns.Split(';'))
                {
                    foreach (string file in Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly))
                    {
                        names.Add(file);
                    }
                }

                count = names.Count;
            }

            builder.AppendLine($"{folderName}: {(exists ? "exists" : "missing")} | matching files by name: {count}");
        }

        private static string ValueOrMissing(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(not set)" : value;
        }
    }
}
