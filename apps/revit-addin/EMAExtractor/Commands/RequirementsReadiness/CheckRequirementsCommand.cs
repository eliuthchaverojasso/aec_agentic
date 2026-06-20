using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.Services;

namespace EMAExtractor.Commands.RequirementsReadiness
{
    [Transaction(TransactionMode.Manual)]
    public class CheckRequirementsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                RequirementCheckWorkflowService.RunComplianceCheck(commandData);
            });
        }
    }
}
