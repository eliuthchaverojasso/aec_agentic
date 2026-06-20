using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Core;
using EMAExtractor.Enums;

namespace EMAExtractor.Commands.SyncExport
{
    [Transaction(TransactionMode.Manual)]
    public class SyncModelDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                ExportRunner.Run(commandData, ExportDiscipline.All);
            });
        }
    }
}
