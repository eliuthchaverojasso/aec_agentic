using EMAExtractor.Services;
using Autodesk.Revit.UI;

namespace EMAExtractor.ExternalEvents
{
    public class MarkReviewedExternalEvent : IExternalEventHandler
    {
        public int IssueId { get; set; }

        public void Execute(UIApplication app)
        {
            LoggingService.Info($"Mark reviewed requested for issue_id={IssueId}. Backend PATCH integration is reserved for the next review workflow sprint.");
        }

        public string GetName()
        {
            return "EMA AI Mark Reviewed";
        }
    }
}
