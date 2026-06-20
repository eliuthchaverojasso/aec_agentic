using System;
using System.Collections.Generic;
using System.Linq;

namespace EMAExtractor.Requirements
{
    public enum ValidationType
    {
        Model,
        Drawing,
        Specification,
        Manual,
        Hybrid
    }

    public class TaxonomyLabel
    {
        public string Label { get; set; }
        public double Score { get; set; }
    }

    public class ValidationTypeResult
    {
        public ValidationType PrimaryType { get; set; }
        public List<ValidationType> SecondaryTypes { get; set; } = new List<ValidationType>();
        public List<TaxonomyLabel> TaxonomyLabels { get; set; } = new List<TaxonomyLabel>();
        public double TypeConfidence { get; set; }
        public string Reasoning { get; set; }
    }

    public static class ValidationTypeClassifier
    {
        private static readonly string[] ModelKeywords = new[]
        {
            "provide", "install", "include", "panel", "circuit", "circuited", "supply from", "breaker",
            "outlet", "receptacle", "fixture", "valve", "equipment", "device", "rack", "camera",
            "scoreboard", "generator", "connect", "mounted", "installed", "present", "placed"
        };

        private static readonly string[] DrawingKeywords = new[]
        {
            "shown", "show on drawings", "sheet", "detail", "diagram", "riser", "schedule", "plans",
            "note", "indicated", "depicted", "displayed", "per drawing", "per plan", "per detail"
        };

        private static readonly string[] SpecificationKeywords = new[]
        {
            "specification", "manufacturer", "manufacturers", "standard", "basis of design", "bod", "warranty",
            "acceptable", "preferred", "brand", "only", "listed", "approved", "certified",
            "ul listed", "nec", "code compliance", "per spec", "product", "submittal",
            "identification", "identify", "label", "labeling", "nameplate", "tag", "marker",
            "approved equal", "nema", "brady", "carlton", "seton"
        };

        private static readonly string[] ManualKeywords = new[]
        {
            "coordinate", "review", "confirm", "verify", "per district", "per owner", "conversation",
            "email", "avoid", "call", "discuss", "communicate", "approval required", "owner approval",
            "protect", "protection", "clean", "original manufacturer", "work completion", "demolition",
            "remove", "removal", "relocate", "disconnect", "abandoned", "salvage", "dispose",
            "field verify", "field", "grounding", "conduit", "conductor", "blank cover"
        };

        private static readonly string[] TaxonomyKeywordMap = new[]
        {
            "equipment_presence:provide,install,include,equipment,device,fixture,panel,rack",
            "parameter_performance:voltage,load,apparent load,connected load,performance,capacity,rating",
            "manufacturer_standard:manufacturer,brand,standard,specification,basis of design,listed,approved",
            "routing_location:location,room,space,level,elevation,floor,wall,ceiling,route,routing",
            "controls_sequence:control,sequence,control system,automation,logic,interlocked,controlled",
            "drawing_sheet_requirement:drawing,sheet,plan,detail,diagram,riser,schedule,indicated",
            "specification_standard:specification,standard,code,nec,per spec,listed,warranty",
            "manual_review_coordination:coordinate,review,confirm,verify,per owner,per district,communication",
            "maintenance_access:maintenance,access,clearance,working space,service,spare capacity",
            "data_technology:network,data,wifi,wireless,broadband,telecom,low voltage,lv,connectivity",
            "life_safety_security:safety,security,emergency,fire,alarm,evacuation,backup power,redundancy",
            "marking_labeling:label,mark,identify,tag,nameplate,legend,signage",
            "general_owner_requirement:owner,requirement,shall,must,required,criteria"
        };

