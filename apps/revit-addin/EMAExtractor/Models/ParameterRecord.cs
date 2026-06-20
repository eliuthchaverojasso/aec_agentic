namespace EMAExtractor.Models
{
    public class ParameterRecord
    {
        public string StorageType { get; set; }
        public bool HasValue { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsShared { get; set; }
        public string Guid { get; set; }
        public string ValueString { get; set; }
        public string RawValue { get; set; }
    }
}