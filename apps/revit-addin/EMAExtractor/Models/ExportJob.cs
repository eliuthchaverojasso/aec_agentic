using System;

namespace EMAExtractor.Models
{
    public class ExportJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString("N");
        public string ExportType { get; set; } = "all";
        public string Discipline { get; set; } = "ALL";
        public string Status { get; set; } = "queued";
        public string Phase { get; set; } = "queued";
        public int ProgressPercent { get; set; }
        public int ElementCount { get; set; }
        public string OutputPath { get; set; } = "";
        public int BackendExportId { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}
