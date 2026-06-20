using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace EMAExtractor.ExternalEvents
{
    public class IsolateElementExternalEvent : IExternalEventHandler
    {
        public int ElementIdInteger { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            if (ElementIdInteger <= 0 || uidoc == null || uidoc.Document == null)
            {
                return;
            }

            using (Transaction tx = new Transaction(uidoc.Document, "EMA AI Isolate Issue Element"))
            {
                tx.Start();
                uidoc.ActiveView.IsolateElementsTemporary(new List<ElementId> { ElementIdCompat.Create(ElementIdInteger) });
                tx.Commit();
            }
        }

        public string GetName()
        {
            return "EMA AI Isolate Element";
        }
    }
}
