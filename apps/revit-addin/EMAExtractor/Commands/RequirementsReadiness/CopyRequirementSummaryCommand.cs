using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Models;
using EMAExtractor.Services;
using System.Windows;

namespace EMAExtractor.Commands.RequirementsReadiness
{
    [Transaction(TransactionMode.Manual)]
    public class CopyRequirementSummaryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                EmaSettings settings = LocalConfigService.LoadSettings();
                string summary = string.Empty;

                if (!string.IsNullOrWhiteSpace(settings.LastRequirementReportPath) && File.Exists(settings.LastRequirementReportPath))
                {
                    ReportRagService rag = new ReportRagService();
                    if (rag.LoadFromHtmlFile(settings.LastRequirementReportPath))
                    {
                        summary = rag.BuildClipboardSummary();
                    }
                }

                if (string.IsNullOrWhiteSpace(summary))
                {
                    summary = settings.LastRequirementReportClipboardSummary;
                }

                if (string.IsNullOrWhiteSpace(summary))
                {
                    TaskDialog.Show("EMA AI", "Generate a report before copying the summary.");
                    return;
                }

                try
                {
                    Clipboard.SetText(summary);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("EMA AI", "Could not copy the summary: " + ex.Message);
                }
            });
        }
    }
}
