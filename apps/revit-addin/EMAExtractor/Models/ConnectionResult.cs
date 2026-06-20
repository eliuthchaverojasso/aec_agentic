namespace EMAExtractor.Models
{
    /// <summary>Result returned by ConnectProjectService.Apply.</summary>
    public class ConnectionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string SyncMode { get; set; } = "";
    }
}
