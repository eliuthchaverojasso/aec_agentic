namespace EMAExtractor.Reporting
{
    /// <summary>
    /// Deterministic caps on how much per-requirement evidence detail is embedded
    /// into the generated HTML report and its hidden machine-readable JSON block.
    ///
    /// Why these exist: an uncapped report for 804 requirements against a 21,868-element
    /// model produced a 516 MB HTML file because full element ID lists were embedded up to
    /// nine times per requirement (copy buttons, scroll lists, JSON arrays, AI lookup hints).
    /// Category-wide keyword matches attach thousands of identical IDs to many requirements,
    /// so embedding must be bounded by policy, not by data shape.
    ///
    /// The engine result objects keep the full matched-element data in memory; these caps
    /// only bound what is serialized into the report artifact. Every truncation is
    /// accompanied by an honest total count and a truncated flag so no consumer can
    /// mistake a capped list for the complete evidence set.
    /// </summary>
    public static class EvidenceEmbedLimits
    {
        /// <summary>Maximum element IDs rendered in the expandable HTML ID list.</summary>
        public const int MaxElementIdsInHtml = 100;

        /// <summary>Maximum Revit UniqueIds rendered in the expandable HTML list.</summary>
        public const int MaxUniqueIdsInHtml = 25;

        /// <summary>
        /// Maximum element IDs placed into the "Copy Revit Element IDs" clipboard payload.
        /// Revit's Select Elements by ID dialog is unusable far below this bound anyway.
        /// </summary>
        public const int MaxCopyElementIds = 500;

        /// <summary>Maximum element IDs serialized per requirement in the hidden JSON block.</summary>
        public const int MaxElementIdsInJson = 50;

        /// <summary>Maximum UniqueIds serialized per requirement in the hidden JSON block.</summary>
        public const int MaxUniqueIdsInJson = 25;

        /// <summary>Maximum detailed matched-element objects serialized per requirement in the hidden JSON block.</summary>
        public const int MaxMatchedElementsInJson = 50;

        /// <summary>
        /// At or above this matched-element count, the report labels the match as a broad
        /// category/keyword sweep (supporting context for review) rather than presenting the
        /// list as item-specific evidence. Display-layer rule only; engine status is unchanged.
        /// </summary>
        public const int BroadMatchElementThreshold = 1000;
    }
}
