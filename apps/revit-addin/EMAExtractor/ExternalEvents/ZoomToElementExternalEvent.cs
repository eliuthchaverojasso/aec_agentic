using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace EMAExtractor.ExternalEvents
{
    public class ZoomToElementExternalEvent : IExternalEventHandler
    {
        public int ElementIdInteger { get; set; }

        public void Execute(UIApplication app)
        {
            if (ElementIdInteger <= 0 || app.ActiveUIDocument == null)
            {
                return;
            }

            app.ActiveUIDocument.ShowElements(ElementIdCompat.Create(ElementIdInteger));
        }

        public string GetName()
        {
            return "EMA AI Zoom To Element";
        }
    }
}
