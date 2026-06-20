using System;
using System.Diagnostics;
using System.IO;

namespace EMAExtractor.Services
{
    public static class LoggingService
    {
        public static string AppRoot
        {
            get
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EMA AI");
                Directory.CreateDirectory(root);
                return root;
            }
        }

        public static string LogsFolder
        {
            get
            {
                string folder = Path.Combine(AppRoot, "logs");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public static string CurrentLogPath => Path.Combine(LogsFolder, "ema-extractor.log");

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write("ERROR", exception == null ? message : message + Environment.NewLine + exception);
        }

        public static void OpenLogsFolder()
        {
            Process.Start(new ProcessStartInfo(LogsFolder) { UseShellExecute = true });
        }

        private static void Write(string level, string message)
        {
            try
            {
                File.AppendAllText(
                    CurrentLogPath,
                    $"{DateTime.Now:O} {level} {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never break a Revit workflow.
            }
        }
    }
}
