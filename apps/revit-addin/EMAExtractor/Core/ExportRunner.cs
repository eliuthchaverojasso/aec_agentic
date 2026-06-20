using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Enums;
using EMAExtractor.Models;
using EMAExtractor.Services;
using EMAExtractor.UI;

namespace EMAExtractor.Core
{
    public static class ExportRunner
    {
        public static Result Run(ExternalCommandData commandData, ExportDiscipline discipline)
        {
            return Run(commandData, discipline, ExportScope.All);
        }

        public static Result Run(
            ExternalCommandData commandData,
            ExportDiscipline discipline,
            ExportScope scope)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            if (uidoc == null || uidoc.Document == null)
            {
                TaskDialog.Show("EMA AI", "No active Revit document found.");
                return Result.Failed;
            }

            Document doc = uidoc.Document;

            try
            {
                EmaSettings settings = LocalConfigService.LoadSettings();
                LogExportPhase("loading settings");
                settings.LastModelSyncStatus = "Syncing";
                settings.LastModelSyncMessage = "Preparing model export.";
                LocalConfigService.SaveSettings(settings);

                IList<BuiltInCategory> categories = ExportUtils.GetCategoriesForDiscipline(discipline, scope);

                DateTime exportedAt = DateTime.Now;
                string outputFolder = LandingStandardService.ResolveExportFolder(settings);

                if (settings.UseLandingStructure)
                {
                    LandingStandardService.EnsureLandingFoldersForExport(settings);
                    outputFolder = LandingStandardService.ResolveExportFolder(settings);
                }

                Directory.CreateDirectory(outputFolder);

                string fileName = LandingStandardService.BuildExportFileName(settings, discipline, scope, exportedAt);
                string outputPath = LandingStandardService.EnsureUniquePath(outputFolder, fileName);
                string metadataPath = LandingStandardService.GetMetadataPath(outputPath);

                ExportJob job = ExportJobService.StartJob(discipline, outputPath);

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                ProgressWindow progressWindow = new ProgressWindow(discipline.ToString(), scope.ToString(), outputPath);
                progressWindow.Show();
                progressWindow.SetStatus("loading settings");
                progressWindow.SetStatus("resolving project");
                progressWindow.SetStatus("resolving output folder");
                progressWindow.SetStatus("counting elements");

                int totalElements = CountElements(doc, categories);
                int exportedElements = 0;

                progressWindow.SetStatus("writing JSON");
                progressWindow.SetProgress(0, totalElements);

                using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartArray();

                    foreach (BuiltInCategory builtInCategory in categories)
                    {
                        FilteredElementCollector collector = new FilteredElementCollector(doc)
                            .OfCategory(builtInCategory)
                            .WhereElementIsNotElementType();

                        foreach (Element element in collector)
                        {
                            if (progressWindow.CancelRequested)
                            {
                                progressWindow.SetStatus("cancelled");
                                job.Status = "cancelled";
                                job.Phase = "cancelled";
                                job.CompletedAt = DateTime.Now;
                                LoggingService.Info($"Export job cancelled: {job.JobId}");
                                return Result.Cancelled;
                            }

                            ExportElementRecord record = ExportUtils.BuildElementRecord(doc, element);
                            record.SchemaVersion = "0.2.0";
                            record.ExportProfile = settings.ExportProfile;
                            record.Discipline = discipline.ToString().ToUpperInvariant();
                            record.Scope = scope.ToString();

                            ApplyExportProfile(record, settings.ExportProfile);
                            JsonSerializer.Serialize(writer, record, options);

                            exportedElements++;

                            if (exportedElements % 25 == 0 || exportedElements == totalElements)
                            {
                                ExportJobService.Update(job, "writing_json", exportedElements, totalElements);
                                progressWindow.SetProgress(exportedElements, totalElements);
                                progressWindow.SetStatus($"writing JSON - {exportedElements} of {totalElements} elements");
                                writer.Flush();
                            }
                        }
                    }

                    writer.WriteEndArray();
                    writer.Flush();
                }

                progressWindow.SetProgress(exportedElements, Math.Max(exportedElements, 1));
                progressWindow.SetStatus("writing metadata");

                LandingStandardService.WriteExportMetadata(
                    settings,
                    outputPath,
                    metadataPath,
                    discipline,
                    scope,
                    exportedAt,
                    exportedElements,
                    doc.Title);

                UploadResult uploadResult = null;

