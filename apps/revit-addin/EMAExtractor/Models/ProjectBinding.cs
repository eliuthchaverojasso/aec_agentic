using System.Globalization;

namespace EMAExtractor.Models
{
    public class ProjectBinding
    {
        public string RevitDocumentTitle { get; set; } = "";
        public int ClientId { get; set; } = 0;
        public int ProjectId { get; set; } = 0;
        public int ModelId { get; set; } = 0;
        public string ClientName { get; set; } = "";
        public string ProjectTitle { get; set; } = "";
        public string CurrentMilestone { get; set; } = "";
        public string ClientCode { get; set; } = "";
        public string ProjectCode { get; set; } = "";
        public string ProjectFolderName { get; set; } = "";
        public string ProjectDisplayName { get; set; } = "";
        public string ProjectSlug { get; set; } = "";

        public string DescribeModelBinding()
        {
            if (ProjectId <= 0)
            {
                return "Project not connected";
            }

            if (ModelId <= 0)
            {
                return "Project connected, model not bound";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Client ID {0} | Project ID {1} | Model ID {2}",
                ClientId,
                ProjectId,
                ModelId);
        }
    }
}
