namespace EMAExtractor.Models
{
    public class UploadResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = "";
        public string ResponseBody { get; set; } = "";
        public string Url { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string Category { get; set; } = "";
    }
}
