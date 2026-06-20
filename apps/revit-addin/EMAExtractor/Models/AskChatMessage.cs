using System;

namespace EMAExtractor.Models
{
    public sealed class AskChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);

        public string Label => IsUser ? "You" : "EMA AI";
    }
}
