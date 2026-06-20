using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Models;
using EMAExtractor.Services;
using EMAExtractor.UI;

namespace EMAExtractor.Commands.Ai
{
    [Transaction(TransactionMode.Manual)]
    public class AskAboutReportCommand : IExternalCommand
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
                    "Ask EMA AI is ready. Select or type a question about the loaded report.");
            });
        }
    }
}
