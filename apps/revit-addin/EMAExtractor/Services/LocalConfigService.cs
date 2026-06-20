using System;
using System.IO;
using System.Text.Json;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public static class LocalConfigService
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string SettingsPath => Path.Combine(LoggingService.AppRoot, "settings.json");
        public static string BindingPath => Path.Combine(LoggingService.AppRoot, "project_binding.json");

        public static EmaSettings LoadSettings()
        {
            EmaSettings settings = Load(SettingsPath, new EmaSettings());
            settings.Normalize();
            return settings;
        }

        public static void SaveSettings(EmaSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.DefaultOutputFolder))
            {
                Directory.CreateDirectory(settings.DefaultOutputFolder);
            }
            Save(SettingsPath, settings);
            LoggingService.Info("Settings saved.");
        }

        public static ProjectBinding LoadBinding()
        {
            return Load(BindingPath, new ProjectBinding());
        }

        public static void SaveBinding(ProjectBinding binding)
        {
            Save(BindingPath, binding);
            LoggingService.Info($"Project binding saved: project_id={binding.ProjectId}, model_id={binding.ModelId}, client_id={binding.ClientId}");
        }

        public static void ClearBinding()
        {
            if (File.Exists(BindingPath))
            {
                File.Delete(BindingPath);
                LoggingService.Info("Project binding cleared.");
            }
        }

        private static T Load<T>(string path, T fallback)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Save(path, fallback);
                    return fallback;
                }

                string json = File.ReadAllText(path);
                T value = JsonSerializer.Deserialize<T>(json, Options);
                return value == null ? fallback : value;
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to load local config: {path}", ex);
                return fallback;
            }
        }

        private static void Save<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
    }
}
