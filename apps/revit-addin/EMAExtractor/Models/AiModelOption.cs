using System;

namespace EMAExtractor.Models
{
    public sealed class AiModelOption
    {
        public string Name { get; set; }
        public string ModelId { get; set; }
        public string ProviderName { get; set; }
        public string ProviderDisplayName { get; set; }
        public string ProviderType { get; set; } // "Local" | "Cloud" | "Deterministic"
        public string BaseUrl { get; set; }
        public bool IsDefault { get; set; }
        public bool IsSmallModel { get; set; }
        public string AvailabilityMessage { get; set; }
        public string PrivacyMessage { get; set; }
        public int TimeoutMs { get; set; }
        public int ChunkTimeoutMs { get; set; }

        public string DisplayText
        {
            get
            {
                string display = string.IsNullOrWhiteSpace(Name) ? ModelId ?? string.Empty : Name;
                var badges = new System.Collections.Generic.List<string>();
                if (IsDefault) badges.Add("Default");
                if (IsSmallModel) badges.Add("Small");
                if (IsDeterministic) badges.Add("Fallback");
                string provider = string.IsNullOrWhiteSpace(ProviderDisplayName) ? string.Empty : ProviderDisplayName + " — ";
                return badges.Count == 0
                    ? provider + display
                    : provider + display + " (" + string.Join(", ", badges) + ")";
            }
        }

        public bool IsLocal => string.Equals(ProviderType, "Local", StringComparison.OrdinalIgnoreCase);
        public bool IsCloud => string.Equals(ProviderType, "Cloud", StringComparison.OrdinalIgnoreCase);
        public bool IsDeterministic => string.Equals(ProviderType, "Deterministic", StringComparison.OrdinalIgnoreCase);
    }
}
