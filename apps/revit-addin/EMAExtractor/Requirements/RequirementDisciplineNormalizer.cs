using System;
using System.Text.RegularExpressions;

namespace EMAExtractor.Requirements
{
    public static class RequirementDisciplineNormalizer
    {
        public static RequirementDiscipline Parse(string value, RequirementDiscipline fallback = RequirementDiscipline.All)
        {
            string normalized = NormalizeText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return fallback;
            }

            if (normalized == "all" || normalized.Contains("all disciplines"))
            {
                return RequirementDiscipline.All;
            }

            if (normalized.Contains("electrical") || normalized == "elec" || normalized.Contains("elec "))
            {
                return RequirementDiscipline.Electrical;
            }

            if (normalized.Contains("lighting"))
            {
                return RequirementDiscipline.Lighting;
            }

            if (normalized.Contains("mechanical") || normalized == "mech" || normalized.Contains("hvac"))
            {
                return RequirementDiscipline.Mechanical;
            }

            if (normalized.Contains("plumbing") || normalized == "plumb")
            {
                return RequirementDiscipline.Plumbing;
            }

            if (normalized.Contains("technology") ||
                normalized.Contains("low voltage") ||
                normalized == "lv" ||
                normalized.Contains("telecom") ||
                normalized.Contains("data") ||
                normalized.Contains("communication"))
            {
                return RequirementDiscipline.Technology;
            }

            return fallback;
        }

        public static bool Matches(RequirementDiscipline selectedDiscipline, string requirementDiscipline)
        {
            if (selectedDiscipline == RequirementDiscipline.All)
            {
                return true;
            }

            string normalized = NormalizeText(requirementDiscipline);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }

            if (normalized == "all" || normalized.Contains("all disciplines"))
            {
                return true;
            }

            switch (selectedDiscipline)
            {
                case RequirementDiscipline.Electrical:
                    return normalized.Contains("electrical") || normalized == "elec" || normalized.Contains("elec ");
                case RequirementDiscipline.Lighting:
                    return normalized.Contains("lighting");
                case RequirementDiscipline.Mechanical:
                    return normalized.Contains("mechanical") || normalized == "mech" || normalized.Contains("hvac");
                case RequirementDiscipline.Plumbing:
                    return normalized.Contains("plumbing") || normalized == "plumb";
                case RequirementDiscipline.Technology:
                    return normalized.Contains("technology") ||
                           normalized.Contains("low voltage") ||
                           normalized == "lv" ||
                           normalized.Contains("telecom") ||
                           normalized.Contains("data") ||
                           normalized.Contains("communication");
                default:
                    return true;
            }
        }

        public static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().ToLowerInvariant();
            normalized = normalized.Replace("&", " and ");
            normalized = Regex.Replace(normalized, @"[^a-z0-9]+", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }
    }
}
