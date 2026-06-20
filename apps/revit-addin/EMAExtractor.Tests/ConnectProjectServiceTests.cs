using System;
using System.IO;
using System.Text.Json;
using EMAExtractor.Models;
using EMAExtractor.Services;
using Xunit;

namespace EMAExtractor.Tests
{
    /// <summary>
    /// Tests for ConnectProjectService.Apply().
    /// Note: successful Apply() calls write to %AppData%\EMA AI\settings.json
    /// as an intended side effect. Tests verify return values; settings
    /// persistence is an integration concern covered by smoke tests.
    /// </summary>
    public class ConnectProjectServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public ConnectProjectServiceTests()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                "ema_connect_test_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        // ---------------------------------------------------------------
        // File-system guards
        // ---------------------------------------------------------------

        [Fact]
        public void Apply_FileNotFound_ReturnsFail()
        {
            ConnectionResult result = ConnectProjectService.Apply(
                Path.Combine("C:", "does", "not", "exist", "project_binding.json"));
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Apply_NullPath_ReturnsFail()
        {
            ConnectionResult result = ConnectProjectService.Apply(null);
            Assert.False(result.Success);
        }

        [Fact]
        public void Apply_EmptyPath_ReturnsFail()
        {
            ConnectionResult result = ConnectProjectService.Apply("");
            Assert.False(result.Success);
        }

        [Fact]
        public void Apply_BadJson_ReturnsFail()
        {
            string path = WriteTempFile("bad.json", "this is not valid json {{{");
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.False(result.Success);
        }

        // ---------------------------------------------------------------
        // Validation guards
        // ---------------------------------------------------------------

        [Fact]
        public void Apply_MissingProjectId_ReturnsFail()
        {
            string path = WriteBinding(new
            {
                project_id  = 0,
                sync_mode   = "local_landing",
                landing_root = @"C:\EMA-Landing"
            });
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.False(result.Success);
            Assert.Contains("project_id", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Apply_LocalLandingWithoutLandingRoot_ReturnsFail()
        {
            string path = WriteBinding(new
            {
                project_id   = 1,
                sync_mode    = "local_landing",
                landing_root = "",
                api_base_url = ""
            });
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.False(result.Success);
            Assert.Contains("landing_root", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Apply_CloudUploadWithoutApiBaseUrl_ReturnsFail()
        {
            string path = WriteBinding(new
            {
                project_id   = 1,
                sync_mode    = "cloud_upload",
                landing_root = "",
                api_base_url = ""
            });
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.False(result.Success);
            Assert.Contains("api_base_url", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------------
        // SyncMode derivation
        // ---------------------------------------------------------------

        [Fact]
        public void Apply_EmptySyncMode_WithLandingRoot_DerivesLocalLanding()
        {
            string path = WriteBinding(new
            {
                project_id           = 10,
                sync_mode            = "",
                landing_root         = @"C:\EMA-Landing",
                project_display_name = "Derived Local Project",
                api_base_url         = ""
            });
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.True(result.Success, "Expected success but got: " + result.Message);
            Assert.Equal("local_landing", result.SyncMode);
            Assert.Equal("Derived Local Project", result.ProjectName);
        }

        [Fact]
        public void Apply_NoSyncModeNoLandingRoot_FailsCloudValidation()
        {
            // Derives cloud_upload (no LandingRoot) but missing ApiBaseUrl → fail
            string path = WriteBinding(new
            {
                project_id   = 1,
                sync_mode    = "",
                landing_root = "",
                api_base_url = ""
            });
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.False(result.Success);
            Assert.Contains("api_base_url", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------------
        // Success paths
        // ---------------------------------------------------------------

        [Fact]
        public void Apply_ValidLocalBinding_ReturnsSuccess()
        {
            string path = WriteBinding(new
            {
                project_id           = 42,
                model_id             = 1,
                client_id            = 5,
                client_code          = "TEST_CLIENT",
                project_code         = "TEST_PROJ",
                project_display_name = "Test School",
                project_folder_name  = "Test School",
                api_base_url         = "http://localhost:8010",
                dashboard_url        = "http://localhost:5173",
                sync_mode            = "local_landing",
                landing_root         = @"C:\EMA-Landing"
            });
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.True(result.Success, "Expected success but got: " + result.Message);
            Assert.Equal("local_landing", result.SyncMode);
            Assert.Equal("Test School", result.ProjectName);
        }

        [Fact]
        public void Apply_ValidCloudBinding_ReturnsSuccess()
        {
            string path = WriteBinding(new
            {
                project_id           = 7,
                project_display_name = "Cloud School",
                sync_mode            = "cloud_upload",
                landing_root         = "",
                api_base_url         = "https://ema-ai.example.com"
            });
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.True(result.Success, "Expected success but got: " + result.Message);
            Assert.Equal("cloud_upload", result.SyncMode);
            Assert.Equal("Cloud School", result.ProjectName);
        }

        [Fact]
        public void Apply_MissingDisplayName_FallsBackToProjectId()
        {
            string path = WriteBinding(new
            {
                project_id           = 99,
                project_display_name = "",
                sync_mode            = "local_landing",
                landing_root         = @"C:\EMA-Landing"
            });
            ConnectionResult result = ConnectProjectService.Apply(path);
            Assert.True(result.Success, "Expected success but got: " + result.Message);
            Assert.Contains("99", result.ProjectName);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private string WriteBinding(object value)
        {
            string path = Path.Combine(
                _tempDir,
                "binding_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(value,
                new JsonSerializerOptions { WriteIndented = true }));
            return path;
        }

        private string WriteTempFile(string name, string content)
        {
            string path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, content);
            return path;
        }
    }
}
