using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using EMAExtractor.Enums;
using EMAExtractor.Models;
using EMAExtractor.Requirements;

namespace EMAExtractor.Core
{
    public class ModelSnapshot
    {
        public List<ExportElementRecord> Records { get; set; } = new List<ExportElementRecord>();
        public int TotalElements { get; set; }
        public string CaptureNote { get; set; }
    }

    public static class ModelSnapshotService
    {
        public static ModelSnapshot Capture(
            Document doc,
            RequirementDiscipline discipline,
            RequirementModelScope scope)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc));
            }

            IList<BuiltInCategory> categories = GetCategoriesForDiscipline(discipline);
            ModelSnapshot snapshot = new ModelSnapshot();

            IEnumerable<Element> elements = CollectElements(doc, scope);

            foreach (Element element in elements)
            {
                try
                {
                    if (element == null || element.Category == null)
                    {
                        continue;
                    }

                    if (!IsInCategories(element, categories))
                    {
                        continue;
                    }

                    ExportElementRecord record = ExportUtils.BuildElementRecord(doc, element);
                    snapshot.Records.Add(record);
                }
                catch
                {
                    // A single bad element should not stop the first-pass check.
                }
            }

            snapshot.TotalElements = snapshot.Records.Count;
            snapshot.CaptureNote = scope == RequirementModelScope.CurrentView
                ? "Requested current-view capture. The service falls back to the entire model if the active view cannot be used."
                : "Captured from the entire model.";

            return snapshot;
        }

        private static IEnumerable<Element> CollectElements(Document doc, RequirementModelScope scope)
        {
            if (scope == RequirementModelScope.CurrentView && doc.ActiveView != null && !doc.ActiveView.IsTemplate)
            {
                try
                {
                    return new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .Cast<Element>();
                }
                catch
                {
                    // Fall back to the full model if the active view cannot be used.
                }
            }

            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements()
                .Cast<Element>();
        }

        private static bool IsInCategories(Element element, IList<BuiltInCategory> categories)
        {
            if (element == null || element.Category == null || categories == null || categories.Count == 0)
            {
                return false;
            }

            long categoryId = SafeElementIdValue(element.Category.Id);
            return categories.Any(category => (int)category == categoryId);
        }

        private static IList<BuiltInCategory> GetCategoriesForDiscipline(RequirementDiscipline discipline)
        {
            switch (discipline)
            {
                case RequirementDiscipline.Electrical:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_ElectricalEquipment,
                        BuiltInCategory.OST_ElectricalFixtures,
                        BuiltInCategory.OST_LightingFixtures
                    };
                case RequirementDiscipline.Lighting:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_LightingFixtures
                    };
                case RequirementDiscipline.Mechanical:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_MechanicalEquipment
                    };
                case RequirementDiscipline.Plumbing:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_PlumbingFixtures,
                        BuiltInCategory.OST_PipeCurves,
                        BuiltInCategory.OST_PipeFitting,
                        BuiltInCategory.OST_PipeAccessory
                    };
                case RequirementDiscipline.Technology:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_CommunicationDevices,
                        BuiltInCategory.OST_DataDevices,
                        BuiltInCategory.OST_FireAlarmDevices,
                        BuiltInCategory.OST_SecurityDevices,
                        BuiltInCategory.OST_NurseCallDevices,
                        BuiltInCategory.OST_TelephoneDevices
                    };
                case RequirementDiscipline.All:
                default:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_ElectricalEquipment,
                        BuiltInCategory.OST_ElectricalFixtures,
                        BuiltInCategory.OST_LightingFixtures,
                        BuiltInCategory.OST_MechanicalEquipment,
                        BuiltInCategory.OST_PlumbingFixtures,
                        BuiltInCategory.OST_PipeCurves,
                        BuiltInCategory.OST_PipeFitting,
                        BuiltInCategory.OST_PipeAccessory,
                        BuiltInCategory.OST_CommunicationDevices,
                        BuiltInCategory.OST_DataDevices,
                        BuiltInCategory.OST_FireAlarmDevices,
                        BuiltInCategory.OST_SecurityDevices,
                        BuiltInCategory.OST_NurseCallDevices,
                        BuiltInCategory.OST_TelephoneDevices
                    };
            }
        }

        private static long SafeElementIdValue(ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId)
            {
                return -1L;
            }

            try
            {
                System.Reflection.PropertyInfo valueProperty = typeof(ElementId).GetProperty("Value");
                if (valueProperty != null)
                {
                    object value = valueProperty.GetValue(id, null);
                    if (value != null)
                    {
                        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
            catch
            {
            }

            try
            {
                System.Reflection.PropertyInfo integerValueProperty = typeof(ElementId).GetProperty("IntegerValue");
                if (integerValueProperty != null)
                {
                    object value = integerValueProperty.GetValue(id, null);
                    if (value != null)
                    {
                        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
            catch
            {
            }

            return -1L;
        }
    }
}
