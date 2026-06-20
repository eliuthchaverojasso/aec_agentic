using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.UI;

namespace EMAExtractor.Commands.RequirementsReadiness
{
    [Transaction(TransactionMode.Manual)]
    public class ReadinessPreviewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                ModelessToolWindow.ShowReadinessPreview();
            });
        }
    }
}
