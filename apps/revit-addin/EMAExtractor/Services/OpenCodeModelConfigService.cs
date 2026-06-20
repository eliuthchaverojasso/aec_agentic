using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public static class OpenCodeModelConfigService
    {
        private static readonly AiModelOption Deterministic = new AiModelOption
        {
            Name = "Deterministic Report Summary (No AI)",
            ModelId = "deterministic",
            ProviderName = "Built-in",
            ProviderDisplayName = "Built-in",
            ProviderType = "Deterministic",
            BaseUrl = null,
            IsDefault = false,
            PrivacyMessage = "No data leaves your machine.",
            AvailabilityMessage = "Always available.",
            TimeoutMs = 0,
            ChunkTimeoutMs = 0
        };

        public static List<AiModelOption> LoadModels()
        {
            string path = LocateOpenCodeJson();
            return LoadModelsFromPath(path);
        }

        public static List<AiModelOption> LoadModelsFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return BuildFallbackList();
            }

            try
            {
                string json = File.ReadAllText(path);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    return ParseModels(doc.RootElement);
                }
            }
            catch
            {
                return BuildFallbackList();
            }
        }

        private static string LocateOpenCodeJson()
        {
            string[] candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "opencode.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "opencode.json"),
                Path.Combine(LoggingService.AppRoot ?? string.Empty, "opencode.json"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".opencode", "config.json")
            };

            foreach (string candidate in candidates)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch { }
            }

            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                while (!string.IsNullOrWhiteSpace(dir))
                {
                    string attempt = Path.Combine(dir, "opencode.json");
                    if (File.Exists(attempt))
                    {
                        return attempt;
                    }

                    DirectoryInfo parent = Directory.GetParent(dir);
                    if (parent == null || string.Equals(parent.FullName, dir, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    dir = parent.FullName;
                }
            }
            catch { }

            return null;
        }

        private static List<AiModelOption> ParseModels(JsonElement root)
        {
            List<AiModelOption> result = new List<AiModelOption>();
            result.Add(Deterministic);

            string defaultModelId = GetString(root, "model");
            string smallModelId = GetString(root, "small_model");
            HashSet<string> enabledProviders = GetEnabledProviders(root);

            if (!root.TryGetProperty("provider", out JsonElement providerSection))
            {
                return result;
            }

            foreach (JsonProperty providerEntry in providerSection.EnumerateObject())
            {
                string providerName = providerEntry.Name;
                JsonElement providerData = providerEntry.Value;

                if (enabledProviders.Count > 0 && !enabledProviders.Contains(providerName))
                {
                    continue;
                }

                string baseUrl = null;
                int timeoutMs = 120_000;
                int chunkTimeoutMs = 30_000;
                string providerDisplayName = ToDisplayProvider(providerName);

                if (providerData.TryGetProperty("options", out JsonElement opts))
                {
                    if (opts.TryGetProperty("baseURL", out JsonElement urlEl))
                        baseUrl = urlEl.GetString();

                    if (opts.TryGetProperty("timeout", out JsonElement toEl) &&
                        toEl.ValueKind == JsonValueKind.Number)
                        timeoutMs = toEl.GetInt32();

                    if (opts.TryGetProperty("chunkTimeout", out JsonElement ctEl) &&
                        ctEl.ValueKind == JsonValueKind.Number)
                        chunkTimeoutMs = ctEl.GetInt32();
                }

                if (providerData.TryGetProperty("name", out JsonElement providerNameEl) &&
                    providerNameEl.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(providerNameEl.GetString()))
                {
                    providerDisplayName = providerNameEl.GetString();
                }

                string providerType = IsCloudProvider(providerName) ? "Cloud" : "Local";
                string privacyMsg = providerType == "Local"
                    ? "Data stays local."
                    : "Report context will be sent to configured cloud provider.";
                string availMsg = providerType == "Local"
                    ? "Requires Ollama running locally."
                    : "Requires valid credentials and network access.";

                if (!providerData.TryGetProperty("models", out JsonElement models))
                {
                    continue;
                }

                if (models.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty modelEntry in models.EnumerateObject())
                    {
                        string modelId = modelEntry.Name;
                        string displayName = modelId;

                        if (modelEntry.Value.ValueKind == JsonValueKind.Object &&
                            modelEntry.Value.TryGetProperty("name", out JsonElement nameEl))
                        {
                            displayName = nameEl.GetString() ?? modelId;
                        }

                        string fullId = providerName + "/" + modelId;
                        bool isDefault = string.Equals(defaultModelId, fullId, StringComparison.OrdinalIgnoreCase);
                        bool isSmall = string.Equals(smallModelId, fullId, StringComparison.OrdinalIgnoreCase);

                        result.Add(new AiModelOption
                        {
                            Name = displayName,
                            ModelId = fullId,
                            ProviderName = providerName,
                            ProviderDisplayName = providerDisplayName,
                            ProviderType = providerType,
                            BaseUrl = baseUrl,
                            IsDefault = isDefault,
                            IsSmallModel = isSmall,
                            PrivacyMessage = privacyMsg,
                            AvailabilityMessage = availMsg,
                            TimeoutMs = timeoutMs,
                            ChunkTimeoutMs = chunkTimeoutMs
                        });
                    }
                }
                else if (models.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement modelEl in models.EnumerateArray())
                    {
                        string modelId = modelEl.GetString();
                        if (string.IsNullOrWhiteSpace(modelId))
                        {
                            continue;
                        }

                        string fullId = providerName + "/" + modelId;
                        bool isDefault = string.Equals(defaultModelId, fullId, StringComparison.OrdinalIgnoreCase);
                        bool isSmall = string.Equals(smallModelId, fullId, StringComparison.OrdinalIgnoreCase);

                        result.Add(new AiModelOption
                        {
                            Name = modelId,
                            ModelId = fullId,
                            ProviderName = providerName,
                            ProviderDisplayName = providerDisplayName,
                            ProviderType = providerType,
                            BaseUrl = baseUrl,
                            IsDefault = isDefault,
                            IsSmallModel = isSmall,
                            PrivacyMessage = privacyMsg,
                            AvailabilityMessage = availMsg,
                            TimeoutMs = timeoutMs,
                            ChunkTimeoutMs = chunkTimeoutMs
                        });
                    }
                }
            }

            return result;
        }

        private static List<AiModelOption> BuildFallbackList()
        {
            const string localPrivacy = "Data stays local.";
            const string cloudPrivacy = "Report context will be sent to configured cloud provider.";
            const string localAvail = "Requires Ollama running locally.";
            const string cloudAvail = "Requires valid credentials and network access.";

            return new List<AiModelOption>
            {
                Deterministic,
                new AiModelOption { Name = "Qwen 3.6 35B", ModelId = "ollama/qwen3.6:35b", ProviderName = "ollama", ProviderDisplayName = "Local Ollama", ProviderType = "Local", BaseUrl = "http://localhost:11434/v1", IsDefault = true, TimeoutMs = 600_000, ChunkTimeoutMs = 60_000, PrivacyMessage = localPrivacy, AvailabilityMessage = localAvail },
                new AiModelOption { Name = "Granite 4.1 30B", ModelId = "ollama/granite4.1:30b", ProviderName = "ollama", ProviderDisplayName = "Local Ollama", ProviderType = "Local", BaseUrl = "http://localhost:11434/v1", IsSmallModel = true, TimeoutMs = 600_000, ChunkTimeoutMs = 60_000, PrivacyMessage = localPrivacy, AvailabilityMessage = localAvail },
                new AiModelOption { Name = "Gemma 4 26B", ModelId = "ollama/gemma4:26b", ProviderName = "ollama", ProviderDisplayName = "Local Ollama", ProviderType = "Local", BaseUrl = "http://localhost:11434/v1", TimeoutMs = 600_000, ChunkTimeoutMs = 60_000, PrivacyMessage = localPrivacy, AvailabilityMessage = localAvail },
                new AiModelOption { Name = "Claude Sonnet 4", ModelId = "openrouter/anthropic/claude-sonnet-4", ProviderName = "openrouter", ProviderDisplayName = "OpenRouter", ProviderType = "Cloud", BaseUrl = "https://openrouter.ai/api/v1", TimeoutMs = 120_000, ChunkTimeoutMs = 30_000, PrivacyMessage = cloudPrivacy, AvailabilityMessage = cloudAvail },
                new AiModelOption { Name = "GPT-4.1", ModelId = "openrouter/openai/gpt-4.1", ProviderName = "openrouter", ProviderDisplayName = "OpenRouter", ProviderType = "Cloud", BaseUrl = "https://openrouter.ai/api/v1", TimeoutMs = 120_000, ChunkTimeoutMs = 30_000, PrivacyMessage = cloudPrivacy, AvailabilityMessage = cloudAvail }
            };
        }

        private static bool IsCloudProvider(string name)
        {
            return string.Equals(name, "openrouter", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "opencode", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "anthropic", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToDisplayProvider(string name)
        {
            if (string.Equals(name, "ollama", StringComparison.OrdinalIgnoreCase)) return "Local Ollama";
            if (string.Equals(name, "openrouter", StringComparison.OrdinalIgnoreCase)) return "OpenRouter";
            if (string.Equals(name, "opencode", StringComparison.OrdinalIgnoreCase)) return "OpenCode";
            return name;
        }

        private static string GetString(JsonElement el, string key)
        {
            try
            {
                if (el.TryGetProperty(key, out JsonElement val) && val.ValueKind == JsonValueKind.String)
                {
                    return val.GetString();
                }
            }
            catch { }
            return null;
        }

        private static HashSet<string> GetEnabledProviders(JsonElement root)
        {
            HashSet<string> enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (root.TryGetProperty("enabled_providers", out JsonElement providers) &&
                    providers.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement provider in providers.EnumerateArray())
                    {
                        if (provider.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        string value = provider.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            enabled.Add(value);
                        }
                    }
                }
            }
            catch { }

            return enabled;
        }
    }
}
