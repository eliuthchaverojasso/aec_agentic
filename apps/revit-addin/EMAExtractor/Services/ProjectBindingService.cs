using Autodesk.Revit.DB;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public static class ProjectBindingService
    {
        public static ProjectBinding Load(Document document = null)
        {
            ProjectBinding binding = LocalConfigService.LoadBinding();
            if (document != null)
            {
                binding.RevitDocumentTitle = document.Title;
            }

            return binding;
        }

        public static void Save(ProjectBinding binding)
        {
            LocalConfigService.SaveBinding(binding);
        }

        public static void Clear()
        {
            LocalConfigService.ClearBinding();
        }
    }
}
