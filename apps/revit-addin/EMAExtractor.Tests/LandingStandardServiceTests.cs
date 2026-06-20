using System;
using System.IO;
using EMAExtractor.Enums;
using EMAExtractor.Models;
using EMAExtractor.Services;
using Xunit;

namespace EMAExtractor.Tests
{
    public class LandingStandardServiceTests
    {
        // ---------------------------------------------------------------
        // NormalizeSlug
        // ---------------------------------------------------------------

        [Fact]
        public void NormalizeSlug_WithSpaces_ReplacesWithUnderscores()
        {
            Assert.Equal("hello_world", LandingStandardService.NormalizeSlug("Hello World"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void NormalizeSlug_EmptyOrWhiteSpace_ReturnsDefaultSlug(string input)
        {
            Assert.Equal("ema_project", LandingStandardService.NormalizeSlug(input));
        }

        [Fact]
        public void NormalizeSlug_WithSpecialChars_RemovesInvalid()
        {
            // '@', '#', '$' become '_', which collapses, trimmed
            string result = LandingStandardService.NormalizeSlug("Proj@#$2024");
            Assert.Equal("proj_2024", result);
        }

        [Fact]
        public void NormalizeSlug_AlreadyClean_ReturnsLowercase()
        {
            Assert.Equal("example_proj", LandingStandardService.NormalizeSlug("EXAMPLE_PROJ"));
        }

        [Fact]
        public void NormalizeSlug_OnlySpecialChars_ReturnsDefaultSlug()
        {
            Assert.Equal("ema_project", LandingStandardService.NormalizeSlug("@#$%^&*"));
        }

        // ---------------------------------------------------------------
        // BuildExportFileName
        // ---------------------------------------------------------------

        [Fact]
        public void BuildExportFileName_ContainsTimestamp()
        {
            EmaSettings settings = new EmaSettings { ProjectFolderName = "Test Project" };
            DateTime ts = new DateTime(2025, 3, 14, 9, 30, 0);
            string name = LandingStandardService.BuildExportFileName(
                settings, ExportDiscipline.Electrical, ExportScope.All, ts);
            Assert.Contains("20250314_093000", name);
            Assert.EndsWith(".json", name);
        }

        [Fact]
        public void BuildExportFileName_ProjectSlugIsNormalized()
        {
            EmaSettings settings = new EmaSettings { ProjectFolderName = "My School Project" };
            DateTime ts = new DateTime(2025, 1, 1, 0, 0, 0);
            string name = LandingStandardService.BuildExportFileName(
                settings, ExportDiscipline.All, ExportScope.All, ts);
            Assert.StartsWith("my_school_project__revit_export__", name);
        }

        [Fact]
        public void BuildExportFileName_DisciplineAndScopeAppear()
        {
            EmaSettings settings = new EmaSettings { ProjectFolderName = "School" };
            DateTime ts = new DateTime(2025, 6, 15, 10, 0, 0);
            string name = LandingStandardService.BuildExportFileName(
                settings, ExportDiscipline.Mechanical, ExportScope.Hvac, ts);
            Assert.Contains("mechanical", name);
            Assert.Contains("hvac", name);
        }

        // ---------------------------------------------------------------
        // EnsureUniquePath
        // ---------------------------------------------------------------

        [Fact]
        public void EnsureUniquePath_NoCollision_ReturnsOriginalPath()
        {
            string tempDir = CreateTempDir();
            try
            {
                string result = LandingStandardService.EnsureUniquePath(tempDir, "export.json");
                Assert.Equal(Path.Combine(tempDir, "export.json"), result);
            }
            finally { Directory.Delete(tempDir, true); }
        }

        [Fact]
        public void EnsureUniquePath_WithCollision_ReturnsV2()
        {
            string tempDir = CreateTempDir();
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "export.json"), "{}");
                string result = LandingStandardService.EnsureUniquePath(tempDir, "export.json");
                Assert.Equal(Path.Combine(tempDir, "export__v2.json"), result);
            }
            finally { Directory.Delete(tempDir, true); }
        }

