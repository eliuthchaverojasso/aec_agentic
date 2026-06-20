using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EMAExtractor.Services
{
    public sealed class DeterministicFallbackProvider : IAiModelProvider
    {
        public string ProviderName => "Deterministic";
        public bool IsAvailable => true;

        public Task<AiCompletionResult> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            int maxTokens = 512,
            CancellationToken cancellationToken = default)
        {
            string answer = BuildAnswer(userPrompt ?? string.Empty);
            return Task.FromResult(new AiCompletionResult
            {
                Success = true,
                Content = answer,
                ProviderName = ProviderName,
                UsedFallback = true
            });
        }

        private static string BuildAnswer(string userPrompt)
        {
            string lower = userPrompt.ToLowerInvariant();

            // Classification queries
            if (lower.Contains("classify") || lower.Contains("requirement type") || lower.Contains("what type"))
            {
                return BuildClassificationAnswer(lower);
            }

            // Evidence queries
            if (lower.Contains("evidence") || lower.Contains("what evidence") || lower.Contains("how to close"))
            {
                return "Review the matched model elements in the report for direct parameter evidence. " +
                       "Direct closing evidence must come from Revit model export data — not inferred from category or level alone.";
            }

            // Status queries
            if (lower.Contains("why") && (lower.Contains("not met") || lower.Contains("insufficient") || lower.Contains("needs review")))
            {
                return "The requirement status was assigned because the model export did not contain sufficient direct parameter evidence to confirm compliance. " +
                       "Human review of the drawings and specifications is recommended.";
            }

            // Next action queries
            if (lower.Contains("next step") || lower.Contains("action") || lower.Contains("what should"))
            {
                return "Review the missing evidence listed in the report. " +
                       "Populate the required Revit parameters on the candidate elements, or consult the project drawings and specifications for the relevant detail.";
            }

            return "This is a deterministic summary. For AI-assisted analysis, select a local or cloud model from the model selector. " +
                   "All answers are grounded in the model export data visible in this report — no external assumptions are made.";
        }

        private static string BuildClassificationAnswer(string lower)
        {
            // Plumbing
            if (lower.Contains("flush valve") || lower.Contains("sloan") || lower.Contains("flushometer"))
                return "This is a plumbing_flush_valve_product_spec requirement. It specifies a flush valve brand or model (e.g., SLOAN ROYAL) and requires manufacturer/model submittal evidence.";
            if (lower.Contains("water hammer") || lower.Contains("arrestor"))
                return "This is a plumbing_water_hammer_arrestor_requirement. It calls for water hammer arrestor pipe accessories and requires direct element or specification evidence.";
            if (lower.Contains("soap dispenser") || lower.Contains("cw line") || lower.Contains("cold water supply"))
                return "This is a plumbing_accessory_water_supply requirement. It involves a plumbing accessory with a cold or hot water supply line.";
            if (lower.Contains("clevis") || lower.Contains("p-trap") || lower.Contains("pipe hanger") || lower.Contains("pipe support"))
                return "This is a plumbing_support_hanger_requirement. It specifies pipe hangers or supports (e.g., P-trap with clevis hanger) and requires drawing/specification evidence.";
            if (lower.Contains("hose bibb") || lower.Contains("rpz") || lower.Contains("backflow"))
                return "This is a plumbing_hose_bibb_rpz_valves requirement. It references hose bibbs, RPZ/backflow preventers, or valves.";

            // Electrical
            if (lower.Contains("grounding") || lower.Contains("bonding") || lower.Contains("electrode"))
                return "This is a grounding_bonding_conductors requirement. It requires direct grounding conductor or bonding evidence.";
            if (lower.Contains("panel") || lower.Contains("circuit") || lower.Contains("breaker"))
                return "This is a panel_circuit_power requirement. It requires direct electrical connection parameters.";
            if (lower.Contains("conduit") || lower.Contains("raceway") || lower.Contains("fmc"))
                return "This is a conduit_raceway_size_requirement or conduit_raceway_presence requirement. It needs direct conduit element evidence.";

            // Specification/manual
            if (lower.Contains("manufacturer") || lower.Contains("brand") || lower.Contains("basis of design"))
                return "This is a manufacturer_brand_restriction or manufacturer_product_spec_submittal requirement. It requires manufacturer/model submittal evidence.";
            if (lower.Contains("label") || lower.Contains("nameplate") || lower.Contains("tag") || lower.Contains("identification"))
                return "This is an identification_labeling_nameplate requirement. It requires direct mark/tag/nameplate parameter evidence.";
            if (lower.Contains("commissioning") || lower.Contains("o&m") || lower.Contains("training"))
                return "This is a commissioning_testing_om_training requirement. It is a closeout deliverable and cannot be closed from model evidence.";

            return "Unable to determine requirement type from this description alone. Review the requirement text against the Universal Requirement Type Matrix for the correct classification.";
        }
    }
}
