using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EMAExtractor.Requirements
{
    public class RequirementSemanticProfile
    {
        public string RequirementType { get; set; }
        public string RequirementTypeReason { get; set; }
        public ValidationType ValidationType { get; set; }
        public string ValidationTypeReason { get; set; }
        public string RuleApplied { get; set; }
        public string RuleFamily { get; set; }
        public List<string> TriggerKeywords { get; set; } = new List<string>();
        public List<string> ExpectedEvidenceSources { get; set; } = new List<string>();
        public List<string> AllowedCategories { get; set; } = new List<string>();
        public List<string> ExpectedCategories
        {
            get { return AllowedCategories; }
            set { AllowedCategories = value ?? new List<string>(); }
        }
        public List<string> ExcludedCategories { get; set; } = new List<string>();
        public List<string> ExpectedFamilyTypeHints { get; set; } = new List<string>();
        public List<string> ExpectedParameters { get; set; } = new List<string>();
        public List<string> DirectClosingEvidence { get; set; } = new List<string>();
        public List<string> SupportingContext { get; set; } = new List<string>();
        public List<string> MissingDirectEvidence { get; set; } = new List<string>();
        public string CandidateScopeReason { get; set; }
        public bool FallbackAllowed { get; set; }
        public bool FullModelFallbackAllowed { get; set; }
        public bool RequiresDirectParameterEvidence { get; set; }
        public bool AllowsModelOnlyMet { get; set; }
        public string ModelEvidenceSufficiency { get; set; }
        public string WhyNotModelCloseable { get; set; }
    }

    public static class RequirementSemanticClassifier
    {
        private sealed class Rule
        {
            public string RequirementType { get; set; }
            public string RequirementTypeReason { get; set; }
            public ValidationType ValidationType { get; set; }
            public string ValidationTypeReason { get; set; }
            public string RuleApplied { get; set; }
            public string RuleFamily { get; set; }
            public List<string> TriggerKeywords { get; set; } = new List<string>();
            public List<string> ExpectedEvidenceSources { get; set; } = new List<string>();
            public List<string> AllowedCategories { get; set; } = new List<string>();
            public List<string> ExcludedCategories { get; set; } = new List<string>();
            public List<string> ExpectedFamilyTypeHints { get; set; } = new List<string>();
            public List<string> ExpectedParameters { get; set; } = new List<string>();
            public List<string> DirectClosingEvidence { get; set; } = new List<string>();
            public List<string> SupportingContext { get; set; } = new List<string>();
            public List<string> MissingDirectEvidence { get; set; } = new List<string>();
            public string CandidateScopeReason { get; set; }
            public bool FallbackAllowed { get; set; }
            public bool FullModelFallbackAllowed { get; set; }
            public bool RequiresDirectParameterEvidence { get; set; }
            public bool AllowsModelOnlyMet { get; set; }
            public string ModelEvidenceSufficiency { get; set; }
            public string WhyNotModelCloseable { get; set; }
            public string[] Patterns { get; set; } = Array.Empty<string>();
            public int Priority { get; set; }
        }

        private static readonly Rule[] Rules = new[]
        {
            new Rule
            {
                RequirementType = "grounding_bonding_conductors",
                RequirementTypeReason = "Requirement explicitly references grounding, bonding, conductors, or ground bars.",
                ValidationType = ValidationType.Hybrid,
                ValidationTypeReason = "Grounding and bonding requirements are usually hybrid: some model parameters may exist, but final closure often depends on drawings, specs, or field verification.",
                RuleApplied = "grounding_bonding_conductors",
                RuleFamily = "grounding_bonding",
                TriggerKeywords = new List<string> { "grounding", "bonding", "ground bar", "ground conductor", "#6 ground conductor", "grounding electrode conductor", "ground wire", "ground bus", "grounding system" },
                ExpectedEvidenceSources = new List<string> { "Conductor/wire grounding parameters", "Drawing/specification", "Field verification" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices" },
                ExcludedCategories = new List<string> { "Mechanical Equipment" },
                ExpectedFamilyTypeHints = new List<string> { "ground bar", "ground conductor", "bonding jumper", "ground wire", "equipment grounding" },
                ExpectedParameters = new List<string> { "DMET_Feeder_GroundWireSize", "DMEN_Feeder_GroundWireArea", "DMET_Instance_GroundWireSize", "DMEN_Instance_GroundWireArea", "DMET_Feeder_WireCallout", "Grounding", "Bonding", "Conductor", "Ground Bar" },
                CandidateScopeReason = "Scope to electrical/low-voltage categories with grounding-related family/type or parameter hints. Do not use technology or mechanical presence alone.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence may be sufficient only when direct grounding/conductor parameters are present on relevant elements.",
                WhyNotModelCloseable = "Grounding/bonding is not safely closable from generic category or level evidence alone.",
                Patterns = new[] { @"\bgrounding\b", @"\bbond(ing)?\b", @"ground bar", @"ground conductor", @"grounding electrode conductor", @"ground wire", @"ground bus", @"bonding jumper", @"#\s*6\s+ground" },
                Priority = 100
            },
            new Rule
            {
                RequirementType = "conduit_raceway_size_requirement",
                RequirementTypeReason = "Requirement states a minimum, maximum, or specific conduit size that cannot be closed from generic equipment presence.",
                ValidationType = ValidationType.Drawing,
                ValidationTypeReason = "Conduit size constraints are drawing/spec driven and require direct conduit evidence or detail notes.",
                RuleApplied = "conduit_raceway_size_requirement",
                RuleFamily = "conduit",
                TriggerKeywords = new List<string> { "minimum", "maximum", "size", "trade size", "conduit size", "fmc", "flexible metallic conduit", "six foot max", "6-foot max" },
                ExpectedEvidenceSources = new List<string> { "Conduit detail notes", "Drawings", "Specifications", "Field verification" },
                AllowedCategories = new List<string> { "Conduits", "Conduit Fittings", "Cable Trays" },
                ExcludedCategories = new List<string> { "Lighting Fixtures", "Electrical Fixtures", "Communication Devices", "Data Devices", "Mechanical Equipment", "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "conduit", "raceway", "flexible metallic conduit", "fmc", "sleeve" },
                ExpectedParameters = new List<string> { "Conduit Size", "Trade Size", "Flexible Conduit Length", "Length", "Comments" },
                DirectClosingEvidence = new List<string> { "Conduit size", "Trade size parameter", "Detail note" },
                SupportingContext = new List<string> { "Conduit family/type", "Technology wording", "Level" },
                MissingDirectEvidence = new List<string> { "Conduit size", "Flexible conduit length", "Detail note" },
                CandidateScopeReason = "Scope to conduit/raceway candidates or explicit conduit size language. Lighting fixture or technology presence is context only.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is not sufficient unless direct conduit size or detail evidence is present.",
                WhyNotModelCloseable = "Minimum or maximum conduit size cannot be closed from lighting fixtures, technology devices, or level evidence alone.",
                Patterns = new[] { @"\bminimum\b", @"\bmaximum\b", @"\bmin\b", @"\bmax\b", @"conduit size", @"trade size", @"flexible metallic conduit", @"\bfmc\b", @"6-?foot max", @"six-?foot max", @"1-inch", @"¾-inch", @"1/2-inch" },
                Priority = 101
            },
            new Rule
            {
                RequirementType = "flexible_conduit_length_requirement",
                RequirementTypeReason = "Requirement states a flexible conduit length limit or tap connection length constraint.",
                ValidationType = ValidationType.Drawing,
                ValidationTypeReason = "Flexible conduit length constraints are drawing/spec driven and require direct evidence rather than category presence.",
                RuleApplied = "flexible_conduit_length_requirement",
                RuleFamily = "conduit",
                TriggerKeywords = new List<string> { "flexible conduit", "fmc", "length", "tap connection", "six foot max", "6-foot max" },
                ExpectedEvidenceSources = new List<string> { "Conduit detail notes", "Drawings", "Specifications", "Field verification" },
                AllowedCategories = new List<string> { "Conduits", "Conduit Fittings", "Cable Trays" },
                ExcludedCategories = new List<string> { "Lighting Fixtures", "Electrical Fixtures", "Communication Devices", "Data Devices", "Mechanical Equipment", "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "flexible metallic conduit", "fmc", "conduit", "raceway" },
                ExpectedParameters = new List<string> { "Flexible Conduit Length", "Length", "Comments" },
                DirectClosingEvidence = new List<string> { "Flexible conduit length", "Detail note", "Specification limit" },
                SupportingContext = new List<string> { "Conduit family/type", "Level" },
                MissingDirectEvidence = new List<string> { "Flexible conduit length", "Tap connection detail" },
                CandidateScopeReason = "Scope to explicit flexible conduit language. Device presence and level are context only.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is not sufficient unless direct flexible conduit length evidence is present.",
                WhyNotModelCloseable = "Flexible conduit length cannot be closed from lighting fixtures or technology devices.",
                Patterns = new[] { @"flexible metallic conduit", @"\bfmc\b", @"tap connection", @"tap connections", @"length limit", @"6-?foot max", @"six-?foot max", @"\b6\b" },
                Priority = 100
            },
            new Rule
            {
                RequirementType = "conduit_raceway_presence",
                RequirementTypeReason = "Requirement is about conduit or raceway presence, routing, or installation path.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Conduit/raceway presence is only model-checkable when direct conduit elements exist.",
                RuleApplied = "conduit_raceway_presence",
                RuleFamily = "conduit",
                TriggerKeywords = new List<string> { "conduit", "raceway", "sleeve", "ductbank", "pathway", "routing" },
                ExpectedEvidenceSources = new List<string> { "Conduit/raceway model elements", "Drawings", "Specifications", "Field verification" },
                AllowedCategories = new List<string> { "Conduits", "Conduit Fittings", "Cable Trays" },
                ExcludedCategories = new List<string> { "Lighting Fixtures", "Electrical Fixtures", "Communication Devices", "Data Devices", "Mechanical Equipment", "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "conduit", "raceway", "sleeve", "ductbank" },
                ExpectedParameters = new List<string> { "Conduit", "Raceway", "Size", "Comments" },
                DirectClosingEvidence = new List<string> { "Conduit/raceway family/type", "Route", "Size", "Sleeve", "Ductbank" },
                SupportingContext = new List<string> { "Electrical or technology context", "Level" },
                MissingDirectEvidence = new List<string> { "Conduit/raceway family/type", "Route", "Size" },
                CandidateScopeReason = "Scope to conduit and raceway elements only. Generic equipment presence is supporting context only.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is only sufficient when direct conduit/raceway elements are present.",
                WhyNotModelCloseable = "Conduit/raceway presence cannot be closed from lighting fixture or equipment presence alone.",
                Patterns = new[] { @"\bconduit\b", @"\braceway\b", @"\bsleeve\b", @"ductbank", @"\brouting\b", @"\bpathway\b" },
                Priority = 99
            },
            new Rule
            {
                RequirementType = "manufacturer_brand_restriction",
                RequirementTypeReason = "Requirement restricts the manufacturer, brand, or allowed products.",
                ValidationType = ValidationType.Specification,
                ValidationTypeReason = "Brand restrictions are specification-driven and cannot be closed from equipment presence alone.",
                RuleApplied = "manufacturer_brand_restriction",
                RuleFamily = "specification",
                TriggerKeywords = new List<string> { "manufacturer", "brand", "approved", "equal", "acceptable", "substitute", "as manufactured by", "no york", "no jci", "no substitution", "basis of design" },
                ExpectedEvidenceSources = new List<string> { "Specification text", "Submittals", "Manufacturer metadata", "Approved manufacturer list" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures", "Mechanical Equipment", "Plumbing Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices" },
                ExcludedCategories = new List<string> { "Pipes", "Pipe Fittings" },
                ExpectedFamilyTypeHints = new List<string> { "manufacturer", "model", "catalog", "product", "approved", "specification" },
                ExpectedParameters = new List<string> { "Manufacturer", "Model", "Catalog Number", "Description", "Comments" },
                DirectClosingEvidence = new List<string> { "Manufacturer parameter", "Model number", "Submittal", "Approved manufacturer list" },
                SupportingContext = new List<string> { "Family/type name hints", "Equipment category", "Spec section" },
                MissingDirectEvidence = new List<string> { "Manufacturer", "Model", "Submittal" },
                CandidateScopeReason = "Scope to product metadata and explicit manufacturer language. Generic equipment presence is only context.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence can only close this if manufacturer or model metadata is populated.",
                WhyNotModelCloseable = "Brand restriction cannot be proven from category or level alone.",
                Patterns = new[] { @"manufacturer", @"brand", @"approved equal", @"acceptable manufacturer", @"as manufactured by", @"no york", @"no jci", @"no substitution", @"basis of design", @"substitute", @"substitution" },
                Priority = 98
            },
            new Rule
            {
                RequirementType = "owner_standard_product_constraint",
                RequirementTypeReason = "Requirement references owner standard, district standard, or approved product constraints.",
                ValidationType = ValidationType.Specification,
                ValidationTypeReason = "Owner-standard product constraints are specification-driven and need direct product or submittal evidence.",
                RuleApplied = "owner_standard_product_constraint",
                RuleFamily = "specification",
                TriggerKeywords = new List<string> { "owner standard", "district standard", "basis of design", "approved equal", "owner approved", "standard product" },
                ExpectedEvidenceSources = new List<string> { "Specification text", "Owner standard list", "Submittals", "Manufacturer metadata" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures", "Mechanical Equipment", "Plumbing Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices" },
                ExcludedCategories = new List<string> { "Pipes", "Pipe Fittings" },
                ExpectedFamilyTypeHints = new List<string> { "standard", "approved", "product", "basis of design", "owner" },
                ExpectedParameters = new List<string> { "Manufacturer", "Model", "Description", "Comments" },
                DirectClosingEvidence = new List<string> { "Owner standard", "District standard", "Basis of design", "Approved equal list" },
                SupportingContext = new List<string> { "Brand names", "Product family", "Spec text" },
                MissingDirectEvidence = new List<string> { "Owner standard", "District standard", "Product submittal" },
                CandidateScopeReason = "Scope to owner standard and product constraint language. Generic equipment presence is supporting context only.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is not sufficient unless direct owner-standard product metadata is present.",
                WhyNotModelCloseable = "Owner standard constraints cannot be closed from category or level alone.",
                Patterns = new[] { @"owner standard", @"district standard", @"basis of design", @"approved equal", @"standard product", @"owner approved" },
                Priority = 97
            },
            new Rule
            {
                RequirementType = "plumbing_hose_bibb_rpz_valves",
                RequirementTypeReason = "Requirement references hose bibbs, RPZ/backflow, valves, or exterior plumbing coordination.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Plumbing hose bibb/RPZ/valve requirements are model-checkable only when plumbing-relevant candidates and direct evidence exist; otherwise they require review.",
                RuleApplied = "plumbing_hose_bibb_rpz_valves",
                RuleFamily = "plumbing",
                TriggerKeywords = new List<string> { "hose bibb", "hose bib", "roof hose bibb", "rpz", "backflow", "backflow preventer", "shut off valve", "valve box", "riser room", "threaded connection", "roof zone" },
                ExpectedEvidenceSources = new List<string> { "Plumbing fixture/fitting/accessory elements", "Drawing/specification", "Coordination notes" },
                AllowedCategories = new List<string> { "Plumbing Fixtures", "Pipe Accessories", "Pipe Fittings", "Pipes" },
                ExcludedCategories = new List<string> { "Electrical Fixtures", "Lighting Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices" },
                ExpectedFamilyTypeHints = new List<string> { "Roof Mount Hose Bibb", "Wall Hose Bibb", "Backflow Preventer", "RPZ", "Ball Valve", "Pressure Regulating Valve", "hose bibb" },
                ExpectedParameters = new List<string> { "RPZ", "Valve", "Backflow", "System Type", "Level", "Elevation", "Location" },
                CandidateScopeReason = "Scope to plumbing categories only. Mechanical equipment can be included only when the family/type explicitly matches RPZ/backflow/valve equipment.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is sufficient only when relevant plumbing candidates and direct hose bibb/RPZ/valve evidence are present.",
                WhyNotModelCloseable = "Do not fall back to all model records or level-only placement evidence.",
                Patterns = new[] { "hose bibb", "hose bib", @"\brpz\b", "backflow", "backflow preventer", "shut off valve", "shutoff valve", "valve box", "roof zone" },
                Priority = 99
            },
            new Rule
            {
                RequirementType = "plumbing_flush_valve_product_spec",
                RequirementTypeReason = "Requirement specifies a flush valve brand, model, or product (e.g., SLOAN ROYAL flushometer) — a plumbing product specification.",
                ValidationType = ValidationType.Specification,
                ValidationTypeReason = "Flush valve product specifications require manufacturer/model submittal evidence, not generic model presence.",
                RuleApplied = "plumbing_flush_valve_product_spec",
                RuleFamily = "plumbing",
                TriggerKeywords = new List<string> { "flush valve", "flushometer", "sloan", "royal", "flush", "diaphragm", "piston" },
                ExpectedEvidenceSources = new List<string> { "Manufacturer submittal", "Product data", "Plumbing fixture schedule", "Specification section" },
                AllowedCategories = new List<string> { "Plumbing Fixtures", "Pipe Accessories" },
                ExcludedCategories = new List<string> { "Electrical Fixtures", "Lighting Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices", "Electrical Equipment", "Conduits", "Cable Trays" },
                ExpectedFamilyTypeHints = new List<string> { "flush valve", "flushometer", "sloan", "royal", "water closet flush", "urinal flush" },
                ExpectedParameters = new List<string> { "Manufacturer", "Model", "Catalog Number", "Flush Volume", "Description", "Comments" },
                DirectClosingEvidence = new List<string> { "Manufacturer parameter", "Model/catalog", "Submittal record", "Flush volume specification" },
                SupportingContext = new List<string> { "Plumbing fixture presence", "Water closet/urinal context", "Level" },
                MissingDirectEvidence = new List<string> { "Manufacturer", "Model/catalog number", "Submittal" },
                CandidateScopeReason = "Scope to plumbing fixture and pipe accessory elements with flush valve or manufacturer language. Do not scope to electrical or communication elements.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence can only close this when manufacturer/model metadata on a plumbing fixture is populated.",
                WhyNotModelCloseable = "Flush valve brand/model specification cannot be verified from fixture category or level alone.",
                Patterns = new[] { @"flush valve", @"flushometer", @"\bsloan\b", @"\broyal\b", @"flush.*valve", @"valve.*flush", @"diaphragm.*valve", @"piston.*flush" },
                Priority = 96
            },
            new Rule
            {
                RequirementType = "plumbing_water_hammer_arrestor_requirement",
                RequirementTypeReason = "Requirement specifies water hammer arrestors or surge protection for plumbing systems.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Water hammer arrestors are model-checkable as pipe accessories when present in the export; otherwise requires field/spec review.",
                RuleApplied = "plumbing_water_hammer_arrestor_requirement",
                RuleFamily = "plumbing",
                TriggerKeywords = new List<string> { "water hammer", "arrestor", "water hammer arrestor", "surge", "shock" },
                ExpectedEvidenceSources = new List<string> { "Pipe accessory model elements", "Plumbing specification", "Drawings" },
                AllowedCategories = new List<string> { "Pipe Accessories", "Pipes", "Pipe Fittings", "Plumbing Fixtures" },
                ExcludedCategories = new List<string> { "Electrical Fixtures", "Lighting Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices", "Electrical Equipment", "Mechanical Equipment", "Conduits", "Cable Trays" },
                ExpectedFamilyTypeHints = new List<string> { "water hammer arrestor", "arrestor", "surge suppressor", "shock absorber" },
                ExpectedParameters = new List<string> { "System Type", "Level", "Size", "Location", "Comments" },
                DirectClosingEvidence = new List<string> { "Water hammer arrestor family/type", "Pipe accessory element", "Spec section reference" },
                SupportingContext = new List<string> { "Plumbing system context", "Level", "Quick-closing valves nearby" },
                MissingDirectEvidence = new List<string> { "Water hammer arrestor element", "Pipe accessory element", "Specification section" },
                CandidateScopeReason = "Scope to pipe accessory and plumbing elements only. Do not scope to electrical, lighting, or mechanical categories.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is sufficient only when a water hammer arrestor pipe accessory element is present in the export.",
                WhyNotModelCloseable = "Water hammer arrestor requirement cannot be closed from level or generic fixture presence; needs specific element.",
                Patterns = new[] { @"water hammer", @"arrestor", @"water.?hammer.?arrestor", @"\bsurge\b.*\bplumb", @"shock.*arrest" },
                Priority = 95
            },
            new Rule
            {
                RequirementType = "plumbing_accessory_water_supply",
                RequirementTypeReason = "Requirement involves a plumbing accessory (soap dispenser, eye wash, drinking fountain) with a cold or hot water supply line specification.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Plumbing accessory water supply requirements are model-checkable when pipe and fixture elements with system type parameters exist.",
                RuleApplied = "plumbing_accessory_water_supply",
                RuleFamily = "plumbing",
                TriggerKeywords = new List<string> { "soap dispenser", "cw line", "hw line", "cold water", "hot water", "water supply", "eye wash", "drinking fountain", "lavatory", "service sink" },
                ExpectedEvidenceSources = new List<string> { "Plumbing fixture elements", "Pipe elements with system type", "Drawings", "Specifications" },
                AllowedCategories = new List<string> { "Plumbing Fixtures", "Pipe Accessories", "Pipes", "Pipe Fittings" },
                ExcludedCategories = new List<string> { "Electrical Fixtures", "Lighting Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices", "Electrical Equipment", "Conduits", "Cable Trays" },
                ExpectedFamilyTypeHints = new List<string> { "soap dispenser", "eye wash", "drinking fountain", "lavatory", "service sink", "cold water", "hot water", "CW", "HW" },
                ExpectedParameters = new List<string> { "System Type", "Size", "Level", "Location", "Manufacturer", "Model", "Comments" },
                DirectClosingEvidence = new List<string> { "Plumbing fixture element", "Pipe system type (CW/HW)", "Size parameter", "Manufacturer/model" },
                SupportingContext = new List<string> { "Level", "Room/space", "Pipe connection" },
                MissingDirectEvidence = new List<string> { "Plumbing fixture element", "Pipe system type", "Supply line size" },
                CandidateScopeReason = "Scope to plumbing fixture and pipe elements. Cold/hot water supply language must not match electrical or communication categories.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is sufficient when a plumbing fixture element with appropriate system type and supply parameters is present.",
                WhyNotModelCloseable = "CW/HW line and supply connection requirements cannot be closed from level or room alone without fixture and pipe evidence.",
                Patterns = new[] { @"soap dispenser", @"\bcw\b.*line", @"\bcw\b.*pipe", @"cold water", @"hot water", @"\bhw\b.*line", @"water supply", @"eye.?wash", @"drinking fountain", @"lavatory.*supply", @"service sink" },
                Priority = 94
            },
            new Rule
            {
                RequirementType = "plumbing_support_hanger_requirement",
                RequirementTypeReason = "Requirement specifies pipe support, hanger, or trap hanger details for plumbing systems (e.g., P-trap with clevis hanger).",
                ValidationType = ValidationType.Drawing,
                ValidationTypeReason = "Pipe support and hanger requirements depend on drawings/specifications for hanger sizing, spacing, and installation details; model presence alone is insufficient.",
                RuleApplied = "plumbing_support_hanger_requirement",
                RuleFamily = "plumbing",
                TriggerKeywords = new List<string> { "p-trap", "ptrap", "clevis hanger", "clevis", "pipe hanger", "pipe support", "hanger rod", "seismic hanger", "trapeze", "trapeze hanger" },
                ExpectedEvidenceSources = new List<string> { "Pipe accessory elements", "Structural/hanger drawings", "Plumbing specifications", "Hanger schedule" },
                AllowedCategories = new List<string> { "Pipe Accessories", "Pipes", "Pipe Fittings", "Plumbing Fixtures" },
                ExcludedCategories = new List<string> { "Electrical Fixtures", "Lighting Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices", "Electrical Equipment", "Mechanical Equipment", "Conduits", "Conduit Fittings", "Cable Trays" },
                ExpectedFamilyTypeHints = new List<string> { "clevis hanger", "pipe hanger", "p-trap", "trap hanger", "hanger rod", "trapeze", "seismic brace" },
                ExpectedParameters = new List<string> { "Hanger Type", "Size", "Spacing", "Level", "System Type", "Comments" },
                DirectClosingEvidence = new List<string> { "Hanger/support family or type", "Clevis hanger element", "Pipe accessory with hanger annotation", "Specification section" },
                SupportingContext = new List<string> { "Pipe level/location", "Adjacent structural elements" },
                MissingDirectEvidence = new List<string> { "Hanger element or type", "Hanger specification", "Hanger spacing detail" },
                CandidateScopeReason = "Scope strictly to pipe/plumbing support elements. Explicitly exclude all electrical, lighting, communication, and mechanical categories — hanger requirements must never scope to unrelated element pools.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is only sufficient when a specific hanger/support pipe accessory element is present in the export.",
                WhyNotModelCloseable = "P-trap and clevis hanger requirements cannot be closed from level assignment, generic equipment presence, or non-plumbing elements.",
                Patterns = new[] { @"p-?trap", @"clevis hanger", @"clevis", @"pipe hanger", @"pipe support", @"hanger rod", @"seismic hanger", @"trapeze hanger", @"\btrapeze\b" },
                Priority = 93
            },
            new Rule
            {
                RequirementType = "manufacturer_product_spec_submittal",
                RequirementTypeReason = "Requirement is about manufacturer acceptance, product data, model/catalog requirements, or submittals.",
                ValidationType = ValidationType.Specification,
                ValidationTypeReason = "Manufacturer/product requirements are specification-driven and are not safely closed from category presence alone.",
                RuleApplied = "manufacturer_product_spec_submittal",
                RuleFamily = "specification",
                TriggerKeywords = new List<string> { "manufacturer", "manufacturers", "product data", "submittal", "catalog", "model", "spec section", "acceptable manufacturers", "approved equal", "furnished by" },
                ExpectedEvidenceSources = new List<string> { "Specification text", "Submittals", "Product data", "Manufacturer metadata" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures", "Mechanical Equipment", "Plumbing Fixtures" },
                ExcludedCategories = new List<string> { "Pipes", "Pipe Fittings" },
                ExpectedFamilyTypeHints = new List<string> { "manufacturer", "model", "catalog", "product", "approved", "specification" },
                ExpectedParameters = new List<string> { "Manufacturer", "Model", "Description", "Catalog Number", "Product Data", "Comments" },
                CandidateScopeReason = "Scope to categories likely to carry product metadata. Do not close from equipment presence alone.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence can only close this when direct manufacturer/model/catalog metadata is present.",
                WhyNotModelCloseable = "If the export lacks manufacturer/model/product metadata, the requirement must remain reviewable.",
                Patterns = new[] { "manufacturer", "manufacturers", "product data", "submittal", "catalog", "basis of design", "approved equal", "specification", "furnished by" },
                Priority = 93
            },
            new Rule
            {
                RequirementType = "identification_labeling_nameplate",
                RequirementTypeReason = "Requirement asks for identification, labels, tags, nameplates, or marking.",
                ValidationType = ValidationType.Specification,
                ValidationTypeReason = "Identification and labeling requirements usually need direct mark/tag/nameplate evidence or specification review.",
                RuleApplied = "identification_labeling_nameplate",
                RuleFamily = "specification_and_marking",
                TriggerKeywords = new List<string> { "identification", "identify", "label", "labeling", "tag", "nameplate", "marker", "marking", "equipment id" },
                ExpectedEvidenceSources = new List<string> { "Marked model parameters", "Specification text", "Submittals" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures", "Communication Devices", "Data Devices", "Security Devices", "Fire Alarm Devices", "Plumbing Fixtures", "Mechanical Equipment" },
                ExcludedCategories = new List<string> { "Pipes", "Pipe Fittings" },
                ExpectedFamilyTypeHints = new List<string> { "nameplate", "label", "tag", "mark", "marker", "identification" },
                ExpectedParameters = new List<string> { "Mark", "Type Mark", "Equipment ID", "Tag", "Label", "Nameplate", "Identification", "Manufacturer", "Model", "Comments" },
                CandidateScopeReason = "Scope to elements that can carry identity metadata. Equipment presence alone is not enough for Met.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Direct mark/tag/nameplate evidence is needed for a Met result.",
                WhyNotModelCloseable = "Category presence by itself does not prove labeling or identification intent.",
                Patterns = new[] { "identification", "identify", "label", "labeling", "tag", "nameplate", "marker", "marking", "equipment id" },
                Priority = 97
            },
            new Rule
            {
                RequirementType = "drawing_spec_manual_owner_approval",
                RequirementTypeReason = "Requirement depends on drawings, specifications, owner approval, or manual coordination rather than pure model presence.",
                ValidationType = ValidationType.Hybrid,
                ValidationTypeReason = "The requirement is not safely closed from model-only evidence and must stay tied to drawings/specifications/manual review.",
                RuleApplied = "drawing_spec_manual_owner_approval",
                RuleFamily = "manual_or_drawing_review",
                TriggerKeywords = new List<string> { "per drawings", "per specifications", "owner approval", "approved by owner", "coordinate with owner", "verify in field", "as shown", "provide as required", "confirm" },
                ExpectedEvidenceSources = new List<string> { "Drawings", "Specifications", "Owner direction", "Manual review" },
                AllowedCategories = new List<string>(),
                ExcludedCategories = new List<string>(),
                ExpectedFamilyTypeHints = new List<string>(),
                ExpectedParameters = new List<string> { "Sheet Reference", "Comments" },
                CandidateScopeReason = "No model-only scope is safe here; treat as drawing/spec/manual/owner evidence.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = false,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is not sufficient for a Met result unless a very explicit model parameter exists and the requirement is purely model-driven.",
                WhyNotModelCloseable = "This rule is review-driven; do not infer Met from generic category or level data.",
                Patterns = new[] { "drawing", "drawings", "specification", "specifications", "owner approval", "approved by owner", "coordinate with owner", "verify in field", "as shown", "per drawings", "per specifications" },
                Priority = 96
            },
            new Rule
            {
                RequirementType = "field_execution_demolition_protection",
                RequirementTypeReason = "Requirement concerns demolition, abandoned elements, salvage, protection, or installation method restrictions.",
                ValidationType = ValidationType.Manual,
                ValidationTypeReason = "Demolition and field execution requirements depend on phase, drawings, specs, and field verification more than model presence.",
                RuleApplied = "field_execution_demolition_protection",
                RuleFamily = "manual_or_drawing_review",
                TriggerKeywords = new List<string> { "demolish", "demolition", "remove", "abandoned", "existing", "salvage", "protect", "clean", "do not fasten", "do not use", "contractor shall" },
                ExpectedEvidenceSources = new List<string> { "Drawings", "Specifications", "Field verification", "Phase data" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures", "Mechanical Equipment", "Plumbing Fixtures", "Pipes", "Pipe Fittings", "Pipe Accessories" },
                ExcludedCategories = new List<string>(),
                ExpectedFamilyTypeHints = new List<string> { "abandoned", "demolition", "blank cover", "salvage", "protection", "remove" },
                ExpectedParameters = new List<string> { "Phase Created", "Phase Demolished", "Demolition Status", "Status", "Comments" },
                CandidateScopeReason = "Scope to model categories that can be demolished/abandoned, but require phase/manual evidence to close.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is insufficient unless phase/demolition data and direct removal context are exported.",
                WhyNotModelCloseable = "If phase/demolition context is missing, keep the requirement reviewable.",
                Patterns = new[] { "demolish", "demolition", "abandoned", "remove", "relocate", "salvage", "protect", "clean", "do not fasten", "contractor shall", "existing", "backfill", "excavat", "compacting", "sand bed", "pipe burial", "geotechnical", "stabilized sand", "trench" },
                Priority = 97
            },
            new Rule
            {
                RequirementType = "mechanical_controls_ddc_emcs",
                RequirementTypeReason = "Requirement references DDC, EMCS, BAS/BMS, control sequences, fan control, metering, or building automation control points.",
                ValidationType = ValidationType.Specification,
                ValidationTypeReason = "DDC/EMCS/control-sequence requirements depend on controls drawings, specifications, and system integration that are not modeled in Revit.",
                RuleApplied = "mechanical_controls_ddc_emcs",
                RuleFamily = "controls",
                TriggerKeywords = new List<string> { "DDC", "EMCS", "BAS", "BMS", "control sequence", "fan control", "metering", "building automation", "points list" },
                ExpectedEvidenceSources = new List<string> { "Controls drawings", "Specifications", "DDC/EMCS schedules", "Points list", "Sequence of operations" },
                AllowedCategories = new List<string> { "Mechanical Equipment", "Electrical Equipment" },
                ExcludedCategories = new List<string> { "Plumbing Fixtures", "Lighting Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "DDC", "EMCS", "BAS", "controls", "sensor", "automation" },
                ExpectedParameters = new List<string> { "Controls", "System", "Sequence", "Comments" },
                CandidateScopeReason = "DDC/EMCS/control-sequence requirements are never safely model-closed. No controls elements exist in the export.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "None",
                WhyNotModelCloseable = "DDC/EMCS control sequences, fan controls, metering ties, and building automation points are not modeled in Revit.",
                Patterns = new[] { @"\bDDC\b", @"\bEMCS\b", @"\bBAS\b", @"\bBMS\b", @"control.*fan", @"fan.*control", "control sequence", "sequence of operations", "building automation", "points list", @"tied to DDC", "metering", "venturi" },
                Priority = 96
            },
            new Rule
            {
                RequirementType = "dimension_clearance_distance_separation",
                RequirementTypeReason = "Requirement states a minimum or maximum clearance, distance, separation, or offset constraint.",
                ValidationType = ValidationType.Drawing,
                ValidationTypeReason = "Clearance and separation constraints are drawing/spec driven and cannot be closed from element presence alone.",
                RuleApplied = "dimension_clearance_distance_separation",
                RuleFamily = "dimension",
                TriggerKeywords = new List<string> { "clearance", "distance", "separation", "offset", "minimum", "maximum", "spacing" },
                ExpectedEvidenceSources = new List<string> { "Drawings", "Specifications", "Field verification", "Measured dimensions" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures", "Mechanical Equipment", "Plumbing Fixtures", "Conduits", "Cable Trays" },
                ExcludedCategories = new List<string> { "Pipes", "Pipe Fittings" },
                ExpectedFamilyTypeHints = new List<string> { "clearance", "distance", "separation", "offset", "spacing" },
                ExpectedParameters = new List<string> { "Clearance", "Distance", "Separation", "Offset", "Comments" },
                DirectClosingEvidence = new List<string> { "Measured clearance", "Distance", "Separation", "Offset", "Detail note" },
                SupportingContext = new List<string> { "Location", "Level", "Host", "Adjacent element" },
                MissingDirectEvidence = new List<string> { "Measured clearance", "Distance", "Separation", "Offset" },
                CandidateScopeReason = "Scope to explicit dimensional language. Category and level are supporting context only.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is not sufficient unless measured clearance or offset data is present.",
                WhyNotModelCloseable = "Clearance and separation constraints cannot be closed from category or level alone.",
                Patterns = new[] { @"clearance", @"distance", @"separation", @"offset", @"spacing", @"minimum", @"maximum", @"maintain", @"at least", @"no less than" },
                Priority = 99
            },
            new Rule
            {
                RequirementType = "installation_method_constraint",
                RequirementTypeReason = "Requirement constrains installation method, roof penetration, attic routing, or field execution detail.",
                ValidationType = ValidationType.Drawing,
                ValidationTypeReason = "Installation method constraints require drawings/specifications/field verification rather than model presence.",
                RuleApplied = "installation_method_constraint",
                RuleFamily = "installation",
                TriggerKeywords = new List<string> { "roof penetration", "penetration", "attic", "sealed roof pipe", "hooded penetration", "installation", "field verify" },
                ExpectedEvidenceSources = new List<string> { "Installation details", "Drawings", "Specifications", "Field verification" },
                AllowedCategories = new List<string> { "Mechanical Equipment", "Electrical Equipment", "Lighting Fixtures", "Plumbing Fixtures", "Pipes", "Pipe Accessories" },
                ExcludedCategories = new List<string>(),
                ExpectedFamilyTypeHints = new List<string> { "roof penetration", "attic", "sealed", "hooded", "penetration" },
                ExpectedParameters = new List<string> { "Comments", "Sheet Reference", "Phase Created", "Phase Demolished" },
                DirectClosingEvidence = new List<string> { "Installation detail", "Field verification", "Specification note" },
                SupportingContext = new List<string> { "Equipment presence", "Location", "Level" },
                MissingDirectEvidence = new List<string> { "Installation detail", "Field verification", "Specification note" },
                CandidateScopeReason = "Scope to installation language and roof/attic constraints. Model presence is supporting context only.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is not sufficient unless installation detail evidence is present.",
                WhyNotModelCloseable = "Roof penetration and field execution constraints cannot be closed from equipment presence alone.",
                Patterns = new[] { @"roof penetration", @"roof penetrations", @"hooded penetration", @"hooded penetrations", @"sealed roof pipe", @"in-?line exhaust fan", @"field execution", @"installation method" },
                Priority = 95
            },
            new Rule
            {
                RequirementType = "code_jurisdiction_requirement",
                RequirementTypeReason = "Requirement references code, jurisdiction, AHJ, or compliance matrix language.",
                ValidationType = ValidationType.Specification,
                ValidationTypeReason = "Code and jurisdiction requirements need code review and cannot be closed from model presence.",
                RuleApplied = "code_jurisdiction_requirement",
                RuleFamily = "code",
                TriggerKeywords = new List<string> { "code", "jurisdiction", "ahj", "compliance", "nec", "iecc", "nfpa" },
                ExpectedEvidenceSources = new List<string> { "Code citations", "Jurisdiction review", "Compliance matrix" },
                AllowedCategories = new List<string>(),
                ExcludedCategories = new List<string>(),
                ExpectedFamilyTypeHints = new List<string>(),
                ExpectedParameters = new List<string> { "Comments", "Sheet Reference" },
                DirectClosingEvidence = new List<string> { "Code citation", "Code matrix", "Jurisdiction review record" },
                SupportingContext = new List<string> { "Requirement text", "Discipline", "System" },
                MissingDirectEvidence = new List<string> { "Code citation", "Jurisdiction review" },
                CandidateScopeReason = "Code and jurisdiction language is external to the model. Model evidence is only supporting context.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = false,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "None",
                WhyNotModelCloseable = "Code/jurisdiction requirements require code review and cannot be closed from category or level.",
                Patterns = new[] { @"\bcode\b", @"jurisdiction", @"\bahj\b", @"\bnec\b", @"\bnfpa\b", @"\biecc\b", @"compliance", @"code compliance" },
                Priority = 94
            },
            new Rule
            {
                RequirementType = "mechanical_performance_feature",
                RequirementTypeReason = "Requirement references RTU or mechanical performance features such as compressor speed, ionization, or efficiency.",
                ValidationType = ValidationType.Specification,
                ValidationTypeReason = "Mechanical performance requirements depend on product or submittal evidence rather than equipment presence alone.",
                RuleApplied = "mechanical_performance_feature",
                RuleFamily = "mechanical",
                TriggerKeywords = new List<string> { "compressor", "ionization", "performance", "efficiency", "two-speed", "rtu", "seer", "eer" },
                ExpectedEvidenceSources = new List<string> { "Manufacturer metadata", "Product data", "Specification text", "Submittals" },
                AllowedCategories = new List<string> { "Mechanical Equipment" },
                ExcludedCategories = new List<string> { "Pipes", "Pipe Fittings", "Lighting Fixtures", "Electrical Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "rtu", "compressor", "equipment", "mechanical" },
                ExpectedParameters = new List<string> { "Manufacturer", "Model", "Description", "Comments" },
                DirectClosingEvidence = new List<string> { "Manufacturer metadata", "Product data", "Specification text" },
                SupportingContext = new List<string> { "Mechanical equipment presence", "Family/type" },
                MissingDirectEvidence = new List<string> { "Manufacturer metadata", "Product data", "Specification text" },
                CandidateScopeReason = "Scope to mechanical equipment and performance language. Presence is only supporting context.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is not sufficient unless performance metadata is present.",
                WhyNotModelCloseable = "RTU performance and ionization language cannot be closed from category and level alone.",
                Patterns = new[] { @"two-?speed", @"bi-?polar ionization", @"compressor", @"performance", @"efficiency", @"\brtu\b", @"\bseer\b", @"\beer\b", @"without demand control ventilation", @"no demand control ventilation" },
                Priority = 84
            },
            new Rule
            {
                RequirementType = "lighting_control_scheme",
                RequirementTypeReason = "Requirement references lighting controls, occupancy sensors, switching schemes, dimming, or control zones.",
                ValidationType = ValidationType.Hybrid,
                ValidationTypeReason = "Lighting control requirements depend on control device placement and programming that cannot be verified from fixture presence alone.",
                RuleApplied = "lighting_control_scheme",
                RuleFamily = "lighting_controls",
                TriggerKeywords = new List<string> { "lighting control", "occupancy sensor", "switchpack", "dimming", "switching zone", "vacancy sensor", "daylight harvesting" },
                ExpectedEvidenceSources = new List<string> { "Lighting controls drawings", "Specifications", "Control device elements" },
                AllowedCategories = new List<string> { "Lighting Fixtures", "Electrical Fixtures", "Electrical Equipment" },
                ExcludedCategories = new List<string> { "Mechanical Equipment", "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "occupancy sensor", "switchpack", "dimmer", "relay", "lighting control" },
                ExpectedParameters = new List<string> { "Controls", "System", "Panel", "Circuit Number" },
                CandidateScopeReason = "Lighting control requirements cannot be closed from fixture presence alone; they need control device or scheme evidence.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Partial at best",
                WhyNotModelCloseable = "Lighting fixture presence does not prove control scheme, occupancy sensor placement, or dimming capability.",
                Patterns = new[] { "lighting control", "occupancy sensor", "switchpack", "dimming", "switching zone", "vacancy sensor", "daylight harvest", @"lighting\s+switch" },
                Priority = 92
            },
            new Rule
            {
                RequirementType = "operation_maintenance_manual",
                RequirementTypeReason = "Requirement references O&M manuals, maintenance manuals, operating instructions, or content deliverables.",
                ValidationType = ValidationType.Manual,
                ValidationTypeReason = "O&M manual requirements are deliverable-driven and cannot be verified from model presence.",
                RuleApplied = "operation_maintenance_manual",
                RuleFamily = "closeout",
                TriggerKeywords = new List<string> { "O&M manual", "maintenance manual", "operating manual", "operation and maintenance manual", "content for each unit" },
                ExpectedEvidenceSources = new List<string> { "O&M manuals", "Specifications", "Closeout deliverables" },
                AllowedCategories = new List<string>(),
                ExcludedCategories = new List<string>(),
                ExpectedFamilyTypeHints = new List<string>(),
                ExpectedParameters = new List<string> { "Comments", "Status" },
                CandidateScopeReason = "O&M manual requirements depend on external deliverables, not model elements.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = false,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "None",
                WhyNotModelCloseable = "O&M manuals are deliverable artifacts, not model elements.",
                Patterns = new[] { @"O\s*&\s*M\s+manual", "maintenance manual", "operating manual", "operation and maintenance manual", "content for each unit", @"O\s*&\s*M\s+documentation" },
                Priority = 91
            },
            new Rule
            {
                RequirementType = "attic_stock_spare_parts",
                RequirementTypeReason = "Requirement references attic stock, spare parts, stock quantities, or physical inventory deliverables.",
                ValidationType = ValidationType.Manual,
                ValidationTypeReason = "Attic stock and spare parts requirements are procurement/physical-inventory deliverables and cannot be verified from model presence.",
                RuleApplied = "attic_stock_spare_parts",
                RuleFamily = "closeout",
                TriggerKeywords = new List<string> { "attic stock", "spare part", "stock zero", "stock quantity" },
                ExpectedEvidenceSources = new List<string> { "Procurement records", "Closeout deliverables", "Specifications" },
                AllowedCategories = new List<string>(),
                ExcludedCategories = new List<string>(),
                ExpectedFamilyTypeHints = new List<string>(),
                ExpectedParameters = new List<string> { "Comments" },
                CandidateScopeReason = "Attic stock and spare parts requirements are physical inventory, not model elements.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = false,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "None",
                WhyNotModelCloseable = "Attic stock quantities and spare parts are procurement deliverables.",
                Patterns = new[] { "attic stock", "spare part", "spare parts", "stock zero", "stock quantity" },
                Priority = 91
            },
            new Rule
            {
                RequirementType = "panel_circuit_power",
                RequirementTypeReason = "Requirement is about panel, circuit, feeder, load, or electrical power assignment.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Panel and circuit assignments are model-checkable when the relevant electrical parameters are exported.",
                RuleApplied = "panel_circuit_power",
                RuleFamily = "electrical_connection",
                TriggerKeywords = new List<string> { "panel", "circuit", "breaker", "feeder", "supply from", "load", "voltage", "power" },
                ExpectedEvidenceSources = new List<string> { "Revit electrical fixture/equipment parameters", "Panel schedules" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures" },
                ExcludedCategories = new List<string> { "Mechanical Equipment", "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "panel", "receptacle", "fixture", "equipment" },
                ExpectedParameters = new List<string> { "Panel", "Panel Name", "Circuit Number", "Circuit", "Supply From", "Load Name", "Voltage" },
                CandidateScopeReason = "Scope to electrical categories that can actually carry panel/circuit metadata. Do not use unrelated categories.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = true,
                ModelEvidenceSufficiency = "Met requires direct electrical connection parameters.",
                WhyNotModelCloseable = "Generic category presence is not enough to prove panel/circuit assignment.",
                Patterns = new[] { "panel", "circuit", "breaker", "supply from", "load", "voltage", "power" },
                Priority = 90
            },
            new Rule
            {
                RequirementType = "outlets_receptacles_devices",
                RequirementTypeReason = "Requirement is about outlets, receptacles, duplex circuits, or related device placement/power.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Outlet and receptacle requirements are model-checkable when the relevant device and electrical parameters exist.",
                RuleApplied = "outlets_receptacles_devices",
                RuleFamily = "electrical_connection",
                TriggerKeywords = new List<string> { "outlet", "receptacle", "duplex", "general purpose circuit", "120v", "208v", "quad" },
                ExpectedEvidenceSources = new List<string> { "Revit electrical fixture/device parameters", "Panel schedules" },
                AllowedCategories = new List<string> { "Electrical Fixtures", "Electrical Equipment" },
                ExcludedCategories = new List<string> { "Mechanical Equipment", "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "duplex", "receptacle", "outlet", "gfi", "gfcI" },
                ExpectedParameters = new List<string> { "Voltage", "Panel", "Panel Name", "Circuit Number", "Circuit", "Load Name", "Room", "Space", "Level" },
                CandidateScopeReason = "Scope to electrical fixture/device candidates. Do not inspect the full model.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Met requires direct voltage/panel/circuit evidence on outlet candidates.",
                WhyNotModelCloseable = "Category and level alone cannot prove outlet circuit assignment.",
                Patterns = new[] { "outlet", "receptacle", "duplex", "general purpose circuit", "120v", "208v", "quad" },
                Priority = 89
            },
            new Rule
            {
                RequirementType = "technology_low_voltage_security_fire_alarm",
                RequirementTypeReason = "Requirement is about data, voice, security, fire alarm, intercom, or low-voltage devices.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Technology device presence is model-checkable when the device categories and direct parameters exist.",
                RuleApplied = "technology_low_voltage_security_fire_alarm",
                RuleFamily = "technology",
                TriggerKeywords = new List<string> { "technology", "data", "voice", "cctv", "matv", "security", "fire alarm", "access control", "intercom", "low voltage", "mdf", "idf", "rack" },
                ExpectedEvidenceSources = new List<string> { "Revit technology device elements", "Network/security schedules" },
                AllowedCategories = new List<string> { "Communication Devices", "Data Devices", "Fire Alarm Devices", "Security Devices", "Nurse Call Devices", "Telephone Devices" },
                ExcludedCategories = new List<string> { "Mechanical Equipment", "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "communication device", "data device", "fire alarm", "security device", "rack" },
                ExpectedParameters = new List<string> { "Level", "Panel", "Circuit Number", "Device ID", "Address", "System" },
                CandidateScopeReason = "Scope to low-voltage device categories only. Do not use grounding or mechanical requirements here.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Met requires relevant device candidates and direct low-voltage metadata.",
                WhyNotModelCloseable = "Do not close grounding or manufacturer/spec intent from low-voltage device presence alone.",
                Patterns = new[] { "technology", "data", "voice", "cctv", "matv", "security", "fire alarm", "access control", "intercom", "low voltage", "mdf", "idf", "rack" },
                Priority = 88
            },
            new Rule
            {
                RequirementType = "mechanical_equipment_coverage",
                RequirementTypeReason = "Requirement is about mechanical equipment presence or placement.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Mechanical equipment coverage is model-checkable when mechanical equipment is present and the requirement is truly about placement.",
                RuleApplied = "mechanical_equipment_coverage",
                RuleFamily = "mechanical",
                TriggerKeywords = new List<string> { "airflow", "hvac", "mechanical equipment", "pump", "chiller", "coil", "fan", "rtu", "ahu" },
                ExpectedEvidenceSources = new List<string> { "Revit mechanical equipment elements" },
                AllowedCategories = new List<string> { "Mechanical Equipment" },
                ExcludedCategories = new List<string> { "Electrical Fixtures", "Electrical Equipment" },
                ExpectedFamilyTypeHints = new List<string> { "mechanical equipment", "pump", "fan", "chiller", "rtu", "ahu" },
                ExpectedParameters = new List<string> { "Level", "Manufacturer", "Model", "Description" },
                CandidateScopeReason = "Scope to mechanical equipment categories only. Do not use this for grounding, spec, or plumbing checks.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = false,
                AllowsModelOnlyMet = true,
                ModelEvidenceSufficiency = "Met requires mechanical equipment candidates and the requirement must truly be about placement/coverage.",
                WhyNotModelCloseable = "Mechanical coverage cannot close grounding, hose bibb, or spec/manual requirements.",
                Patterns = new[] { "airflow", "hvac", "mechanical equipment", "pump", "chiller", "coil", "fan", "rtu", "ahu" },
                Priority = 50
            },
            new Rule
            {
                RequirementType = "level_location_mounting_placement",
                RequirementTypeReason = "Requirement is primarily about level, location, mounting, elevation, or placement.",
                ValidationType = ValidationType.Model,
                ValidationTypeReason = "Level/location requirements are model-checkable when the requirement is truly about placement and not a higher-priority semantic family.",
                RuleApplied = "level_location_mounting_placement",
                RuleFamily = "placement",
                TriggerKeywords = new List<string> { "level", "elevation", "mounted", "mounting", "roof", "located", "location", "space", "room", "placement" },
                ExpectedEvidenceSources = new List<string> { "Revit level assignment" },
                AllowedCategories = new List<string> { "Electrical Fixtures", "Lighting Fixtures", "Mechanical Equipment", "Plumbing Fixtures", "Electrical Equipment" },
                ExcludedCategories = new List<string> { "Pipes", "Pipe Fittings" },
                ExpectedFamilyTypeHints = new List<string> { "level", "elevation", "mount", "roof", "location" },
                ExpectedParameters = new List<string> { "Level", "Offset", "Elevation", "Room", "Space", "Host" },
                CandidateScopeReason = "Use only when the requirement is primarily about placement. Words like roof or level must not override a higher-priority semantic family.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = true,
                ModelEvidenceSufficiency = "Met requires actual level/location data on relevant elements.",
                WhyNotModelCloseable = "Do not use level evidence to close plumbing, grounding, manufacturer/spec, or demolition requirements.",
                Patterns = new[] { "level", "elevation", "mounted", "mounting", "roof", "located", "location", "space", "room", "placement" },
                Priority = 10
            },
            new Rule
            {
                RequirementType = "conduit_raceway",
                RequirementTypeReason = "Requirement references conduit, raceway, pathways, sleeves, or underground connector/routing details.",
                ValidationType = ValidationType.Hybrid,
                ValidationTypeReason = "Conduit and raceway requirements can have model hints, but closure usually depends on drawings, specifications, and installation details.",
                RuleApplied = "conduit_raceway",
                RuleFamily = "raceway",
                TriggerKeywords = new List<string> { "conduit", "raceway", "pathway", "sleeve", "underground", "connector", "ductbank" },
                ExpectedEvidenceSources = new List<string> { "Conduit/raceway model elements", "Drawings", "Specifications", "Field verification" },
                AllowedCategories = new List<string> { "Conduits", "Conduit Fittings", "Cable Trays", "Electrical Equipment", "Electrical Fixtures" },
                ExcludedCategories = new List<string> { "Mechanical Equipment", "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "conduit", "raceway", "connector", "sleeve", "ductbank" },
                ExpectedParameters = new List<string> { "Conduit", "Raceway", "System Type", "Size", "Comments" },
                CandidateScopeReason = "Scope to conduit/raceway categories and explicit conduit/raceway family or parameter hints; do not use unrelated equipment level evidence.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Direct conduit/raceway evidence is needed before model evidence can support closure.",
                WhyNotModelCloseable = "Conduit installation restrictions and underground connector requirements are not proven by generic model presence.",
                Patterns = new[] { "conduit", "raceway", "pathway", "sleeve", "underground connector", "ductbank" },
                Priority = 87
            },
            new Rule
            {
                RequirementType = "controls_bms_bas_contactors_relays",
                RequirementTypeReason = "Requirement references controls, BMS/BAS, contactors, relays, starters, or control wiring.",
                ValidationType = ValidationType.Hybrid,
                ValidationTypeReason = "Controls requirements are partly model-checkable but often require controls drawings and coordination.",
                RuleApplied = "controls_bms_bas_contactors_relays",
                RuleFamily = "controls",
                TriggerKeywords = new List<string> { "controls", "bms", "bas", "contactor", "relay", "starter", "control wiring" },
                ExpectedEvidenceSources = new List<string> { "Electrical/control equipment parameters", "Controls drawings", "Specifications" },
                AllowedCategories = new List<string> { "Electrical Equipment", "Mechanical Equipment", "Communication Devices", "Data Devices" },
                ExcludedCategories = new List<string> { "Plumbing Fixtures" },
                ExpectedFamilyTypeHints = new List<string> { "contactor", "relay", "starter", "bms", "bas", "controls" },
                ExpectedParameters = new List<string> { "Panel", "Circuit Number", "System", "Controls", "Comments" },
                CandidateScopeReason = "Scope to equipment or low-voltage controls candidates with explicit controls/BMS/BAS/contactors/relays hints.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = true,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is partial unless control system parameters or direct controls elements are exported.",
                WhyNotModelCloseable = "Controls coordination generally requires drawings/specification review.",
                Patterns = new[] { "controls", @"\bbms\b", @"\bbas\b", "contactor", "relay", "starter", "control wiring" },
                Priority = 86
            },
            new Rule
            {
                RequirementType = "commissioning_testing_om_training",
                RequirementTypeReason = "Requirement references commissioning, testing, balancing, O&M manuals, warranty, or training.",
                ValidationType = ValidationType.Manual,
                ValidationTypeReason = "Commissioning/testing/O&M/training requirements are deliverable and process evidence, not safely model-closed.",
                RuleApplied = "commissioning_testing_om_training",
                RuleFamily = "closeout",
                TriggerKeywords = new List<string> { "commissioning", "testing", "test", "balancing", "o&m", "operation and maintenance", "training", "warranty" },
                ExpectedEvidenceSources = new List<string> { "Commissioning records", "Test reports", "O&M manuals", "Training logs", "Specifications" },
                AllowedCategories = new List<string>(),
                ExcludedCategories = new List<string>(),
                ExpectedFamilyTypeHints = new List<string>(),
                ExpectedParameters = new List<string> { "Comments", "Status" },
                CandidateScopeReason = "No full-model candidate pool is safe; closure depends on external closeout deliverables.",
                FallbackAllowed = false,
                FullModelFallbackAllowed = false,
                RequiresDirectParameterEvidence = false,
                AllowsModelOnlyMet = false,
                ModelEvidenceSufficiency = "Model evidence is not sufficient for commissioning, testing, O&M, or training closure.",
                WhyNotModelCloseable = "These requirements require external closeout evidence.",
                Patterns = new[] { "commissioning", "testing", "test report", "balancing", @"\bo&m\b", "operation and maintenance", "training", "warranty" },
                Priority = 85
            }
        };

        public static RequirementSemanticProfile Classify(string requirementText, string categoryList, RequirementDiscipline discipline)
        {
            string text = RequirementDisciplineNormalizer.NormalizeText(requirementText ?? string.Empty);
            string categoryText = RequirementDisciplineNormalizer.NormalizeText(categoryList ?? string.Empty);

            Rule match = Rules
                .Select(rule => new
                {
                    Rule = rule,
                    Score = Score(rule, text, categoryText)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Rule.Priority)
                .ThenByDescending(item => item.Score)
                .Select(item => item.Rule)
                .FirstOrDefault();

            if (match == null)
            {
                match = new Rule
                {
                    RequirementType = "unknown_ambiguous",
                    RequirementTypeReason = "No stable semantic family matched the requirement text.",
                    ValidationType = ValidationType.Hybrid,
                    ValidationTypeReason = "The requirement is ambiguous or cross-disciplinary.",
                    RuleApplied = "(none)",
                    RuleFamily = "unmatched",
                    CandidateScopeReason = "No safe candidate scope could be established from the text alone.",
                    FallbackAllowed = true,
                    FullModelFallbackAllowed = false,
                    RequiresDirectParameterEvidence = false,
                    AllowsModelOnlyMet = false,
                    ModelEvidenceSufficiency = "Manual review is required.",
                    WhyNotModelCloseable = "The requirement was not specific enough to close from model evidence alone.",
                    Patterns = Array.Empty<string>(),
                    Priority = 1
                };
            }

            return new RequirementSemanticProfile
            {
                RequirementType = match.RequirementType,
                RequirementTypeReason = match.RequirementTypeReason,
                ValidationType = match.ValidationType,
                ValidationTypeReason = match.ValidationTypeReason,
                RuleApplied = match.RuleApplied,
                RuleFamily = match.RuleFamily,
                TriggerKeywords = match.TriggerKeywords.ToList(),
                ExpectedEvidenceSources = match.ExpectedEvidenceSources.ToList(),
                AllowedCategories = match.AllowedCategories.ToList(),
                ExcludedCategories = match.ExcludedCategories.ToList(),
                ExpectedFamilyTypeHints = match.ExpectedFamilyTypeHints.ToList(),
                ExpectedParameters = match.ExpectedParameters.ToList(),
                DirectClosingEvidence = match.DirectClosingEvidence.ToList(),
                SupportingContext = match.SupportingContext.ToList(),
                MissingDirectEvidence = match.MissingDirectEvidence.ToList(),
                CandidateScopeReason = match.CandidateScopeReason,
                FallbackAllowed = match.FallbackAllowed,
                FullModelFallbackAllowed = match.FullModelFallbackAllowed,
                RequiresDirectParameterEvidence = match.RequiresDirectParameterEvidence,
                AllowsModelOnlyMet = match.AllowsModelOnlyMet,
                ModelEvidenceSufficiency = match.ModelEvidenceSufficiency,
                WhyNotModelCloseable = match.WhyNotModelCloseable
            };
        }

        private static int Score(Rule rule, string text, string categoryText)
        {
            int patternScore = 0;
            int categoryScore = 0;

            if (string.Equals(rule.RequirementType, "manufacturer_product_spec_submittal", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(text, @"\bprotect(ion)?\b|work completion|original manufacturer's condition", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                !Regex.IsMatch(text, @"submittal|product data|acceptable manufacturers|approved equal|catalog|basis of design", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return 0;
            }

            foreach (string pattern in rule.Patterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern) && Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    patternScore++;
                }
            }

            foreach (string categoryHint in rule.AllowedCategories)
            {
                if (!string.IsNullOrWhiteSpace(categoryHint) && categoryText.IndexOf(categoryHint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    categoryScore++;
                }
            }

            if (patternScore == 0)
            {
                return 0;
            }

            return (patternScore * 10) + categoryScore;
        }
    }
}
