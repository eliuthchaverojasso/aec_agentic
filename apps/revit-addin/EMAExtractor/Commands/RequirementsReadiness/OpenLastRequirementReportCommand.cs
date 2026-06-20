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
    public class OpenLastRequirementReportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                EmaSettings settings = LocalConfigService.LoadSettings();
                ReportNavigatorResult discovery = ReportNavigatorService.DiscoverLatestReport(settings);
                string reportPath = discovery.ReportPath;

                if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
                {
                    TaskDialog.Show("EMA AI", discovery.StatusMessage);
                    return;
                }

                Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
            });
        }
    }
}
