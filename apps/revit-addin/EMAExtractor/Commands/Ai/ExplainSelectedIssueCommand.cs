using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Models;
using EMAExtractor.Services;
using EMAExtractor.UI;

namespace EMAExtractor.Commands.Ai
{
    [Transaction(TransactionMode.Manual)]
    public class ExplainSelectedIssueCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                ReportNavigatorResult discovery = ReportNavigatorService.DiscoverLatestReport(LocalConfigService.LoadSettings());
                EmaReportNavigatorWindow.ShowWindow(
                    discovery,
                    "ask",
                    "",
                    "Select a requirement row in the report first. EMA AI will explain the selected issue when context is available.");
            });
        }
    }
}
