using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Core;
using EMAExtractor.Enums;

namespace EMAExtractor.Commands.SyncExport
{
    [Transaction(TransactionMode.Manual)]
    public class ExportLightingCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            return ExportRunner.Run(commandData, ExportDiscipline.Electrical, ExportScope.Lighting);
        }
    }
}
