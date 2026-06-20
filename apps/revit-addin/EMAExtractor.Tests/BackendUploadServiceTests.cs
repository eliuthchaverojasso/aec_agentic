using System;
using System.IO;
using EMAExtractor.Models;
using EMAExtractor.Services;
using Xunit;

namespace EMAExtractor.Tests
{
    /// <summary>
    /// Tests for BackendUploadService pre-flight guards.
    /// These tests verify that invalid inputs are rejected without making
    /// any HTTP calls (all guards fire before the HttpClient is used).
    /// </summary>
    public class BackendUploadServiceTests
    {
        // ---------------------------------------------------------------
        // Pre-flight guards — no HTTP calls made
        // ---------------------------------------------------------------

        [Fact]
        public void UploadExportAsync_NullSettings_ReturnsFail()
        {
            UploadResult result = BackendUploadService.UploadExportAsync(null, "export.json");
            Assert.False(result.Success);
            Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UploadExportAsync_EmptyApiBaseUrl_ReturnsFail()
        {
            EmaSettings settings = new EmaSettings { ApiBaseUrl = "", ProjectId = 5 };
            UploadResult result = BackendUploadService.UploadExportAsync(settings, "export.json");
            Assert.False(result.Success);
            Assert.Contains("ApiBaseUrl", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UploadExportAsync_WhiteSpaceApiBaseUrl_ReturnsFail()
        {
            EmaSettings settings = new EmaSettings { ApiBaseUrl = "   ", ProjectId = 5 };
            UploadResult result = BackendUploadService.UploadExportAsync(settings, "export.json");
            Assert.False(result.Success);
        }

        [Fact]
        public void UploadExportAsync_ProjectIdZero_ReturnsFail()
        {
            EmaSettings settings = new EmaSettings { ApiBaseUrl = "http://localhost:8010", ProjectId = 0 };
            UploadResult result = BackendUploadService.UploadExportAsync(settings, "export.json");
            Assert.False(result.Success);
            Assert.Contains("ProjectId", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UploadExportAsync_NegativeProjectId_ReturnsFail()
        {
            EmaSettings settings = new EmaSettings { ApiBaseUrl = "http://localhost:8010", ProjectId = -1 };
            UploadResult result = BackendUploadService.UploadExportAsync(settings, "export.json");
            Assert.False(result.Success);
        }

        [Fact]
        public void UploadExportAsync_FileMissing_ReturnsFail()
        {
            EmaSettings settings = new EmaSettings
            {
                ApiBaseUrl = "http://localhost:8010",
                ProjectId  = 1
            };
            UploadResult result = BackendUploadService.UploadExportAsync(
                settings,
                Path.Combine("C:", "does", "not", "exist", "export.json"));
            Assert.False(result.Success);
            Assert.Contains("does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UploadExportAsync_EmptyExportPath_ReturnsFail()
        {
            EmaSettings settings = new EmaSettings
            {
                ApiBaseUrl = "http://localhost:8010",
                ProjectId  = 1
            };
            UploadResult result = BackendUploadService.UploadExportAsync(settings, "");
            Assert.False(result.Success);
        }

        [Fact]
        public void UploadExportAsync_NullExportPath_ReturnsFail()
        {
            EmaSettings settings = new EmaSettings
            {
                ApiBaseUrl = "http://localhost:8010",
                ProjectId  = 1
            };
            UploadResult result = BackendUploadService.UploadExportAsync(settings, null);
            Assert.False(result.Success);
        }

        // ---------------------------------------------------------------
        // Success=false fields are well-formed
        // ---------------------------------------------------------------

        [Fact]
        public void UploadExportAsync_FailResult_HasZeroStatusCode()
        {
            // Pre-flight failures have StatusCode = 0 (no HTTP round-trip occurred)
            EmaSettings settings = new EmaSettings { ApiBaseUrl = "", ProjectId = 0 };
            UploadResult result = BackendUploadService.UploadExportAsync(settings, "");
            Assert.Equal(0, result.StatusCode);
        }

        [Theory]
        [InlineData("revit_exports", "drawing")]
        [InlineData("owner_requirements", "owner_requirements")]
        [InlineData("owner requirements", "owner_requirements")]
        [InlineData("specification", "specification")]
        [InlineData("drawings", "drawing")]
        public void ResolveIntakeType_MapsToAcceptedBackendValues(string category, string expected)
        {
            Assert.Equal(expected, BackendUploadService.ResolveIntakeType(category));
        }
    }
}