        [Fact]
        public void EnsureUniquePath_MultipleCollisions_IncrementsVersion()
        {
            string tempDir = CreateTempDir();
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "export.json"), "{}");
                File.WriteAllText(Path.Combine(tempDir, "export__v2.json"), "{}");
                string result = LandingStandardService.EnsureUniquePath(tempDir, "export.json");
                Assert.Equal(Path.Combine(tempDir, "export__v3.json"), result);
            }
            finally { Directory.Delete(tempDir, true); }
        }

        // ---------------------------------------------------------------
        // GetMetadataPath
        // ---------------------------------------------------------------

        [Fact]
        public void GetMetadataPath_ReturnsCorrectSuffix()
        {
            string outputPath = Path.Combine("C:", "Landing", "Project", "Revit Exports", "export.json");
            string expected   = Path.Combine("C:", "Landing", "Project", "Revit Exports", "export.meta.json");
            Assert.Equal(expected, LandingStandardService.GetMetadataPath(outputPath));
        }

        [Fact]
        public void GetMetadataPath_PreservesFolder()
        {
            string outputPath = Path.Combine("C:", "output", "myfile.json");
            string metaPath   = LandingStandardService.GetMetadataPath(outputPath);
            Assert.Equal(Path.GetDirectoryName(outputPath), Path.GetDirectoryName(metaPath));
        }

        // ---------------------------------------------------------------
        // GetRevitExportsFolder
        // ---------------------------------------------------------------

        [Fact]
        public void GetRevitExportsFolder_WithValidSettings_ReturnsCorrectPath()
        {
            EmaSettings settings = new EmaSettings
            {
                LandingRoot         = Path.Combine("C:", "EMA-Landing"),
                ProjectFolderName   = "Example School Project",
                UseLandingStructure = true
            };
            string expected = Path.Combine("C:", "EMA-Landing", "Example School Project", "Revit Exports");
            Assert.Equal(expected, LandingStandardService.GetRevitExportsFolder(settings));
        }

        [Fact]
        public void GetRevitExportsFolder_MissingLandingRoot_ReturnsEmpty()
        {
            EmaSettings settings = new EmaSettings
            {
                LandingRoot       = "",
                ProjectFolderName = "SomeProject"
            };
            Assert.Equal("", LandingStandardService.GetRevitExportsFolder(settings));
        }

        [Fact]
        public void GetRevitExportsFolder_MissingProjectFolder_ReturnsEmpty()
        {
            EmaSettings settings = new EmaSettings
            {
                LandingRoot       = Path.Combine("C:", "EMA-Landing"),
                ProjectFolderName = ""
            };
            Assert.Equal("", LandingStandardService.GetRevitExportsFolder(settings));
        }

        // ---------------------------------------------------------------
        // AppendMetadataNotes
        // ---------------------------------------------------------------

        [Fact]
        public void AppendMetadataNotes_AppendsSentinel()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "test_meta_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                const string sentinel = "Backend ingestion is manual in current MVP.";
                File.WriteAllText(tempFile,
                    "{\n  \"notes\": \"" + sentinel + "\"\n}");

                LandingStandardService.AppendMetadataNotes(tempFile, "cloud_upload: succeeded HTTP 200");

                string updated = File.ReadAllText(tempFile);
                Assert.Contains("cloud_upload: succeeded HTTP 200", updated);
                Assert.Contains(sentinel + " |", updated);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void AppendMetadataNotes_FileNotFound_DoesNotThrow()
        {
            // Should silently no-op — not throw
            LandingStandardService.AppendMetadataNotes(
                Path.Combine("C:", "does", "not", "exist.meta.json"),
                "test note");
        }

        [Fact]
        public void AppendMetadataNotes_NullPath_DoesNotThrow()
        {
            LandingStandardService.AppendMetadataNotes(null, "test note");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static string CreateTempDir()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "ema_landing_test_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
