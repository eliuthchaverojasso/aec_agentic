using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using EMAExtractor.UI;

namespace EMAExtractor.Commands.ProjectSetup
{
    [Transaction(TransactionMode.Manual)]
    public class ProjectInfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            return EMAExtractor.Commands.CommandSafety.Execute(ref message, () =>
            {
                Document document = commandData.Application.ActiveUIDocument?.Document;
                ModelessToolWindow.ShowProjectInfo(document);
            });
        }
    }
}