                if (settings.AutoSubmitToBackend)
                {
                    progressWindow.SetStatus("uploading to EMA AI backend");

                    uploadResult = BackendUploadService.UploadRevitExportSync(
                        settings,
                        outputPath,
                        discipline);

                    LandingStandardService.UpdateBackendIngestionMetadata(
                        metadataPath,
                        uploadResult.Success ? "submitted" : "upload_failed",
                        uploadResult.Message,
                        uploadResult);
                }
                else
                {
                    LandingStandardService.UpdateBackendIngestionMetadata(
                        metadataPath,
                        "not_submitted",
                        "AutoSubmitToBackend is disabled.",
                        null);
                }

                settings.LastExportPath = outputPath;
                settings.LastExportMetadataPath = metadataPath;
                settings.LastExportedAt = exportedAt.ToString("o");
                settings.LastExportDiscipline = discipline.ToString();
                settings.LastExportScope = scope.ToString();
                settings.LastModelSyncStatus = "Synced";
                settings.LastModelSyncElementCount = exportedElements;
                settings.LastModelSyncAt = exportedAt.ToString("o");
                settings.LastModelSyncPath = outputPath;
                settings.LastModelSyncMessage = settings.AutoSubmitToBackend
                    ? (uploadResult != null && uploadResult.Success
                        ? "Model data synced and uploaded successfully."
                        : uploadResult != null
                            ? uploadResult.Message
                            : "Model data synced locally.")
                    : "Model data synced locally. Backend upload skipped.";

                LocalConfigService.SaveSettings(settings);

                progressWindow.SetMetadataPath(metadataPath);
                progressWindow.SetStatus($"completed - {exportedElements} elements");

                ExportJobService.Complete(job, exportedElements);
                ModelessToolWindow.ShowExportComplete(exportedElements, outputPath, metadataPath, uploadResult);

                return Result.Succeeded;
            }
            catch (OutOfMemoryException ex)
            {
                EmaSettings settings = LocalConfigService.LoadSettings();
                settings.LastModelSyncStatus = "Failed";
                settings.LastModelSyncMessage = "Export ran out of memory: " + ex.Message;
                LocalConfigService.SaveSettings(settings);
                TaskDialog.Show(
                    "EMA AI Export Memory Error",
                    "The export ran out of memory. EMA AI now writes JSON using a streaming writer; try a smaller discipline export or Light/Standard profile if this persists.\n\n" + ex.Message
                );
                LoggingService.Error("Export ran out of memory.", ex);
                return Result.Failed;
            }
            catch (Exception ex)
            {
                EmaSettings settings = LocalConfigService.LoadSettings();
                settings.LastModelSyncStatus = "Failed";
                settings.LastModelSyncMessage = ex.Message;
                LocalConfigService.SaveSettings(settings);
                LoggingService.Error("Export failed.", ex);
                TaskDialog.Show("EMA AI Export Error", ex.ToString());
                return Result.Failed;
            }
        }

        private static void LogExportPhase(string phase)
        {
            LoggingService.Info($"Export phase: {phase}");
        }

        private static void ApplyExportProfile(ExportElementRecord record, string exportProfile)
        {
            string profile = string.IsNullOrWhiteSpace(exportProfile)
                ? "Standard"
                : exportProfile.Trim();

            if (profile.Equals("Full", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (profile.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                record.TypeParameters.Clear();
                record.InstanceParameters = FilterParameters(record.InstanceParameters, new[]
                {
                    "Panel",
                    "Circuit Number",
                    "Supply From",
                    "System Name",
                    "Comments",
                    "Mark"
                });
                return;
            }

            record.TypeParameters = FilterParameters(record.TypeParameters, new[]
            {
                "Type Mark",
                "Description",
                "Manufacturer",
                "Model",
                "Voltage",
                "Apparent Load"
            });
        }

        private static Dictionary<string, ParameterRecord> FilterParameters(
            Dictionary<string, ParameterRecord> source,
            IEnumerable<string> allowedNames)
        {
            Dictionary<string, ParameterRecord> filtered =
                new Dictionary<string, ParameterRecord>(StringComparer.OrdinalIgnoreCase);

            HashSet<string> allowed = new HashSet<string>(allowedNames, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, ParameterRecord> item in source)
            {
                if (allowed.Contains(item.Key))
                {
                    filtered[item.Key] = item.Value;
                }
            }

            return filtered;
        }

        private static int CountElements(Document doc, IEnumerable<BuiltInCategory> categories)
        {
            int total = 0;

            foreach (BuiltInCategory builtInCategory in categories)
            {
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfCategory(builtInCategory)
                    .WhereElementIsNotElementType();

                total += collector.GetElementCount();
            }

            return total;
        }
    }
}
