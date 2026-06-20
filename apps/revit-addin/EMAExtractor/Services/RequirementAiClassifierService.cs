using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EMAExtractor.Requirements;

namespace EMAExtractor.Services
{
    public sealed class AiClassificationResult
    {
        public string RequirementType { get; set; }
        public string RequirementTypeReason { get; set; }
        public string ValidationType { get; set; }
        public double Confidence { get; set; }
        public bool UsedAi { get; set; }
        public bool UsedFallback { get; set; }
        public string ProviderName { get; set; }
        public string RawAiResponse { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class RequirementAiClassifierService
    {
        private const string SystemPrompt =
            "You are an EMA AI requirement classifier. You classify BIM/VDC owner requirements into the correct requirement type based on the Universal Requirement Type Matrix. " +
            "You MUST only choose from the known requirement types in the matrix. You MUST NOT invent evidence or claim compliance. " +
            "Return a JSON object with fields: requirementType (string), requirementTypeReason (string), validationType (string: Model|Drawing|Specification|Manual|Hybrid), confidence (0.0-1.0). " +
            "No additional fields. No markdown. Only raw JSON.";

        private readonly IAiModelProvider _provider;
        private readonly string _taxonomyContext;

        public RequirementAiClassifierService(IAiModelProvider provider, string taxonomyContext = null)
        {
            _provider = provider ?? new DeterministicFallbackProvider();
            _taxonomyContext = taxonomyContext ?? BuildDefaultTaxonomyContext();
        }

        public async Task<AiClassificationResult> ClassifyAsync(
            OwnerRequirementRow row,
            CancellationToken cancellationToken = default)
        {
            if (row == null)
            {
                return new AiClassificationResult
                {
                    RequirementType = "unknown_ambiguous",
                    RequirementTypeReason = "Row was null.",
                    ValidationType = "Hybrid",
                    Confidence = 0.0,
                    UsedFallback = true
                };
            }

            // Always run deterministic classifier first; use AI only to refine unknown_ambiguous
            RequirementSemanticProfile deterministicProfile = RequirementSemanticClassifier.Classify(
                row.RequirementText,
                row.Category,
                RequirementDisciplineNormalizer.Parse(row.Discipline, RequirementDiscipline.All));

            if (!string.Equals(deterministicProfile.RequirementType, "unknown_ambiguous", StringComparison.OrdinalIgnoreCase))
            {
                return new AiClassificationResult
                {
                    RequirementType = deterministicProfile.RequirementType,
                    RequirementTypeReason = deterministicProfile.RequirementTypeReason,
                    ValidationType = deterministicProfile.ValidationType.ToString(),
                    Confidence = 0.90,
                    UsedAi = false,
                    UsedFallback = false,
                    ProviderName = "Deterministic"
                };
            }

            if (!_provider.IsAvailable)
            {
                return FallbackResult(deterministicProfile);
            }

            string userPrompt = BuildUserPrompt(row);

            try
            {
                AiCompletionResult completion = await _provider.CompleteAsync(
                    SystemPrompt,
                    userPrompt,
                    maxTokens: 256,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!completion.Success)
                {
                    return FallbackResult(deterministicProfile, completion.ErrorMessage);
                }

                AiClassificationResult parsed = ParseAiResponse(completion.Content);
                parsed.UsedAi = true;
                parsed.UsedFallback = completion.UsedFallback;
                parsed.ProviderName = completion.ProviderName;
                parsed.RawAiResponse = completion.Content;
                return parsed;
            }
            catch (Exception ex)
            {
                return FallbackResult(deterministicProfile, ex.Message);
            }
        }

        private static string BuildUserPrompt(OwnerRequirementRow row)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Classify this owner requirement:");
            sb.AppendLine("Discipline: " + (row.Discipline ?? "Unknown"));
            sb.AppendLine("Requirement text: " + (row.RequirementText ?? string.Empty));
            if (!string.IsNullOrWhiteSpace(row.Category))
            {
                sb.AppendLine("Category: " + row.Category);
            }

            return sb.ToString();
        }

        private static AiClassificationResult ParseAiResponse(string json)
        {
            try
            {
                using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    System.Text.Json.JsonElement root = doc.RootElement;

                    string type = GetString(root, "requirementType") ?? "unknown_ambiguous";
                    string reason = GetString(root, "requirementTypeReason") ?? "AI classification.";
                    string vt = GetString(root, "validationType") ?? "Hybrid";
                    double conf = 0.70;
                    if (root.TryGetProperty("confidence", out System.Text.Json.JsonElement confEl) &&
                        confEl.TryGetDouble(out double c))
                    {
                        conf = Math.Min(1.0, Math.Max(0.0, c));
                    }

                    return new AiClassificationResult
                    {
                        RequirementType = type,
                        RequirementTypeReason = reason,
                        ValidationType = vt,
                        Confidence = conf
                    };
                }
            }
            catch
            {
                return new AiClassificationResult
                {
                    RequirementType = "unknown_ambiguous",
                    RequirementTypeReason = "AI response could not be parsed.",
                    ValidationType = "Hybrid",
                    Confidence = 0.30
                };
            }
        }

        private static AiClassificationResult FallbackResult(RequirementSemanticProfile profile, string error = null)
        {
            return new AiClassificationResult
            {
                RequirementType = profile.RequirementType,
                RequirementTypeReason = profile.RequirementTypeReason,
                ValidationType = profile.ValidationType.ToString(),
                Confidence = 0.40,
                UsedAi = false,
                UsedFallback = true,
                ProviderName = "Deterministic",
                ErrorMessage = error
            };
        }

        private static string GetString(System.Text.Json.JsonElement el, string key)
        {
            if (el.TryGetProperty(key, out System.Text.Json.JsonElement val) &&
                val.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return val.GetString();
            }

            return null;
        }

        private static string BuildDefaultTaxonomyContext()
        {
            return string.Join(", ", new[]
            {
                "grounding_bonding_conductors", "conduit_raceway_size_requirement",
                "flexible_conduit_length_requirement", "conduit_raceway_presence",
                "plumbing_hose_bibb_rpz_valves", "plumbing_flush_valve_product_spec",
                "plumbing_water_hammer_arrestor_requirement", "plumbing_accessory_water_supply",
                "plumbing_support_hanger_requirement", "manufacturer_brand_restriction",
                "owner_standard_product_constraint", "identification_labeling_nameplate",
                "drawing_spec_manual_owner_approval", "field_execution_demolition_protection",
                "mechanical_controls_ddc_emcs", "dimension_clearance_distance_separation",
                "installation_method_constraint", "code_jurisdiction_requirement",
                "mechanical_performance_feature", "lighting_control_scheme",
                "operation_maintenance_manual", "attic_stock_spare_parts",
                "panel_circuit_power", "outlets_receptacles_devices",
                "technology_low_voltage_security_fire_alarm", "mechanical_equipment_coverage",
                "conduit_raceway", "controls_bms_bas_contactors_relays",
                "commissioning_testing_om_training", "manufacturer_product_spec_submittal",
                "level_location_mounting_placement", "unknown_ambiguous"
            });
        }
    }
}
