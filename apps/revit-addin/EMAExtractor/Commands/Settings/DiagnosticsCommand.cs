using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.UI;

namespace EMAExtractor.Commands.Settings
{
    [Transaction(TransactionMode.Manual)]
    public class DiagnosticsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                Document document = commandData.Application.ActiveUIDocument?.Document;
                ModelessToolWindow.ShowDiagnostics(document);
            });
        }
    }
}
