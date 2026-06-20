using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using EMAExtractor.Models;
using EMAExtractor.Enums;

namespace EMAExtractor.Services
{
    public static class BackendUploadService
    {
        public static UploadResult UploadProjectFile(
            EmaSettings settings,
            string filePath,
            string category,
            bool autoIngest)
        {
            UploadResult result = new UploadResult
            {
                Success = false,
                FilePath = filePath ?? "",
                Category = category ?? "revit_exports"
            };

            if (settings == null)
            {
                result.Message = "Settings are missing.";
                return result;
            }

            if (settings.ProjectId <= 0)
            {
                result.Message = "ProjectId is not configured in EMA AI settings.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            {
                result.Message = "ApiBaseUrl is not configured in EMA AI settings.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                result.Message = "Export file does not exist: " + filePath;
                return result;
            }

            string apiBase = settings.ApiBaseUrl.TrimEnd('/');
            string url = apiBase + "/api/v1/projects/" + settings.ProjectId + "/files/upload";
            result.Url = url;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);

                    using (MultipartFormDataContent form = new MultipartFormDataContent())
                    {
                        string resolvedCategory = string.IsNullOrWhiteSpace(category) ? "revit_exports" : category;
                        string resolvedIntakeType = ResolveIntakeType(resolvedCategory);

                        form.Add(new StringContent(resolvedCategory), "category");
                        form.Add(new StringContent(resolvedIntakeType), "intake_type");
                        form.Add(new StringContent(autoIngest ? "true" : "false"), "auto_ingest");

                        byte[] bytes = File.ReadAllBytes(filePath);
                        ByteArrayContent fileContent = new ByteArrayContent(bytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                        form.Add(fileContent, "files", Path.GetFileName(filePath));

                        HttpResponseMessage response = client.PostAsync(url, form).GetAwaiter().GetResult();
                        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        result.StatusCode = (int)response.StatusCode;
                        result.ResponseBody = body;
                        result.Success = response.IsSuccessStatusCode;
                        result.Message = result.Success
                            ? "Upload succeeded."
                            : "Upload failed: HTTP " + result.StatusCode + " - " + body;

                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = "Upload exception: " + ex.Message;
                result.ResponseBody = ex.ToString();
                LoggingService.Error("Backend upload failed.", ex);
                return result;
            }
        }

        public static UploadResult UploadExportAsync(
            EmaSettings settings,
            string filePath,
            string category,
            bool autoIngest)
        {
            return UploadProjectFile(settings, filePath, category, autoIngest);
        }

        public static UploadResult UploadExportAsync(
            EmaSettings settings,
            string filePath)
        {
            return UploadExportAsync(settings, filePath, "revit_exports", false);
        }

        public static UploadResult UploadRevitExportSync(
            EmaSettings settings,
            string filePath,
            ExportDiscipline discipline)
        {
            UploadResult result = new UploadResult
            {
                Success = false,
                FilePath = filePath ?? "",
                Category = ResolveExportType(discipline)
            };

            if (settings == null)
            {
                result.Message = "Settings are missing.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            {
                result.Message = "ApiBaseUrl is not configured in EMA AI settings.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                result.Message = "Export file does not exist: " + filePath;
                return result;
            }

            string apiBase = settings.ApiBaseUrl.TrimEnd('/');
            string url = apiBase + "/api/v1/exports/sync";
            result.Url = url;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);

                    using (MultipartFormDataContent form = new MultipartFormDataContent())
                    {
                        string exportType = ResolveExportType(discipline);

                        form.Add(new StringContent(exportType), "export_type");

                        byte[] bytes = File.ReadAllBytes(filePath);
                        ByteArrayContent fileContent = new ByteArrayContent(bytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                        form.Add(fileContent, "file", Path.GetFileName(filePath));

                        HttpResponseMessage response = client.PostAsync(url, form).GetAwaiter().GetResult();
                        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        result.StatusCode = (int)response.StatusCode;
                        result.ResponseBody = body;
                        result.Success = response.IsSuccessStatusCode;
                        result.Message = result.Success
                            ? "Revit export sync succeeded."
                            : "Revit export sync failed: HTTP " + result.StatusCode + " - " + body;

                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = "Revit export sync exception: " + ex.Message;
                result.ResponseBody = ex.ToString();
                LoggingService.Error("Revit export sync failed.", ex);
                return result;
            }
        }

        private static string ResolveExportType(ExportDiscipline discipline)
        {
            string value = discipline.ToString().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(value) ? "all" : value;
        }

        internal static string ResolveIntakeType(string category)
        {
            string normalized = string.IsNullOrWhiteSpace(category)
                ? string.Empty
                : category.Trim().ToLowerInvariant();

            if (normalized.Contains("owner_requirements") || normalized.Contains("owner requirements"))
            {
                return "owner_requirements";
            }

            if (normalized.Contains("specification") || normalized.Contains("spec"))
            {
                return "specification";
            }

            if (normalized.Contains("drawing") || normalized.Contains("revit") || normalized.Contains("export") || normalized.Contains("model"))
            {
                return "drawing";
            }

            return "drawing";
        }
    }
}
