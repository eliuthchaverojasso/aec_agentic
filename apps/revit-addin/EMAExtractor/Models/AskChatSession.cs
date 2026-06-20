using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EMAExtractor.Models
{
    public sealed class AskChatSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public string ReportPath { get; set; } = string.Empty;
        public string ReportName { get; set; } = string.Empty;
        public string Title { get; set; } = "New chat";
        public string ModelDisplayName { get; set; } = string.Empty;
        public string ContextScope { get; set; } = "Summary";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public List<AskChatMessage> Messages { get; set; } = new List<AskChatMessage>();

        public string LastQuestion
        {
            get
            {
                AskChatMessage message = Messages == null ? null : Messages.LastOrDefault(item => item != null && item.IsUser);
                return message?.Content ?? string.Empty;
            }
        }

        public string LastAnswer
        {
            get
            {
                AskChatMessage message = Messages == null ? null : Messages.LastOrDefault(item => item != null && !item.IsUser);
                return message?.Content ?? string.Empty;
            }
        }

        public string DisplayText
        {
            get
            {
                string title = string.IsNullOrWhiteSpace(Title) ? "New chat" : Title.Trim();
                string preview = string.IsNullOrWhiteSpace(LastQuestion) ? "New chat" : Truncate(LastQuestion, 54);
                string updated = UpdatedAtUtc == default
                    ? string.Empty
                    : UpdatedAtUtc.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture);

                string suffix = string.IsNullOrWhiteSpace(updated) ? preview : preview + " · " + updated;
                return title + " · " + suffix;
            }
        }

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string text = value.Trim();
            if (text.Length <= maxChars)
            {
                return text;
            }

            return text.Substring(0, Math.Max(0, maxChars - 3)).TrimEnd() + "...";
        }
    }
}