        public static ValidationTypeResult Classify(string requirementText, string worksheetName)
        {
            if (string.IsNullOrWhiteSpace(requirementText))
            {
                return new ValidationTypeResult
                {
                    PrimaryType = ValidationType.Manual,
                    TypeConfidence = 0.3,
                    Reasoning = "Requirement text is empty or blank."
                };
            }

            string normalized = requirementText.ToLowerInvariant();

            var typeScores = new Dictionary<ValidationType, double>
            {
                { ValidationType.Model, ScoreKeywords(normalized, ModelKeywords) },
                { ValidationType.Drawing, ScoreKeywords(normalized, DrawingKeywords) },
                { ValidationType.Specification, ScoreKeywords(normalized, SpecificationKeywords) },
                { ValidationType.Manual, ScoreKeywords(normalized, ManualKeywords) }
            };

            var primaryType = typeScores.OrderByDescending(x => x.Value).First().Key;
            var primaryScore = typeScores[primaryType];

            var secondaryTypes = typeScores
                .Where(x => x.Key != primaryType && x.Value >= 0.25)
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .ToList();

            double typeConfidence = CalculateTypeConfidence(primaryType, primaryScore);

            if (secondaryTypes.Count > 0)
            {
                primaryType = ValidationType.Hybrid;
            }

            if (ContainsAny(normalized, new[]
            {
                "manufacturer", "manufacturers", "specification", "submittal", "identification",
                "identify", "label", "labeling", "nameplate", "tag", "approved equal", "product",
                "brady", "carlton", "seton", "warranty"
            }) && ContainsAny(normalized, ModelKeywords))
            {
                primaryType = ValidationType.Hybrid;
            }

            if (ContainsAny(normalized, new[]
            {
                "protect", "protection", "clean", "demolition", "remove", "relocate", "disconnect",
                "abandoned", "salvage", "dispose", "blank cover", "grounding", "conduit", "conductor"
            }) && !ContainsAny(normalized, DrawingKeywords) && !ContainsAny(normalized, SpecificationKeywords))
            {
                primaryType = ValidationType.Manual;
            }

            var taxonomyLabels = ClassifyTaxonomy(normalized);

            return new ValidationTypeResult
            {
                PrimaryType = primaryType,
                SecondaryTypes = secondaryTypes,
                TaxonomyLabels = taxonomyLabels,
                TypeConfidence = typeConfidence,
                Reasoning = BuildTypeReasoning(primaryType, primaryScore, secondaryTypes.Count)
            };
        }

        private static double ScoreKeywords(string normalizedText, string[] keywords)
        {
            int matchCount = 0;
            foreach (string keyword in keywords)
            {
                if (normalizedText.Contains(keyword))
                {
                    matchCount++;
                }
            }

            return keywords.Length == 0 ? 0 : (double)matchCount / keywords.Length;
        }

        private static double CalculateTypeConfidence(ValidationType type, double typeScore)
        {
            return typeScore switch
            {
                >= 0.75 => 0.95,
                >= 0.50 => 0.80,
                >= 0.25 => 0.60,
                >= 0.10 => 0.40,
                _ => 0.25
            };
        }

        private static List<TaxonomyLabel> ClassifyTaxonomy(string normalizedText)
        {
            var labels = new List<TaxonomyLabel>();

            foreach (var mapping in TaxonomyKeywordMap)
            {
                var parts = mapping.Split(':');
                if (parts.Length != 2) continue;

                string label = parts[0];
                string[] keywords = parts[1].Split(',');

                int matchCount = keywords.Count(kw => normalizedText.Contains(kw));
                if (matchCount > 0)
                {
                    double score = (double)matchCount / keywords.Length;
                    labels.Add(new TaxonomyLabel { Label = label, Score = score });
                }
            }

            return labels.OrderByDescending(x => x.Score).ToList();
        }

        private static string BuildTypeReasoning(ValidationType type, double score, int secondaryCount)
        {
            string baseReasoning = type switch
            {
                ValidationType.Model => "This requirement depends on model elements and parameters.",
                ValidationType.Drawing => "This requirement references drawings and plans.",
                ValidationType.Specification => "This requirement depends on specifications, product data, or manufacturer criteria.",
                ValidationType.Manual => "This requirement requires human review, field verification, or coordination.",
                ValidationType.Hybrid => "This requirement combines model evidence with drawings, specifications, or manual review.",
                _ => "Validation type could not be determined."
            };

            if (score < 0.3)
            {
                baseReasoning += " Classification confidence is low.";
            }

            if (secondaryCount > 0)
            {
                baseReasoning += $" Multiple validation types detected (hybrid).";
            }

            return baseReasoning;
        }

        private static bool ContainsAny(string text, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null)
            {
                return false;
            }

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) &&
                    text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
