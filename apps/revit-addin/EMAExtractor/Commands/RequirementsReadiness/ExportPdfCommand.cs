using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Models;
using EMAExtractor.Services;

namespace EMAExtractor.Commands.RequirementsReadiness
{
    [Transaction(TransactionMode.Manual)]
    public class ExportPdfCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                EmaSettings settings = LocalConfigService.LoadSettings();
                string reportPath = settings.LastRequirementReportPath;

                if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
                {
                    TaskDialog.Show("EMA AI", "Generate a report before exporting to PDF.");
                    return;
                }

                Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
                TaskDialog.Show("EMA AI", "The HTML report opened in your browser. Use Print > Save as PDF to export it.");
            });
        }
    }
}
