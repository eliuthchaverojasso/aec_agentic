using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using EMAExtractor.Enums;
using EMAExtractor.Models;

namespace EMAExtractor.Core
{
    public static class ExportUtils
    {
        public static IList<BuiltInCategory> GetCategoriesForDiscipline(
            ExportDiscipline discipline,
            ExportScope scope = ExportScope.All)
        {
            if (discipline == ExportDiscipline.Electrical && scope == ExportScope.Lighting)
            {
                return new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_LightingFixtures
                };
            }

            switch (discipline)
            {
                case ExportDiscipline.Electrical:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_ElectricalEquipment,
                        BuiltInCategory.OST_ElectricalFixtures,
                        BuiltInCategory.OST_LightingFixtures
                    };

                case ExportDiscipline.Mechanical:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_MechanicalEquipment
                    };

                case ExportDiscipline.Plumbing:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_PlumbingFixtures,
                        BuiltInCategory.OST_PipeCurves,
                        BuiltInCategory.OST_PipeFitting,
                        BuiltInCategory.OST_PipeAccessory
                    };

                case ExportDiscipline.Technology:
                    return new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_CommunicationDevices,
                        BuiltInCategory.OST_DataDevices,
                        BuiltInCategory.OST_FireAlarmDevices,
                        BuiltInCategory.OST_SecurityDevices,
                        BuiltInCategory.OST_NurseCallDevices,
                        BuiltInCategory.OST_TelephoneDevices
                    };

                case ExportDiscipline.All:
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

        public static string GetOutputFileName(
            ExportDiscipline discipline,
            ExportScope scope = ExportScope.All)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            if (discipline == ExportDiscipline.Electrical && scope == ExportScope.Lighting)
            {
                return $"ema_extract_electrical_lighting_{timestamp}.json";
            }

            switch (discipline)
            {
                case ExportDiscipline.Electrical:
                    return $"ema_extract_electrical_{timestamp}.json";

                case ExportDiscipline.Mechanical:
                    return $"ema_extract_mechanical_{timestamp}.json";

                case ExportDiscipline.Plumbing:
                    return $"ema_extract_plumbing_{timestamp}.json";

                case ExportDiscipline.All:
                default:
                    return $"ema_extract_all_{timestamp}.json";
            }
        }

        public static ExportElementRecord BuildElementRecord(Document doc, Element element)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc));
            }

            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            ElementId typeId = element.GetTypeId();

            Element typeElement = typeId != ElementId.InvalidElementId
                ? doc.GetElement(typeId)
                : null;

            ExportElementRecord record = new ExportElementRecord
            {
                ProjectTitle = doc.Title,
                ElementId = SafeElementIdValue(element.Id),
                UniqueId = element.UniqueId,
                Category = element.Category != null ? element.Category.Name : null,
                Name = element.Name,
                Family = GetFamilyName(element),
                Type = typeElement != null ? typeElement.Name : null,
                Level = GetLevelName(doc, element),
                InstanceParameters = GetParameterDictionary(element),
                TypeParameters = typeElement != null
                    ? GetParameterDictionary(typeElement)
                    : new Dictionary<string, ParameterRecord>()
            };

            return record;
        }

        private static string GetFamilyName(Element element)
        {
            try
            {
                FamilyInstance familyInstance = element as FamilyInstance;

                if (familyInstance != null &&
                    familyInstance.Symbol != null &&
                    familyInstance.Symbol.Family != null)
                {
                    return familyInstance.Symbol.Family.Name;
                }

                ElementType elementType = element as ElementType;

                if (elementType != null &&
                    elementType.FamilyName != null)
                {
                    return elementType.FamilyName;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetLevelName(Document doc, Element element)
        {
            try
            {
                Parameter levelParameter =
                    element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM) ??
                    element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM) ??
                    element.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);

                if (levelParameter != null && levelParameter.StorageType == StorageType.ElementId)
                {
                    ElementId levelId = levelParameter.AsElementId();

                    if (levelId != ElementId.InvalidElementId)
                    {
                        Element level = doc.GetElement(levelId);
                        return level != null ? level.Name : null;
                    }
                }

                if (element.LevelId != ElementId.InvalidElementId)
                {
                    Element level = doc.GetElement(element.LevelId);
                    return level != null ? level.Name : null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, ParameterRecord> GetParameterDictionary(Element element)
        {
            Dictionary<string, ParameterRecord> result = new Dictionary<string, ParameterRecord>();

            try
            {
                foreach (Parameter parameter in element.Parameters)
                {
                    if (parameter == null || parameter.Definition == null)
                    {
                        continue;
                    }

                    string name = parameter.Definition.Name;

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (result.ContainsKey(name))
                    {
                        continue;
                    }

                    result[name] = BuildParameterRecord(parameter);
                }
            }
            catch
            {
                // Keep export resilient. A bad parameter should not stop the full export.
            }

            return result;
        }

        private static ParameterRecord BuildParameterRecord(Parameter parameter)
        {
            return new ParameterRecord
            {
                StorageType = parameter.StorageType.ToString(),
                HasValue = parameter.HasValue,
                IsReadOnly = parameter.IsReadOnly,
                IsShared = parameter.IsShared,
                Guid = parameter.IsShared ? TryGetSharedParameterGuid(parameter) : null,
                ValueString = SafeValueString(parameter),
                RawValue = SafeRawValue(parameter)
            };
        }

        private static string TryGetSharedParameterGuid(Parameter parameter)
        {
            try
            {
                if (parameter != null && parameter.IsShared)
                {
                    return parameter.GUID.ToString();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string SafeValueString(Parameter parameter)
        {
            try
            {
                return parameter.AsValueString();
            }
            catch
            {
                return null;
            }
        }

        private static string SafeRawValue(Parameter parameter)
        {
            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        return parameter.AsString();

                    case StorageType.Integer:
                        return parameter.AsInteger().ToString(CultureInfo.InvariantCulture);

                    case StorageType.Double:
                        return parameter.AsDouble().ToString(CultureInfo.InvariantCulture);

                    case StorageType.ElementId:
                        return SafeElementIdValue(parameter.AsElementId()).ToString(CultureInfo.InvariantCulture);

                    case StorageType.None:
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static long SafeElementIdValue(ElementId id)
        {
            if (id == null)
            {
                return -1L;
            }

            if (id == ElementId.InvalidElementId)
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
                        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
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
                        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
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





