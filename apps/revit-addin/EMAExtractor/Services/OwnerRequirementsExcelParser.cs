using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EMAExtractor.Requirements;

namespace EMAExtractor.Services
{
    public class OwnerRequirementsExcelParser
    {
        private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

        public List<OwnerRequirementRow> Parse(string workbookPath)
        {
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                throw new ArgumentException("A workbook path is required.", nameof(workbookPath));
            }

            if (!File.Exists(workbookPath))
            {
                throw new FileNotFoundException("Owner requirements workbook not found.", workbookPath);
            }

            using (ZipArchive archive = ZipFile.OpenRead(workbookPath))
            {
                List<string> sharedStrings = LoadSharedStrings(archive);
                List<SheetInfo> sheets = LoadSheets(archive);
                List<OwnerRequirementRow> rows = new List<OwnerRequirementRow>();

                foreach (SheetInfo sheet in sheets)
                {
                    rows.AddRange(ParseSheet(workbookPath, archive, sheet, sharedStrings));
                }

                return rows;
            }
        }

        private static List<OwnerRequirementRow> ParseSheet(
            string workbookPath,
            ZipArchive archive,
            SheetInfo sheet,
            IReadOnlyList<string> sharedStrings)
        {
            ZipArchiveEntry entry = archive.GetEntry(sheet.TargetPath);
            if (entry == null)
            {
                return new List<OwnerRequirementRow>();
            }

            XDocument document;
            using (Stream stream = entry.Open())
            {
                document = XDocument.Load(stream);
            }

            List<Dictionary<int, string>> rows = new List<Dictionary<int, string>>();
            List<int> rowNumbers = new List<int>();

            foreach (XElement row in document.Descendants(SpreadsheetNamespace + "row"))
            {
                Dictionary<int, string> values = new Dictionary<int, string>();
                int rowNumber = ParseInt(row.Attribute("r") != null ? row.Attribute("r").Value : null);

                foreach (XElement cell in row.Elements(SpreadsheetNamespace + "c"))
                {
                    string reference = (string)cell.Attribute("r");
                    int columnIndex = CellReferenceToColumnIndex(reference);
                    string value = ReadCellValue(cell, sharedStrings);

                    if (columnIndex >= 0 && !string.IsNullOrWhiteSpace(value))
                    {
                        values[columnIndex] = value.Trim();
                    }
                }

                if (values.Count > 0)
                {
                    rows.Add(values);
                    rowNumbers.Add(rowNumber <= 0 ? rows.Count : rowNumber);
                }
            }

            if (rows.Count == 0)
            {
                return new List<OwnerRequirementRow>();
            }

            int headerRowIndex = FindHeaderRowIndex(rows);
            Dictionary<int, string> headerMap = BuildHeaderMap(rows[headerRowIndex]);
            List<OwnerRequirementRow> parsedRows = new List<OwnerRequirementRow>();

            for (int index = headerRowIndex + 1; index < rows.Count; index++)
            {
                Dictionary<int, string> row = rows[index];
                OwnerRequirementRow parsed = BuildRequirementRow(
                    workbookPath,
                    sheet,
                    row,
                    headerMap,
                    rowNumbers[index]);

                if (!string.IsNullOrWhiteSpace(parsed.RequirementText) ||
                    !string.IsNullOrWhiteSpace(parsed.RequirementId) ||
                    !string.IsNullOrWhiteSpace(parsed.Discipline))
                {
                    parsedRows.Add(parsed);
                }
            }

            return parsedRows;
        }

        private static OwnerRequirementRow BuildRequirementRow(
            string workbookPath,
            SheetInfo sheet,
            IDictionary<int, string> row,
            IDictionary<int, string> headerMap,
            int rowNumber)
        {
            OwnerRequirementRow parsed = new OwnerRequirementRow
            {
                SourceFile = workbookPath,
                SourceSheet = sheet.Name,
                RowNumber = rowNumber,
                Discipline = RequirementDisciplineNormalizer.Parse(sheet.Name, RequirementDiscipline.All).ToString()
            };

            foreach (KeyValuePair<int, string> cell in row)
            {
                string header = headerMap.ContainsKey(cell.Key)
                    ? headerMap[cell.Key]
                    : "Column " + cell.Key.ToString(CultureInfo.InvariantCulture);

                parsed.Columns[header] = cell.Value;
            }

            parsed.RequirementId = PickValue(parsed.Columns, new[]
            {
                "Requirement Id",
                "RequirementID",
                "ID",
                "Row Id",
                "Item Id",
                "No."
            });

            parsed.RequirementText = PickValue(parsed.Columns, new[]
            {
                "Requirement",
                "Owner Requirement",
                "Requirement Text",
                "Description",
                "Text",
                "Details",
                "Scope"
            });

            parsed.Category = PickValue(parsed.Columns, new[]
            {
                "Category",
                "System",
                "Group",
                "Type"
            });

            parsed.Status = PickValue(parsed.Columns, new[]
            {
                "Status",
                "State"
            });

            string rowDiscipline = PickValue(parsed.Columns, new[]
            {
                "Discipline",
                "Trade",
                "Workstream",
                "Package"
            });

            if (!string.IsNullOrWhiteSpace(rowDiscipline))
            {
                parsed.Discipline = rowDiscipline;
            }

            if (string.IsNullOrWhiteSpace(parsed.RequirementText) && row.Count > 0)
            {
                parsed.RequirementText = row
                    .OrderByDescending(pair => pair.Value == null ? 0 : pair.Value.Length)
                    .Select(pair => pair.Value)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            }

            if (string.IsNullOrWhiteSpace(parsed.RequirementId))
            {
                parsed.RequirementId = sheet.Name + "-" + rowNumber.ToString(CultureInfo.InvariantCulture);
            }

            return parsed;
        }

