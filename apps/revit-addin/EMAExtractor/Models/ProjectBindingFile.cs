namespace EMAExtractor.Models
{
    /// <summary>
    /// Deserialisation target for the project_binding.json file that the EMA AI
    /// web app generates. All fields use PascalCase; System.Text.Json with
    /// PropertyNameCaseInsensitive = true maps the snake_case JSON keys.
    /// </summary>
    public class ProjectBindingFile
    {
        public int ProjectId { get; set; } = 0;
        public int ModelId { get; set; } = 0;
        public int ClientId { get; set; } = 0;
        public string ClientCode { get; set; } = "";
        public string ProjectCode { get; set; } = "";
        public string ProjectDisplayName { get; set; } = "";
        public string ProjectFolderName { get; set; } = "";
        public string ApiBaseUrl { get; set; } = "";
        public string DashboardUrl { get; set; } = "";
        public string EnvironmentName { get; set; } = "Local";

        /// <summary>
        /// "local_landing" or "cloud_upload". Empty string triggers auto-derivation:
        /// if LandingRoot is non-empty → local_landing, else → cloud_upload.
        /// </summary>
        public string SyncMode { get; set; } = "";

        public string LandingRoot { get; set; } = "";
    }
}
