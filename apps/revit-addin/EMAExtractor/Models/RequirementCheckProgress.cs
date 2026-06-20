using System;
using System.Collections.Generic;

namespace EMAExtractor.Models
{
    /// <summary>
    /// Progress model for the multi-stage compliance check workflow.
    /// Marshaled from background thread to UI via IProgress&lt;RequirementCheckProgress&gt;.
    /// </summary>
    public class RequirementCheckProgress
    {
        public string StageName { get; set; }
        public int StageIndex { get; set; }
        public int TotalStages { get; set; }
        public double OverallPercent { get; set; }
        public double StagePercent { get; set; }
        public string Message { get; set; }
        public int ProcessedRequirements { get; set; }
        public int TotalRequirements { get; set; }
        public int IndexedElements { get; set; }
        public int KeyIssuesFound { get; set; }
        public string Discipline { get; set; }
        public string Scope { get; set; }
        public TimeSpan Elapsed { get; set; }
        public TimeSpan? EstimatedRemaining { get; set; }
        public bool IsIndeterminate { get; set; }
        public bool CanCancel { get; set; }
        public List<string> DetailLines { get; set; } = new List<string>();

        /// <summary>Stage status for checklist display.</summary>
        public List<StageInfo> Stages { get; set; } = new List<StageInfo>();

        /// <summary>
        /// Clamps OverallPercent and StagePercent to [0, 100].
        /// </summary>
        public void Clamp()
        {
            OverallPercent = Math.Max(0, Math.Min(100, OverallPercent));
            StagePercent = Math.Max(0, Math.Min(100, StagePercent));
        }

        /// <summary>10 predefined stages for the compliance check.</summary>
        public static List<StageInfo> BuildDefaultStages()
        {
            return new List<StageInfo>
            {
                new StageInfo(1, "Preparing requirements"),
                new StageInfo(2, "Capturing model evidence"),
                new StageInfo(3, "Building evidence index"),
                new StageInfo(4, "Matching requirements"),
                new StageInfo(5, "Assigning statuses"),
                new StageInfo(6, "Calculating scores"),
                new StageInfo(7, "Ranking key issues"),
                new StageInfo(8, "Generating report"),
                new StageInfo(9, "Preparing Ask EMA AI"),
                new StageInfo(10, "Complete")
            };
        }
    }

    public class StageInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public StageStatus Status { get; set; }
        public long ElapsedMs { get; set; }

        public StageInfo() { }
        public StageInfo(int index, string name)
        {
            Index = index;
            Name = name;
            Status = StageStatus.Waiting;
        }
    }

    public enum StageStatus
    {
        Waiting,
        Running,
        Complete,
        Warning,
        Failed
    }
}
