using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EMAExtractor.Services
{
    public sealed class RequirementFeedbackEntry
    {
        public string Timestamp { get; set; }
        public string SourceTextHash { get; set; }
        public string SourceRow { get; set; }
        public string SourceWorksheet { get; set; }
        public string OldType { get; set; }
        public string CorrectedType { get; set; }
        public string Reason { get; set; }
        public string ApprovedBy { get; set; }
    }

    public sealed class RequirementFeedbackService
    {
        private readonly string _feedbackPath;

        public RequirementFeedbackService(string feedbackPath = null)
        {
            _feedbackPath = feedbackPath ?? DefaultFeedbackPath();
        }

        public bool SaveCorrection(
            string sourceText,
            string sourceRow,
            string sourceWorksheet,
            string oldType,
            string correctedType,
            string reason,
            string approvedBy)
        {
            try
            {
                EnsureDirectory();

                var entry = new RequirementFeedbackEntry
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    SourceTextHash = HashText(sourceText),
                    SourceRow = sourceRow ?? string.Empty,
                    SourceWorksheet = sourceWorksheet ?? string.Empty,
                    OldType = oldType ?? string.Empty,
                    CorrectedType = correctedType ?? string.Empty,
                    Reason = reason ?? string.Empty,
                    ApprovedBy = approvedBy ?? string.Empty
                };

                string line = JsonSerializer.Serialize(entry);
                File.AppendAllText(_feedbackPath, line + Environment.NewLine, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<RequirementFeedbackEntry> LoadFeedback()
        {
            var result = new List<RequirementFeedbackEntry>();
            if (!File.Exists(_feedbackPath))
                return result;

            try
            {
                string[] lines = File.ReadAllLines(_feedbackPath, Encoding.UTF8);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        RequirementFeedbackEntry entry = JsonSerializer.Deserialize<RequirementFeedbackEntry>(line);
                        if (entry != null)
                            result.Add(entry);
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }

        public RequirementFeedbackEntry FindMatchingFeedback(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) return null;
            string hash = HashText(sourceText);

            List<RequirementFeedbackEntry> all = LoadFeedback();
            RequirementFeedbackEntry match = null;
            foreach (RequirementFeedbackEntry entry in all)
            {
                if (string.Equals(entry.SourceTextHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    // Last matching entry wins (most recent correction)
                    match = entry;
                }
            }
            return match;
        }

        private void EnsureDirectory()
        {
            string dir = Path.GetDirectoryName(_feedbackPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static string HashText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            byte[] bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text.Trim()));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 16);
        }

        private static string DefaultFeedbackPath()
        {
            string appRoot = LoggingService.AppRoot;
            if (!string.IsNullOrWhiteSpace(appRoot))
                return Path.Combine(appRoot, "data", "feedback", "requirement_mapping_feedback.jsonl");

            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "data", "feedback", "requirement_mapping_feedback.jsonl");
        }
    }
}
