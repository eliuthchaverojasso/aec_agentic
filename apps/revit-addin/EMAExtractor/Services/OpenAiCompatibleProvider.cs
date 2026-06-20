using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public sealed class OpenAiCompatibleProvider : IAiModelProvider
    {
        private readonly AiModelOption _model;
        private readonly HttpClient _http;

        public string ProviderName => _model.ProviderName ?? "OpenAI-compatible";

        public bool IsAvailable { get; private set; } = true;

        public OpenAiCompatibleProvider(AiModelOption model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            TimeSpan timeout = TimeSpan.FromMilliseconds(
                _model.TimeoutMs > 0 ? _model.TimeoutMs : TimeSpan.FromSeconds(45).TotalMilliseconds);
            _http = new HttpClient { Timeout = timeout };
        }

        public async Task<AiCompletionResult> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            int maxTokens = 512,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_model.BaseUrl))
            {
                return Fail("No base URL configured for provider.");
            }

            string endpoint = _model.BaseUrl.TrimEnd('/') + "/chat/completions";

            string modelId = _model.ModelId ?? string.Empty;
            if (modelId.Contains("/"))
            {
                int slash = modelId.IndexOf('/');
                modelId = modelId.Substring(slash + 1);
            }

            var body = new
            {
                model = modelId,
                max_tokens = maxTokens,
                temperature = 0.1,
                stream = false,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt ?? string.Empty },
                    new { role = "user", content = userPrompt ?? string.Empty }
                }
            };

            string requestJson = JsonSerializer.Serialize(body);
            using (StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content })
            {
                string apiKey = ResolveApiKey(_model.ProviderName);
                if (!string.IsNullOrWhiteSpace(apiKey))
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);

                try
                {
                    using (HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            IsAvailable = false;
                            return Fail("HTTP " + (int)response.StatusCode + " from provider.");
                        }

                        return ParseChatCompletion(responseBody);
                    }
                }
                catch (OperationCanceledException)
                {
                    return Fail("Request was cancelled or timed out.");
                }
                catch (HttpRequestException ex)
                {
                    IsAvailable = false;
                    return Fail("Network error: " + ex.Message);
                }
                catch (Exception ex)
                {
                    return Fail("Unexpected error: " + ex.Message);
                }
            }
        }

        private AiCompletionResult ParseChatCompletion(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("choices", out JsonElement choices) &&
                        choices.ValueKind == JsonValueKind.Array &&
                        choices.GetArrayLength() > 0)
                    {
                        JsonElement first = choices[0];
                        if (first.TryGetProperty("message", out JsonElement message) &&
                            message.TryGetProperty("content", out JsonElement contentEl))
                        {
                            string text = contentEl.GetString() ?? string.Empty;
                            return new AiCompletionResult
                            {
                                Success = true,
                                Content = text.Trim(),
                                ProviderName = ProviderName,
                                UsedFallback = false
                            };
                        }
                    }
                }
            }
            catch { }

            return Fail("Could not parse completion response.");
        }

        private static string ResolveApiKey(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName)) return null;
            if (providerName.Equals("openrouter", StringComparison.OrdinalIgnoreCase))
                return Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            if (providerName.Equals("opencode", StringComparison.OrdinalIgnoreCase))
                return Environment.GetEnvironmentVariable("OPENCODE_API_KEY");
            if (providerName.Equals("anthropic", StringComparison.OrdinalIgnoreCase))
                return Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            // Ollama and other local providers don't use API keys
            return null;
        }

        private AiCompletionResult Fail(string message)
        {
            return new AiCompletionResult
            {
                Success = false,
                Content = string.Empty,
                ErrorMessage = message,
                ProviderName = ProviderName,
                UsedFallback = false
            };
        }
    }
}
