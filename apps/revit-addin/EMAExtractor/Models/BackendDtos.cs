using System.Collections.Generic;

namespace EMAExtractor.Models
{
    public class ProjectDto
    {
        public int id { get; set; }
        public int? client_id { get; set; }

        // Existing backend shape
        public string project_title { get; set; }
        public string client_name { get; set; }
        public string phase { get; set; }

        // Optional web/frontend/cloud variants
        public string name { get; set; }
        public string project_name { get; set; }
        public string project_code { get; set; }
        public string client_code { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(name)) return name;
                if (!string.IsNullOrWhiteSpace(project_name)) return project_name;
                if (!string.IsNullOrWhiteSpace(project_title)) return project_title;
                return "Project " + id;
            }
        }

        public string FolderName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(project_name)) return project_name;
                if (!string.IsNullOrWhiteSpace(name)) return name;
                if (!string.IsNullOrWhiteSpace(project_title)) return project_title;
                return "Project " + id;
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class ExportDto
    {
        public int id { get; set; }
        public int project_id { get; set; }
        public int model_id { get; set; }
        public string export_type { get; set; }
        public string status { get; set; }
        public int? element_count { get; set; }
        public string completed_at { get; set; }
        public decimal? duration_seconds { get; set; }
    }

    public class IssueDto
    {
        public int id { get; set; }
        public string rule_code { get; set; }
        public string severity { get; set; }
        public string status { get; set; }
        public string element_unique_id { get; set; }
        public string message { get; set; }
        public string created_at { get; set; }
    }

    public class IssueListDto
    {
        public int total { get; set; }
        public List<IssueDto> items { get; set; } = new List<IssueDto>();
    }

    public class ReadinessDto
    {
        public int project_id { get; set; }
        public decimal overall_readiness { get; set; }
        public string label { get; set; }
        public Dictionary<string, int> gap_summary { get; set; } = new Dictionary<string, int>();
        public List<ReadinessGapDto> top_gaps { get; set; } = new List<ReadinessGapDto>();
        public List<ReadinessActionDto> recommended_actions { get; set; } = new List<ReadinessActionDto>();
        public List<TradeReadinessDto> trade_readiness { get; set; } = new List<TradeReadinessDto>();
    }

    public class ReadinessGapDto
    {
        public string rule_code { get; set; }
        public string severity { get; set; }
        public string message { get; set; }
        public string discipline { get; set; }
    }

    public class ReadinessActionDto
    {
        public string action_type { get; set; }
        public string label { get; set; }
        public string detail { get; set; }
        public string severity { get; set; }
        public string discipline { get; set; }
    }

    public class TradeReadinessDto
    {
        public string discipline { get; set; }
        public decimal readiness { get; set; }
        public string label { get; set; }
        public int requirements_total { get; set; }
        public int missing_requirements { get; set; }
        public int critical_issues { get; set; }
        public int high_issues { get; set; }
    }
}
