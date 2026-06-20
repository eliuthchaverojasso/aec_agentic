using System;
using Autodesk.Revit.UI;
using EMAExtractor.Services;

namespace EMAExtractor.Commands
{
    public static class CommandSafety
    {
        public static Result Execute(ref string message, Action action)
        {
            try
            {
                action();
                return Result.Succeeded;
            }
            catch (OperationCanceledException ex)
            {
                message = ex.Message;
                LoggingService.Error("Command cancelled.", ex);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                LoggingService.Error("Command failed.", ex);
                return Result.Failed;
            }
        }
    }
}