        private static Dictionary<int, string> BuildHeaderMap(IDictionary<int, string> headerRow)
        {
            Dictionary<int, string> headerMap = new Dictionary<int, string>();

            foreach (KeyValuePair<int, string> cell in headerRow)
            {
                string normalized = NormalizeHeader(cell.Value);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                headerMap[cell.Key] = normalized;
            }

            return headerMap;
        }

        private static int FindHeaderRowIndex(IReadOnlyList<Dictionary<int, string>> rows)
        {
            int bestIndex = 0;
            int bestScore = -1;

            for (int index = 0; index < Math.Min(rows.Count, 10); index++)
            {
                int score = ScoreHeaderRow(rows[index]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private static int ScoreHeaderRow(IDictionary<int, string> row)
        {
            int score = 0;

            foreach (string value in row.Values)
            {
                string normalized = NormalizeHeader(value);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (ContainsAny(normalized, new[]
                {
                    "requirement",
                    "owner requirement",
                    "description",
                    "discipline",
                    "category",
                    "status",
                    "id",
                    "trade",
                    "text"
                }))
                {
                    score += 2;
                }
                else
                {
                    score += 1;
                }
            }

            return score;
        }

        private static List<SheetInfo> LoadSheets(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (workbookEntry == null || relsEntry == null)
            {
                return new List<SheetInfo>();
            }

            Dictionary<string, string> relationships = LoadRelationships(relsEntry);

            XDocument workbook;
            using (Stream stream = workbookEntry.Open())
            {
                workbook = XDocument.Load(stream);
            }

            List<SheetInfo> sheets = new List<SheetInfo>();

            foreach (XElement sheet in workbook.Descendants(SpreadsheetNamespace + "sheet"))
            {
                string name = (string)sheet.Attribute("name");
                string relationshipId = sheet.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))?.Value;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relationshipId))
                {
                    continue;
                }

                string target = relationships.ContainsKey(relationshipId)
                    ? relationships[relationshipId]
                    : null;

                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                sheets.Add(new SheetInfo
                {
                    Name = name,
                    TargetPath = NormalizeTargetPath(target)
                });
            }

            return sheets;
        }

        private static Dictionary<string, string> LoadRelationships(ZipArchiveEntry relsEntry)
        {
            Dictionary<string, string> relationships = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            XDocument document;
            using (Stream stream = relsEntry.Open())
            {
                document = XDocument.Load(stream);
            }

            foreach (XElement relationship in document.Descendants(RelationshipsNamespace + "Relationship"))
            {
                string id = (string)relationship.Attribute("Id");
                string target = (string)relationship.Attribute("Target");

                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(target))
                {
                    relationships[id] = target;
                }
            }

            return relationships;
        }

        private static List<string> LoadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
            List<string> sharedStrings = new List<string>();

            if (sharedStringsEntry == null)
            {
                return sharedStrings;
            }

            XDocument document;
            using (Stream stream = sharedStringsEntry.Open())
            {
                document = XDocument.Load(stream);
            }

            foreach (XElement item in document.Descendants(SpreadsheetNamespace + "si"))
            {
                sharedStrings.Add(ExtractText(item));
            }

            return sharedStrings;
        }

        private static string ExtractText(XElement item)
        {
            StringBuilder builder = new StringBuilder();

            foreach (XElement text in item.Descendants(SpreadsheetNamespace + "t"))
            {
                builder.Append(text.Value);
            }

            return builder.ToString();
        }

        private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
        {
            string cellType = (string)cell.Attribute("t");
            XElement valueElement = cell.Element(SpreadsheetNamespace + "v");

            if (cellType == "s")
            {
                int index = ParseInt(valueElement != null ? valueElement.Value : null);
                if (index >= 0 && index < sharedStrings.Count)
                {
                    return sharedStrings[index];
                }
            }
            else if (cellType == "inlineStr")
            {
                XElement inlineText = cell.Descendants(SpreadsheetNamespace + "t").FirstOrDefault();
                if (inlineText != null)
                {
                    return inlineText.Value;
                }
            }
            else if (cellType == "b")
            {
                return valueElement != null && valueElement.Value == "1" ? "TRUE" : "FALSE";
            }
            else
            {
                if (valueElement != null)
                {
                    return valueElement.Value;
                }
            }

            return null;
        }

        private static int CellReferenceToColumnIndex(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return -1;
            }

            Match match = Regex.Match(reference, "^[A-Z]+", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return -1;
            }

            string letters = match.Value.ToUpperInvariant();
            int index = 0;

            foreach (char letter in letters)
            {
                index = (index * 26) + (letter - 'A' + 1);
            }

            return index - 1;
        }

        private static int ParseInt(string value)
        {
            int parsed;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return -1;
        }

        private static string PickValue(IReadOnlyDictionary<string, string> columns, IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                string normalized = NormalizeHeader(name);
                foreach (KeyValuePair<string, string> pair in columns)
                {
                    if (NormalizeHeader(pair.Key).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(pair.Value))
                        {
                            return pair.Value.Trim();
                        }
                    }
                }
            }

            return null;
        }

        private static string NormalizeHeader(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim();
            normalized = Regex.Replace(normalized, @"\s+", " ");
            normalized = normalized.Trim(':', '-', '_');
            return normalized;
        }

        private static bool ContainsAny(string value, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (string keyword in keywords)
            {
                if (value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeTargetPath(string target)
        {
            string normalized = target.Replace('\\', '/').TrimStart('/');

            if (normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return "xl/" + normalized;
        }

        private class SheetInfo
        {
            public string Name { get; set; }
            public string TargetPath { get; set; }
        }
    }
}
