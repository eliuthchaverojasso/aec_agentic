using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EMAExtractor.Models;
using EMAExtractor.Services;
using Xunit;

namespace EMAExtractor.Tests
{
    public class OpenCodeModelConfigServiceTests
    {
        [Fact]
        public void LoadModelsFromPath_ParsesDefaultAndSmallModelsAndProviderMetadata()
        {
            string folder = Path.Combine(Path.GetTempPath(), "EMA_AI_OpenCode_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "opencode.json");

            File.WriteAllText(path, @"{
  ""model"": ""ollama/granite4.1:30b"",
  ""small_model"": ""ollama/qwen3.6:30b"",
  ""enabled_providers"": [""ollama"", ""openrouter""],
  ""provider"": {
    ""ollama"": {
      ""name"": ""Local Ollama"",
      ""options"": {
        ""baseURL"": ""http://localhost:11434/v1"",
        ""timeout"": 30000,
        ""chunkTimeout"": 5000
      },
      ""models"": {
        ""granite4.1:30b"": { ""name"": ""Granite 4.1 30B"" },
        ""qwen3.6:30b"": { ""name"": ""Qwen 3.6 30B"" }
      }
    },
    ""openrouter"": {
      ""name"": ""OpenRouter"",
      ""options"": {
        ""baseURL"": ""https://openrouter.ai/api/v1"",
        ""timeout"": 120000,
        ""chunkTimeout"": 30000
      },
      ""models"": {
        ""openai/gpt-4.1"": { ""name"": ""GPT-4.1"" }
      }
    },
    ""disabled_provider"": {
      ""name"": ""Disabled Provider"",
      ""models"": { ""foo"": { ""name"": ""Foo"" } }
    }
  }
}");

            try
            {
                List<AiModelOption> models = OpenCodeModelConfigService.LoadModelsFromPath(path);

                Assert.NotEmpty(models);
                Assert.Contains(models, item => item.IsDeterministic);
                Assert.Contains(models, item => item.ModelId == "ollama/granite4.1:30b" && item.IsDefault);
                Assert.Contains(models, item => item.ModelId == "ollama/qwen3.6:30b" && item.IsSmallModel);
                Assert.Contains(models, item => item.ProviderDisplayName == "Local Ollama");
                Assert.Contains(models, item => item.ProviderDisplayName == "OpenRouter");
                Assert.Contains(models, item => item.DisplayText.Contains("Default", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(models, item => item.DisplayText.Contains("Small", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(models, item => item.ProviderName == "disabled_provider");
            }
            finally
            {
                SafeDeleteFolder(folder);
            }
        }

        [Fact]
        public void LoadModelsFromPath_ReturnsDeterministicFallbackWhenMissing()
        {
            List<AiModelOption> models = OpenCodeModelConfigService.LoadModelsFromPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-opencode.json"));

            Assert.Contains(models, item => item.IsDeterministic);
            Assert.Contains(models, item => item.DisplayText.Contains("Fallback", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void LoadModelsFromPath_ReturnsDeterministicFallbackWhenMalformed()
        {
            string folder = Path.Combine(Path.GetTempPath(), "EMA_AI_OpenCode_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "opencode.json");
            File.WriteAllText(path, "{ not valid json");

            try
            {
                List<AiModelOption> models = OpenCodeModelConfigService.LoadModelsFromPath(path);

                Assert.Contains(models, item => item.IsDeterministic);
                Assert.True(models.Count >= 1);
            }
            finally
            {
                SafeDeleteFolder(folder);
            }
        }

        private static void SafeDeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
