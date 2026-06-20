using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.UI;

namespace EMAExtractor.Commands.ModelQaQc
{
    [Transaction(TransactionMode.Manual)]
    public class PanelScheduleAuditPreviewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                ModelessToolWindow.ShowRuleResults();
            });
        }
    }
}
