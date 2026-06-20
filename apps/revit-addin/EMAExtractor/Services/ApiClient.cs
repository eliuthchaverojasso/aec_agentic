using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public class ApiClient
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public ApiClient()
            : this(LocalConfigService.LoadSettings().ApiBaseUrl)
        {
        }

        public ApiClient(string baseUrl)
        {
            BaseUrl = (baseUrl ?? "http://ema-ai-demo.shokworks.io:8010").TrimEnd('/');
        }

        public string BaseUrl { get; }

        public async Task<string> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            return await GetStringAsync("/health", cancellationToken);
        }

        public async Task<List<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default)
        {
            return await GetAsync<List<ProjectDto>>("/api/v1/projects", cancellationToken) ?? new List<ProjectDto>();
        }

        public async Task<ProjectDto> GetProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            return await GetAsync<ProjectDto>($"/api/v1/projects/{projectId}", cancellationToken);
        }

        public async Task<List<ExportDto>> GetExportsAsync(CancellationToken cancellationToken = default)
        {
            return await GetAsync<List<ExportDto>>("/api/v1/exports", cancellationToken) ?? new List<ExportDto>();
        }

        public async Task<IssueListDto> GetIssuesAsync(int projectId, int pageSize = 25, CancellationToken cancellationToken = default)
        {
            string path = $"/api/v1/issues?project_id={projectId}&page_size={pageSize}";
            return await GetAsync<IssueListDto>(path, cancellationToken) ?? new IssueListDto();
        }

        public async Task<ReadinessDto> GetReadinessAsync(int projectId, CancellationToken cancellationToken = default)
        {
            return await GetAsync<ReadinessDto>($"/api/v1/projects/{projectId}/readiness", cancellationToken);
        }

        public async Task<List<ReadinessActionDto>> GetReadinessActionsAsync(int projectId, CancellationToken cancellationToken = default)
        {
            ReadinessDto readiness = await GetReadinessAsync(projectId, cancellationToken);
            return readiness?.recommended_actions ?? new List<ReadinessActionDto>();
        }

        public async Task<string> RecalculateReadinessAsync(int projectId, CancellationToken cancellationToken = default)
        {
            string url = BaseUrl + $"/api/v1/projects/{projectId}/readiness/recalculate";
            HttpResponseMessage response = await Http.PostAsync(url, new StringContent(""), cancellationToken);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
            }

            return body;
        }

        private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
        {
            string json = await GetStringAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<T>(json, _options);
        }

        private async Task<string> GetStringAsync(string path, CancellationToken cancellationToken)
        {
            string url = BaseUrl + path;
            HttpResponseMessage response = await Http.GetAsync(url, cancellationToken);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
            }

            return body;
        }
    }
}
