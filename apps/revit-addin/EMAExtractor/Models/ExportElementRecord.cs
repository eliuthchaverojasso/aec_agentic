using System.Collections.Generic;

namespace EMAExtractor.Models
{
    public class ExportElementRecord
    {
        public string ProjectTitle { get; set; }
        public string SchemaVersion { get; set; }
        public string ExportProfile { get; set; }
        public string Discipline { get; set; }
        public string Scope { get; set; }
        public long ElementId { get; set; }
        public string UniqueId { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string Level { get; set; }

        public Dictionary<string, ParameterRecord> InstanceParameters { get; set; }
            = new Dictionary<string, ParameterRecord>();

        public Dictionary<string, ParameterRecord> TypeParameters { get; set; }
            = new Dictionary<string, ParameterRecord>();
    }
}
