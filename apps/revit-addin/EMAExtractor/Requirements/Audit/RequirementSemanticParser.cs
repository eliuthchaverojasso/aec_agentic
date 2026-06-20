using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EMAExtractor.Requirements.Audit
{
    /// <summary>
    /// Deterministic, dependency-free extraction of a <see cref="RequirementSemanticIr"/>
    /// from raw requirement text. Regex + curated dictionaries only — no LLM, no network.
    /// The IR is the structural anchor used by the coherence engine to compare
    /// requirements against each other (same subject + incompatible value = conflict).
    /// </summary>
    public static class RequirementSemanticParser
    {
        private static readonly string[] ModalityOrder = { "shall", "must", "will", "should", "may" };

        // Curated AEC subject nouns. Overlap of subject tokens is the gate that
        // prevents cross-scope false conflicts (different subject -> not compared).
        private static readonly string[] SubjectNouns =
        {
            "panelboard", "panel", "switchboard", "switchgear", "busway", "transformer",
            "circuit", "breaker", "receptacle", "outlet", "rack", "fixture", "luminaire",
            "conduit", "raceway", "generator", "ups", "rtu", "ahu", "fan", "exhaust",
            "valve", "hanger", "meter", "pump", "chiller", "boiler", "vav", "damper",
            "camera", "scoreboard", "disconnect", "contactor", "relay", "sensor",
            "thermostat", "sink", "lavatory", "toilet", "urinal", "drain", "trap",
            "heater", "compressor", "ionization", "bibb", "rpz", "arrestor"
        };

        // Multi-word brands must be listed before single-word fragments so the
        // longest match wins during scanning.
        private static readonly string[] Brands =
        {
            "square d", "general electric", "cutler-hammer", "cutler hammer", "american standard",
            "trane", "lennox", "york", "aaon", "carrier", "daikin", "mitsubishi", "greenheck",
            "jci", "schneider", "eaton", "siemens", "abb", "leviton", "hubbell", "lutron",
            "cooper", "panduit", "kohler", "sloan", "zurn", "bradley", "moen", "delta", "watts",
            "brady", "carlton", "seton"
        };

        private static readonly Dictionary<string, int> WordNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 },
            { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 },
            { "eleven", 11 }, { "twelve", 12 }
        };

        public static RequirementSemanticIr Parse(string requirementText)
        {
            RequirementSemanticIr ir = new RequirementSemanticIr();

            string raw = requirementText ?? string.Empty;
            string lower = raw.ToLowerInvariant();
            string normalized = RequirementDisciplineNormalizer.NormalizeText(raw);

            ir.NormalizedText = normalized;
            ir.ContentHash = Sha256Hex(normalized);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                ir.Ambiguities.Add("Requirement text is empty.");
                ir.Modality = "none";
                ir.Quantifier = "none";
                return ir;
            }

            ir.Modality = DetectModality(lower);
            // Extract unit-bearing quantities first, then let the quantifier pass
            // append count constraints — order matters: ExtractQuantities replaces
            // the list, DetectQuantifier appends to it.
            ir.Quantities = ExtractQuantities(lower);
            DetectQuantifier(lower, ir);
            ir.SubjectTokens = ExtractSubjectTokens(normalized);
            ExtractManufacturers(lower, normalized, ir);
            ExtractConditions(lower, ir);

            if (ir.SubjectTokens.Count == 0)
            {
                ir.Ambiguities.Add("No recognized subject noun; coherence comparison limited to text similarity.");
            }

            return ir;
        }

        private static string DetectModality(string lower)
        {
            foreach (string modal in ModalityOrder)
            {
                if (Regex.IsMatch(lower, "\\b" + modal + "\\b"))
                {
                    return modal;
                }
            }
            return "none";
        }

        private static void DetectQuantifier(string lower, RequirementSemanticIr ir)
        {
            if (Regex.IsMatch(lower, "\\b(each|every|per\\s+each)\\b"))
            {
                ir.Quantifier = "each";
            }
            else if (Regex.IsMatch(lower, "\\b(all|any)\\b"))
            {
                ir.Quantifier = "all";
            }
            else
            {
                ir.Quantifier = "none";
            }

            double? min = MatchBoundedNumber(lower, "(?:at\\s+least|minimum\\s+of|no\\s+fewer\\s+than|min(?:imum)?)");
            if (min.HasValue)
            {
                ir.MinimumQuantity = min;
                ir.Quantifier = ir.Quantifier == "none" ? "at_least" : ir.Quantifier;
                ir.Quantities.Add(new SemanticQuantity { Property = "count", Operator = "min", Value = min.Value, Unit = "ea", RawText = "min " + min.Value.ToString(CultureInfo.InvariantCulture) });
            }

            double? max = MatchBoundedNumber(lower, "(?:at\\s+most|maximum\\s+of|no\\s+more\\s+than|max(?:imum)?|up\\s+to)");
            if (max.HasValue)
            {
                ir.MaximumQuantity = max;
                ir.Quantities.Add(new SemanticQuantity { Property = "count", Operator = "max", Value = max.Value, Unit = "ea", RawText = "max " + max.Value.ToString(CultureInfo.InvariantCulture) });
            }
        }

        private static double? MatchBoundedNumber(string lower, string boundPattern)
        {
            // Negative lookahead keeps "no more than 5%" or "at least 208V" from being
            // read as a *count*; those are unit-bearing values handled elsewhere.
            Match m = Regex.Match(lower, boundPattern + "\\s+(\\d+|" + string.Join("|", WordNumbers.Keys) +
                ")\\b(?!\\s*(?:%|percent|v\\b|volts?|a\\b|amp|ft\\b|in\\b|inch|mm\\b))");
            if (!m.Success)
            {
                return null;
            }

            string token = m.Groups[1].Value;
            return ParseNumberToken(token);
        }

        private static double? ParseNumberToken(string token)
        {
            if (WordNumbers.TryGetValue(token, out int word))
            {
                return word;
            }
            if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }
            return null;
        }

        private static List<SemanticQuantity> ExtractQuantities(string lower)
        {
            List<SemanticQuantity> quantities = new List<SemanticQuantity>();

            AddMatches(quantities, lower, "(\\d{2,4})\\s*v(?:olts?|ac|dc)?\\b", "voltage", "V");
            AddMatches(quantities, lower, "(\\d{1,4})\\s*amp(?:s|ere|eres)?\\b", "current", "A");
            AddMatches(quantities, lower, "(\\d{1,4})a\\b", "current", "A");
            AddMatches(quantities, lower, "(\\d{1,3}(?:\\.\\d+)?)\\s*(?:%|percent)", "percent", "%");
            AddMatches(quantities, lower, "(\\d+(?:\\.\\d+)?)\\s*(?:foot|feet|ft)\\b", "length", "ft");
            AddMatches(quantities, lower, "(\\d+(?:\\.\\d+)?)\\s*(?:millimeters?|mm)\\b", "length", "mm");
            AddFractionMatches(quantities, lower);

            // De-duplicate identical (property, unit, value) tuples deterministically.
            return quantities
                .GroupBy(q => q.Property + "|" + q.Unit + "|" + q.Value.ToString("R", CultureInfo.InvariantCulture) + "|" + q.Operator)
                .Select(g => g.First())
                .OrderBy(q => q.Property, StringComparer.Ordinal)
                .ThenBy(q => q.Value)
                .ToList();
        }

        private static void AddMatches(List<SemanticQuantity> quantities, string lower, string pattern, string property, string unit)
        {
            foreach (Match m in Regex.Matches(lower, pattern))
            {
                if (double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                {
                    quantities.Add(new SemanticQuantity
                    {
                        Property = property,
                        Operator = "=",
                        Value = value,
                        Unit = unit,
                        RawText = m.Value.Trim()
                    });
                }
            }
        }

        private static void AddFractionMatches(List<SemanticQuantity> quantities, string lower)
        {
            // e.g. "3/4-inch", "1-inch", "1/2 inch"
            foreach (Match m in Regex.Matches(lower, "(\\d+(?:\\s*/\\s*\\d+)?)\\s*-?\\s*(?:inch|inches|in)\\b"))
            {
                double? value = ParseFraction(m.Groups[1].Value);
                if (value.HasValue)
                {
                    quantities.Add(new SemanticQuantity
                    {
                        Property = "length",
                        Operator = "=",
                        Value = value.Value,
                        Unit = "in",
                        RawText = m.Value.Trim()
                    });
                }
            }
        }

        private static double? ParseFraction(string token)
        {
            token = token.Replace(" ", string.Empty);
            if (token.Contains("/"))
            {
                string[] parts = token.Split('/');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double n) &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double d) &&
                    d != 0)
                {
                    return Math.Round(n / d, 4);
                }
                return null;
            }
            return double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double value) ? value : (double?)null;
        }

        private static List<string> ExtractSubjectTokens(string normalized)
        {
            string padded = " " + normalized + " ";
            List<string> found = new List<string>();
            foreach (string noun in SubjectNouns)
            {
                if (padded.Contains(" " + noun + " ") || padded.Contains(" " + noun + "s "))
                {
                    if (!found.Contains(noun))
                    {
                        found.Add(noun);
                    }
                }
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        private static void ExtractManufacturers(string lower, string normalized, RequirementSemanticIr ir)
        {
            string spaced = " " + normalized + " ";
            foreach (string brand in Brands)
            {
                string norm = RequirementDisciplineNormalizer.NormalizeText(brand);
                if (spaced.Contains(" " + norm + " "))
                {
                    if (!ir.ManufacturerBrands.Contains(norm))
                    {
                        ir.ManufacturerBrands.Add(norm);
                    }

                    // Excluded when "no <brand>" / "not <brand>" / "no <brand> ..." appears.
                    if (Regex.IsMatch(lower, "\\bno\\s+" + Regex.Escape(brand) + "\\b") ||
                        Regex.IsMatch(lower, "\\bnot\\s+" + Regex.Escape(brand) + "\\b"))
                    {
                        if (!ir.ExcludedManufacturerBrands.Contains(norm))
                        {
                            ir.ExcludedManufacturerBrands.Add(norm);
                        }
                    }
                }
            }

            ir.ManufacturerBrands.Sort(StringComparer.Ordinal);
            ir.ExcludedManufacturerBrands.Sort(StringComparer.Ordinal);
            ir.ManufacturerExclusive = Regex.IsMatch(lower, "\\bonly\\b") && ir.ManufacturerBrands.Count > 0;
        }

        private static void ExtractConditions(string lower, RequirementSemanticIr ir)
        {
            CaptureClauses(lower, "\\b(if|when|where)\\b([^.;]*)", ir.Conditions);
            CaptureClauses(lower, "\\b(unless|except|excluding|other than)\\b([^.;]*)", ir.Exceptions);
        }

        private static void CaptureClauses(string lower, string pattern, List<string> target)
        {
            foreach (Match m in Regex.Matches(lower, pattern))
            {
                string clause = (m.Groups[1].Value + " " + m.Groups[2].Value).Trim();
                clause = Regex.Replace(clause, "\\s+", " ");
                if (clause.Length > 4 && !target.Contains(clause))
                {
                    target.Add(clause);
                    if (target.Count >= 5)
                    {
                        break;
                    }
                }
            }
        }

        public static string Sha256Hex(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                StringBuilder sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }
    }
}
