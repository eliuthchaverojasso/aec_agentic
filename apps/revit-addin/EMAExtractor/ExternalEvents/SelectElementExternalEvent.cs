using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace EMAExtractor.ExternalEvents
{
    public class SelectElementExternalEvent : IExternalEventHandler
    {
        public int ElementIdInteger { get; set; }

        public void Execute(UIApplication app)
        {
            if (ElementIdInteger <= 0 || app.ActiveUIDocument == null)
            {
                return;
            }

            ElementId id = ElementIdCompat.Create(ElementIdInteger);
            app.ActiveUIDocument.Selection.SetElementIds(new List<ElementId> { id });
        }

        public string GetName()
        {
            return "EMA AI Select Element";
        }
    }
}
