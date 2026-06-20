using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.UI;

namespace EMAExtractor.Commands.Dashboard
{
    [Transaction(TransactionMode.Manual)]
    public class OpenEmaPanelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                EmaDashboardWindow.ShowWindow(commandData);
            });
        }
    }
}
