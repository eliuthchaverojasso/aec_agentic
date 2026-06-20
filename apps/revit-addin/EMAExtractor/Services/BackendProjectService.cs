using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace EMAExtractor.Services
{
    public class BackendProjectOption
    {
        public int ProjectId { get; set; }
        public int? ClientId { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectCode { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string ClientCode { get; set; } = "";
        public string ProjectFolderName { get; set; } = "";

        public string DisplayLabel
        {
            get
            {
                string code = string.IsNullOrWhiteSpace(ProjectCode) ? "" : $" | {ProjectCode}";
                string client = string.IsNullOrWhiteSpace(ClientName) ? "" : $" | {ClientName}";
                return $"{ProjectId} - {ProjectName}{code}{client}";
            }
        }
    }

    public class BackendProjectFetchResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public List<BackendProjectOption> Projects { get; set; } = new List<BackendProjectOption>();
    }

    public static class BackendProjectService
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<BackendProjectFetchResult> TestConnectionAsync(string apiBaseUrl)
        {
            try
            {
                string baseUrl = NormalizeBaseUrl(apiBaseUrl);
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    HttpResponseMessage response = await client.GetAsync(baseUrl + "/health").ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return new BackendProjectFetchResult
                    {
                        Ok = response.IsSuccessStatusCode,
                        Message = response.IsSuccessStatusCode
                            ? "Backend health check succeeded."
                            : $"Backend health check failed: {(int)response.StatusCode} {response.ReasonPhrase} {body}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new BackendProjectFetchResult
                {
                    Ok = false,
                    Message = "Could not reach EMA AI backend: " + ex.Message
                };
            }
        }

        public static async Task<BackendProjectFetchResult> GetProjectsAsync(string apiBaseUrl)
        {
            try
            {
                string baseUrl = NormalizeBaseUrl(apiBaseUrl);
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    HttpResponseMessage response = await client.GetAsync(baseUrl + "/api/v1/projects").ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        return new BackendProjectFetchResult
                        {
                            Ok = false,
                            Message = $"Project request failed: {(int)response.StatusCode} {response.ReasonPhrase} {body}"
                        };
                    }

                    List<BackendProjectOption> projects = ParseProjects(body);

                    if (projects.Count == 0)
                    {
                        return new BackendProjectFetchResult
                        {
                            Ok = false,
                            Projects = projects,
                            Message = "Backend is reachable but returned no projects. Create or bind a project in EMA AI first."
                        };
                    }

                    return new BackendProjectFetchResult
                    {
                        Ok = true,
                        Projects = projects,
                        Message = $"Loaded {projects.Count} project(s)."
                    };
                }
            }
            catch (Exception ex)
            {
                return new BackendProjectFetchResult
                {
                    Ok = false,
                    Message = "Could not load projects from EMA AI backend: " + ex.Message
                };
            }
        }

        private static string NormalizeBaseUrl(string value)
        {
            string url = string.IsNullOrWhiteSpace(value)
                ? "http://ema-ai-demo.shokworks.io:8010"
                : value.Trim();

            return url.TrimEnd('/');
        }

        private static List<BackendProjectOption> ParseProjects(string json)
        {
            List<BackendProjectOption> result = new List<BackendProjectOption>();
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                JsonElement array = root;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetProperty(root, "projects", out JsonElement projects))
                    {
                        array = projects;
                    }
                    else if (TryGetProperty(root, "items", out JsonElement items))
                    {
                        array = items;
                    }
                    else if (TryGetProperty(root, "data", out JsonElement data))
                    {
                        array = data;
                    }
                    else if (TryGetProperty(root, "results", out JsonElement results))
                    {
                        array = results;
                    }
                    else if (TryReadInt(root, new[] { "id", "project_id", "ProjectId" }, out int singleId))
                    {
                        BackendProjectOption single = BuildProject(root);
                        if (single.ProjectId > 0)
                        {
                            result.Add(single);
                        }
                        return result;
                    }
                }

                if (array.ValueKind != JsonValueKind.Array)
                {
                    return result;
                }

                foreach (JsonElement item in array.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    BackendProjectOption project = BuildProject(item);
                    if (project.ProjectId > 0)
                    {
                        result.Add(project);
                    }
                }
            }

            return result
                .GroupBy(p => p.ProjectId)
                .Select(g => g.First())
                .OrderBy(p => p.ProjectName)
                .ToList();
        }

        private static BackendProjectOption BuildProject(JsonElement item)
        {
            TryReadInt(item, new[] { "id", "project_id", "ProjectId" }, out int projectId);
            int clientIdValue;
            int? clientId = TryReadInt(item, new[] { "client_id", "ClientId" }, out clientIdValue)
                ? clientIdValue
                : (int?)null;

            string projectName = ReadString(item, new[] { "project_title", "project_name", "name", "display_name", "ProjectTitle", "ProjectName", "Name" }, "EMA Project");
            string projectCode = ReadString(item, new[] { "project_code", "code", "ProjectCode", "Code" }, Slug(projectName).ToUpperInvariant());
            string clientName = ReadString(item, new[] { "client_name", "client", "owner_name", "ClientName" }, "");
            string clientCode = ReadString(item, new[] { "client_code", "ClientCode" }, Slug(clientName).ToUpperInvariant());
            string folderName = ReadString(item, new[] { "project_folder_name", "landing_folder", "folder_name", "ProjectFolderName" }, projectName);

            return new BackendProjectOption
            {
                ProjectId = projectId,
                ClientId = clientId,
                ProjectName = projectName,
                ProjectCode = projectCode,
                ClientName = clientName,
                ClientCode = clientCode,
                ProjectFolderName = string.IsNullOrWhiteSpace(folderName) ? projectName : folderName
            };
        }

        private static string ReadString(JsonElement item, string[] names, string fallback)
        {
            foreach (string name in names)
            {
                if (TryGetProperty(item, name, out JsonElement value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        string s = value.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            return s.Trim();
                        }
                    }
                    else if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                    {
                        string s = value.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            return s.Trim();
                        }
                    }
                }
            }

            return fallback ?? "";
        }

        private static bool TryReadInt(JsonElement item, string[] names, out int value)
        {
            foreach (string name in names)
            {
                if (TryGetProperty(item, name, out JsonElement element))
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
                    {
                        return true;
                    }

                    if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value))
                    {
                        return true;
                    }
                }
            }

            value = 0;
            return false;
        }

        private static bool TryGetProperty(JsonElement item, string name, out JsonElement value)
        {
            foreach (JsonProperty prop in item.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default(JsonElement);
            return false;
        }

        private static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            char[] chars = value
                .Trim()
                .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')
                .ToArray();

            return new string(chars).Trim('_');
        }
    }
}
