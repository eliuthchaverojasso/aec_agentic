using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Models;
using EMAExtractor.Services;

namespace EMAExtractor.Commands.Help
{
    [Transaction(TransactionMode.Manual)]
    public class OpenDashboardCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return CommandSafety.Execute(ref message, () =>
            {
                EmaSettings settings = LocalConfigService.LoadSettings();
                string url = string.IsNullOrWhiteSpace(settings.DashboardUrl)
                    ? EmaSettings.GetDashboardUrlForEnvironment(settings.GetEnvironmentLabel())
                    : settings.DashboardUrl;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            });
        }
    }
}
