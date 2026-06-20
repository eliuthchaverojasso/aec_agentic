using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public sealed class RagQueryResult
    {
        public bool Success { get; set; }
        public string Answer { get; set; }
        public string Query { get; set; }
        public List<string> SourceRows { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
        public bool UsedFallback { get; set; }
    }

    public sealed class ReportContextSnapshot
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string RequirementsFileName { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string GeneratedAt { get; set; } = string.Empty;
        public int RequirementCount { get; set; }
        public int ModelElementCount { get; set; }
        public int MetCount { get; set; }
        public int NotMetCount { get; set; }
        public int NeedsHumanReviewCount { get; set; }
        public int InsufficientModelDataCount { get; set; }
        public int NotApplicableCount { get; set; }
        public int KeyIssueCount { get; set; }
    }

    public sealed class ReportRagService
    {
        private readonly List<JsonElement> _requirements = new List<JsonElement>();
        private readonly List<JsonElement> _keyIssues = new List<JsonElement>();
        private readonly Dictionary<int, JsonElement> _requirementsByRow = new Dictionary<int, JsonElement>();
        private readonly Dictionary<string, List<JsonElement>> _byStatus = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<JsonElement>> _byDiscipline = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<JsonElement>> _byType = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
        private ReportContextSnapshot _snapshot = new ReportContextSnapshot();
        private string _lastSourcePath = string.Empty;
        private string _lastErrorMessage = string.Empty;
        private bool _parseFailed;
        private bool _loaded;

        public ReportRagService() { }

        public ReportDataState DataState { get; private set; } = ReportDataState.NoReportData;
        public bool HasReportData => DataState == ReportDataState.ReportDataLoaded;
        public bool HasParseFailed => DataState == ReportDataState.ReportDataParseFailed;
        public string LastSourcePath => _lastSourcePath;
        public string LastErrorMessage => _lastErrorMessage;
        public ReportContextSnapshot Snapshot => _snapshot;
        public int RequirementCount => _snapshot.RequirementCount;
        public int ModelElementCount => _snapshot.ModelElementCount;
        public int MetCount => _snapshot.MetCount;
        public int NotMetCount => _snapshot.NotMetCount;
        public int NeedsHumanReviewCount => _snapshot.NeedsHumanReviewCount;
        public int InsufficientModelDataCount => _snapshot.InsufficientModelDataCount;
        public int NotApplicableCount => _snapshot.NotApplicableCount;
        public int KeyIssueCount => _snapshot.KeyIssueCount;

        public bool LoadFromHtmlFile(string htmlPath)
        {
            if (string.IsNullOrWhiteSpace(htmlPath) || !File.Exists(htmlPath))
            {
                ClearLoadedData(reportDataState: ReportDataState.NoReportData);
                return false;
            }

            try
            {
                _lastSourcePath = Path.GetFullPath(htmlPath);
                DataState = ReportDataState.ReportDataLoading;
                string html = File.ReadAllText(htmlPath);
                return LoadFromHtml(html);
            }
            catch
            {
                ClearLoadedData(reportDataState: ReportDataState.ReportDataParseFailed, errorMessage: "The report file could not be read.");
                return false;
            }
        }

        public bool LoadFromHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                ClearLoadedData(reportDataState: ReportDataState.ReportDataParseFailed, errorMessage: "The report file was empty.");
                return false;
            }

            try
            {
                string json = ExtractHiddenJson(html);
                if (string.IsNullOrWhiteSpace(json))
                {
                    ClearLoadedData(reportDataState: ReportDataState.ReportDataParseFailed, errorMessage: "Embedded report data was not found.");
                    return false;
                }

                return LoadFromJson(json);
            }
            catch
            {
                ClearLoadedData(reportDataState: ReportDataState.ReportDataParseFailed, errorMessage: "Embedded report data could not be parsed.");
                return false;
            }
        }

        public bool LoadFromJson(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;

                    _snapshot = new ReportContextSnapshot
                    {
                        ProjectName = GetNestedString(root, "report_metadata", "project_name"),
                        ModelName = GetNestedString(root, "report_metadata", "model_name"),
                        RequirementsFileName = GetNestedString(root, "report_metadata", "requirements_file"),
                        Scope = GetNestedString(root, "report_metadata", "scope"),
                        GeneratedAt = GetNestedString(root, "report_metadata", "generated_at"),
                        RequirementCount = GetNestedInt(root, "report_metadata", "total_requirements"),
                        ModelElementCount = GetNestedInt(root, "report_metadata", "model_elements_reviewed"),
                        MetCount = GetNestedInt(root, "summary_counts", "met"),
                        NotMetCount = GetNestedInt(root, "summary_counts", "not_met"),
                        NeedsHumanReviewCount = GetNestedInt(root, "summary_counts", "needs_human_review"),
                        InsufficientModelDataCount = GetNestedInt(root, "summary_counts", "insufficient_model_data"),
                        NotApplicableCount = GetNestedInt(root, "summary_counts", "not_applicable")
                    };

                    List<JsonElement> reqs = new List<JsonElement>();
                    List<JsonElement> issues = new List<JsonElement>();

                    // Support both hidden-JSON snake_case ("requirement_results") and test camelCase ("requirements")
                    JsonElement reqsEl;
                    bool hasReqs = root.TryGetProperty("requirement_results", out reqsEl) ||
                                   root.TryGetProperty("requirements", out reqsEl);
                    if (hasReqs && reqsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement el in reqsEl.EnumerateArray())
                        {
                            reqs.Add(el.Clone());
                        }
                    }

                    // Support both "key_issues" (hidden JSON) and "keyIssues" (test format)
                    JsonElement issuesEl;
                    bool hasIssues = root.TryGetProperty("key_issues", out issuesEl) ||
                                     root.TryGetProperty("keyIssues", out issuesEl);
                    if (hasIssues && issuesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement el in issuesEl.EnumerateArray())
                        {
                            issues.Add(el.Clone());
                        }
                    }

                    _requirements.Clear();
                    _requirements.AddRange(reqs);

                    _keyIssues.Clear();
                    _keyIssues.AddRange(issues);

                    _requirementsByRow.Clear();
                    foreach (JsonElement requirement in _requirements)
                    {
                        int row = GetRequirementRow(requirement);
                        if (row > 0 && !_requirementsByRow.ContainsKey(row))
                        {
                            _requirementsByRow[row] = requirement;
                        }
                    }

                    _snapshot.RequirementCount = _snapshot.RequirementCount > 0 ? _snapshot.RequirementCount : _requirements.Count;
                    _snapshot.KeyIssueCount = _keyIssues.Count;
                    if (_snapshot.ModelElementCount <= 0)
                    {
                        _snapshot.ModelElementCount = GetNestedInt(root, "report_metadata", "model_element_count");
                    }

                    BuildIndexes();
                    _loaded = true;
                    _parseFailed = false;
                    _lastErrorMessage = string.Empty;
                    DataState = ReportDataState.ReportDataLoaded;
                    return true;
                }
            }
            catch
            {
                ClearLoadedData(reportDataState: ReportDataState.ReportDataParseFailed, errorMessage: "Embedded report data could not be parsed.");
                return false;
            }
        }

        public RagQueryResult Query(string userQuery)
        {
            if (!_loaded)
            {
                if (DataState == ReportDataState.ReportDataParseFailed)
                {
                    return new RagQueryResult
                    {
                        Success = false,
                        Query = userQuery,
                        ErrorMessage = _lastErrorMessage,
                        UsedFallback = true,
                        Answer = "Report file found, but embedded report data could not be parsed."
                    };
                }

                return new RagQueryResult
                {
                    Success = false,
                    Query = userQuery,
                    ErrorMessage = "Report data not loaded.",
                    UsedFallback = true,
                    Answer = "No report data is loaded. Open a report first."
                };
            }

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                return new RagQueryResult
                {
                    Success = false,
                    Query = string.Empty,
                    Answer = "Please enter a question."
                };
            }

            string lower = userQuery.ToLowerInvariant();

            // Summary questions
            if (lower.Contains("summary") || lower.Contains("overview") || lower.Contains("how many"))
            {
                return AnswerSummary(userQuery);
            }

            // Not met questions
            if ((lower.Contains("not met") || lower.Contains("failed")) && !lower.Contains("why"))
            {
                return AnswerByStatus("NotMet", userQuery);
            }

            if (lower.Contains("what is not met"))
            {
                return AnswerByStatus("NotMet", userQuery);
            }

            // Needs review questions
            if (lower.Contains("needs review") || lower.Contains("need review") || lower.Contains("human review"))
            {
                return AnswerByStatus("NeedsHumanReview", userQuery);
            }

            // Discipline questions
            string discipline = ExtractDiscipline(lower);
            if (!string.IsNullOrEmpty(discipline))
            {
                return AnswerByDiscipline(discipline, userQuery);
            }

            // Key issues
            if (lower.Contains("key issue") || lower.Contains("critical") || lower.Contains("top issue") || lower.Contains("high priority"))
            {
                return AnswerKeyIssues(userQuery);
            }

            // Row-specific questions
            Match rowMatch = Regex.Match(lower, @"row\s+(\d+)", RegexOptions.IgnoreCase);
            if (rowMatch.Success && int.TryParse(rowMatch.Groups[1].Value, out int rowNum))
            {
                return AnswerForRow(rowNum, userQuery);
            }

            // Requirement type questions
            string reqType = ExtractRequirementType(lower);
            if (!string.IsNullOrEmpty(reqType))
            {
                return AnswerByType(reqType, userQuery);
            }

            return AnswerGeneral(userQuery);
        }

        private RagQueryResult AnswerSummary(string query)
        {
            int total = _snapshot.RequirementCount > 0 ? _snapshot.RequirementCount : _requirements.Count;
            int met = _snapshot.MetCount > 0 ? _snapshot.MetCount : (_byStatus.ContainsKey("Met") ? _byStatus["Met"].Count : 0);
            int notMet = _snapshot.NotMetCount > 0 ? _snapshot.NotMetCount : (_byStatus.ContainsKey("NotMet") ? _byStatus["NotMet"].Count : 0);
            int review = _snapshot.NeedsHumanReviewCount > 0 ? _snapshot.NeedsHumanReviewCount : (_byStatus.ContainsKey("NeedsHumanReview") ? _byStatus["NeedsHumanReview"].Count : 0);
            int insufficient = _snapshot.InsufficientModelDataCount > 0 ? _snapshot.InsufficientModelDataCount : (_byStatus.ContainsKey("InsufficientModelData") ? _byStatus["InsufficientModelData"].Count : 0);
            int notApplicable = _snapshot.NotApplicableCount > 0 ? _snapshot.NotApplicableCount : (_byStatus.ContainsKey("NotApplicable") ? _byStatus["NotApplicable"].Count : 0);
            int keyIssues = _snapshot.KeyIssueCount > 0 ? _snapshot.KeyIssueCount : _keyIssues.Count;

            var lines = new List<string>
            {
                $"Answer: This report covers {total} owner requirements.",
                $"Evidence Used: {met} Met, {notMet} Not Met, {review} Need Human Review, {insufficient} Insufficient Model Data, {notApplicable} Not Applicable.",
                $"Missing Evidence: The report contains {keyIssues} flagged key issue(s) that should be reviewed against the loaded report rows."
            };

            List<string> topRefs = _requirements
                .Where(item => string.Equals(GetString(item, "status"), "NotMet", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(GetString(item, "status"), "NeedsHumanReview", StringComparison.OrdinalIgnoreCase))
                .Take(6)
                .Select(FormatRowReference)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            if (topRefs.Count > 0)
            {
                lines.Add("References:");
                lines.AddRange(topRefs.Select(item => "  - " + item));
            }

            lines.Add("Recommended Actions: Use the Requirements Schedule tab to filter by status, discipline, or requirement type.");
            lines.Add("Model Used / Fallback Used: Deterministic report summary generated without an AI model.");

            string answer = string.Join(Environment.NewLine, lines);

            return new RagQueryResult
            {
                Success = true,
                Query = query,
                Answer = answer
            };
        }

        private RagQueryResult AnswerByStatus(string status, string query)
        {
            if (!_byStatus.ContainsKey(status) || _byStatus[status].Count == 0)
            {
                return new RagQueryResult
                {
                    Success = true,
                    Query = query,
                    Answer = $"No requirements with status '{status}' were found in this report."
                };
            }

            List<JsonElement> items = _byStatus[status]
                .OrderBy(item => GetRequirementRow(item))
                .Take(8)
                .ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"Answer: There are {_byStatus[status].Count} requirements with status {StatusDisplayName(status)}.");
            sb.AppendLine("Evidence Used:");

            foreach (JsonElement item in items)
            {
                sb.AppendLine("  - " + FormatRowReference(item));
                string evidence = GetListSummary(item, "missing_direct_evidence");
                if (string.IsNullOrWhiteSpace(evidence))
                {
                    evidence = GetListSummary(item, "direct_closing_evidence");
                }

                string nextAction = GetString(item, "nextBestAction") ?? GetString(item, "NextBestAction") ?? string.Empty;
                string whyNotModelCloseable = GetString(item, "why_not_model_closeable") ?? GetString(item, "whyNotModelCloseable") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(evidence))
                {
                    sb.AppendLine("    Missing Evidence: " + evidence);
                }

                if (!string.IsNullOrWhiteSpace(nextAction))
                {
                    sb.AppendLine("    Recommended Actions: " + nextAction);
                }

                if (!string.IsNullOrWhiteSpace(whyNotModelCloseable))
                {
                    sb.AppendLine("    Model Evidence Note: " + whyNotModelCloseable);
                }
            }

            sb.AppendLine("References:");
            foreach (JsonElement item in items)
            {
                sb.AppendLine("  - " + FormatRowReference(item));
            }
            sb.AppendLine("Model Used / Fallback Used: Deterministic report summary generated without an AI model.");

            return new RagQueryResult
            {
                Success = true,
                Query = query,
                Answer = sb.ToString().TrimEnd(' ', ';'),
                SourceRows = items.Select(i => GetString(i, "requirementId") ?? "?").ToList()
            };
        }

        private RagQueryResult AnswerByDiscipline(string discipline, string query)
        {
            string key = _byDiscipline.Keys.FirstOrDefault(k =>
                k.IndexOf(discipline, StringComparison.OrdinalIgnoreCase) >= 0);

            if (key == null || _byDiscipline[key].Count == 0)
            {
                return new RagQueryResult
                {
                    Success = true,
                    Query = query,
                    Answer = $"No requirements found for discipline '{discipline}' in this report."
                };
            }

            List<JsonElement> items = _byDiscipline[key];
            int notMet = items.Count(i => string.Equals(GetString(i, "status"), "NotMet", StringComparison.OrdinalIgnoreCase));
            int review = items.Count(i => string.Equals(GetString(i, "status"), "NeedsHumanReview", StringComparison.OrdinalIgnoreCase));
            int met = items.Count(i => string.Equals(GetString(i, "status"), "Met", StringComparison.OrdinalIgnoreCase));
            int insufficient = items.Count(i => string.Equals(GetString(i, "status"), "InsufficientModelData", StringComparison.OrdinalIgnoreCase));

            var sb = new StringBuilder();
            sb.AppendLine($"Answer: The {key} discipline has {items.Count} requirements.");
            sb.AppendLine($"Evidence Used: {notMet} Not Met, {review} Need Human Review, {met} Met, {insufficient} Insufficient Model Data.");
            sb.AppendLine("Top Rows:");
            foreach (JsonElement item in items
                .Where(item => !string.Equals(GetString(item, "status"), "Met", StringComparison.OrdinalIgnoreCase))
                .Take(8))
            {
                sb.AppendLine("  - " + FormatRowReference(item));
                string nextAction = GetString(item, "nextBestAction") ?? GetString(item, "NextBestAction") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(nextAction))
                {
                    sb.AppendLine("    Recommended Actions: " + nextAction);
                }
            }

            sb.AppendLine("References:");
            foreach (JsonElement item in items.Take(8))
            {
                sb.AppendLine("  - " + FormatRowReference(item));
            }
            sb.AppendLine("Model Used / Fallback Used: Deterministic report summary generated without an AI model.");

            return new RagQueryResult
            {
                Success = true,
                Query = query,
                Answer = sb.ToString(),
                SourceRows = items.Take(8).Select(item => GetRequirementRow(item).ToString(CultureInfo.InvariantCulture)).Where(item => item != "0").ToList()
            };
        }

        private RagQueryResult AnswerKeyIssues(string query)
        {
            if (_keyIssues.Count == 0)
            {
                return new RagQueryResult
                {
                    Success = true,
                    Query = query,
                    Answer = "No key issues were flagged in this report."
                };
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Answer: There are {_keyIssues.Count} key issues in this report.");
            sb.AppendLine("Evidence Used:");
            foreach (JsonElement issue in _keyIssues.Take(5))
            {
                string title = GetString(issue, "issue_title") ?? GetString(issue, "issueTitle") ?? GetString(issue, "requirementText") ?? "(unknown)";
                string severity = GetString(issue, "urgency") ?? GetString(issue, "severity") ?? "?";
                string row = GetString(issue, "source_row") ?? GetString(issue, "sourceRow") ?? "?";
                sb.AppendLine($"  - Row {row} [{severity}] {title}");
                string action = GetString(issue, "next_best_action") ?? GetString(issue, "nextBestAction") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(action))
                {
                    sb.AppendLine("    Recommended Actions: " + action);
                }
            }

            sb.AppendLine("References:");
            foreach (JsonElement issue in _keyIssues.Take(5))
            {
                string row = GetString(issue, "source_row") ?? GetString(issue, "sourceRow") ?? "?";
                string severity = GetString(issue, "urgency") ?? GetString(issue, "severity") ?? "?";
                sb.AppendLine($"  - Row {row} [{severity}]");
            }
            sb.AppendLine("Model Used / Fallback Used: Deterministic report summary generated without an AI model.");

            return new RagQueryResult
            {
                Success = true,
                Query = query,
                Answer = sb.ToString().TrimEnd()
            };
        }

        private RagQueryResult AnswerForRow(int rowNum, string query)
        {
            JsonElement match;
            if (!_requirementsByRow.TryGetValue(rowNum, out match))
            {
                JsonElement? fallback = _requirements.Cast<JsonElement?>().FirstOrDefault(el => GetRequirementRow(el ?? default(JsonElement)) == rowNum);
                if (fallback.HasValue)
                {
                    match = fallback.Value;
                }
                else
                {
                    return new RagQueryResult
                    {
                        Success = true,
                        Query = query,
                        Answer = $"Row {rowNum} was not found in this report."
                    };
                }
            }

            string text = GetString(match, "requirementText") ?? GetString(match, "requirement_text") ?? "(no text)";
            string status = GetString(match, "status") ?? "?";
            string type = GetString(match, "requirementType") ?? GetString(match, "requirement_type") ?? "unknown";
            string reasoning = GetString(match, "reasoning") ?? GetString(match, "Reasoning") ?? "No reasoning available.";
            string evidenceSummary = GetString(match, "evidenceSummary") ?? GetString(match, "evidence_summary") ?? "Not captured in the current export.";
            string nextAction = GetString(match, "nextBestAction") ?? GetString(match, "next_best_action") ?? "Review the requirement manually.";
            string whyNotModelCloseable = GetString(match, "why_not_model_closeable") ?? GetString(match, "whyNotModelCloseable") ?? string.Empty;
            string modelEvidenceSufficiency = GetString(match, "model_evidence_sufficiency") ?? GetString(match, "modelEvidenceSufficiency") ?? string.Empty;
            List<string> directEvidence = GetList(match, "direct_closing_evidence");
            List<string> supportingContext = GetList(match, "supporting_context");
            List<string> missingEvidence = GetList(match, "missing_direct_evidence");
            List<string> missingExpectedParameters = GetList(match, "missing_expected_parameters");

            if (reasoning.Length > 240) reasoning = reasoning.Substring(0, 240) + "...";

            string discipline = GetString(match, "discipline") ?? GetString(match, "Discipline") ?? "Unknown";

            var lines = new List<string>
            {
                $"Answer: Row {rowNum} is {status} and classified as {type}.",
                $"Requirement Text: {text}",
                $"Evidence Used: {evidenceSummary}",
                $"Missing Evidence: {(missingEvidence.Count > 0 ? string.Join(", ", missingEvidence) : missingExpectedParameters.Count > 0 ? string.Join(", ", missingExpectedParameters) : "Not captured in the current export.")}",
                $"Recommended Actions: {nextAction}",
                $"References: Row {rowNum} | {discipline} | {type}",
                $"Model Used / Fallback Used: Deterministic report summary generated without an AI model."
            };

            if (directEvidence.Count > 0)
            {
                lines.Insert(2, "Direct Closing Evidence: " + string.Join(", ", directEvidence));
            }

            if (supportingContext.Count > 0)
            {
                lines.Insert(3, "Supporting Context: " + string.Join(", ", supportingContext));
            }

            if (!string.IsNullOrWhiteSpace(whyNotModelCloseable))
            {
                lines.Add("Model Evidence Note: " + whyNotModelCloseable);
            }

            if (!string.IsNullOrWhiteSpace(modelEvidenceSufficiency))
            {
                lines.Add("Model Evidence Sufficiency: " + modelEvidenceSufficiency);
            }

            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                lines.Add("Reasoning: " + reasoning);
            }

            return new RagQueryResult
            {
                Success = true,
                Query = query,
                Answer = string.Join(Environment.NewLine, lines),
                SourceRows = new List<string> { rowNum.ToString(CultureInfo.InvariantCulture) }
            };
        }

        private RagQueryResult AnswerByType(string reqType, string query)
        {
            string key = _byType.Keys.FirstOrDefault(k =>
                k.IndexOf(reqType, StringComparison.OrdinalIgnoreCase) >= 0);

            if (key == null || _byType[key].Count == 0)
            {
                return new RagQueryResult
                {
                    Success = true,
                    Query = query,
                    Answer = $"No requirements of type '{reqType}' found in this report."
                };
            }

            List<JsonElement> items = _byType[key];
            int notMet = items.Count(i => string.Equals(GetString(i, "status"), "NotMet", StringComparison.OrdinalIgnoreCase));
            var sb = new StringBuilder();
            sb.AppendLine($"Answer: There are {items.Count} requirements of type '{key}'.");
            sb.AppendLine($"Evidence Used: {notMet} are Not Met.");
            sb.AppendLine("Top Rows:");
            foreach (JsonElement item in items.Take(8))
            {
                sb.AppendLine("  - " + FormatRowReference(item));
            }
            sb.AppendLine("References:");
            foreach (JsonElement item in items.Take(8))
            {
                sb.AppendLine("  - " + FormatRowReference(item));
            }
            sb.AppendLine("Model Used / Fallback Used: Deterministic report summary generated without an AI model.");

            return new RagQueryResult
            {
                Success = true,
                Query = query,
                Answer = sb.ToString(),
                SourceRows = items.Take(8).Select(item => GetRequirementRow(item).ToString(CultureInfo.InvariantCulture)).Where(item => item != "0").ToList()
            };
        }

        private RagQueryResult AnswerGeneral(string query)
        {
            return new RagQueryResult
            {
                Success = true,
                Query = query,
                UsedFallback = true,
                Answer = "Answer: This report contains " + (_snapshot.RequirementCount > 0 ? _snapshot.RequirementCount : _requirements.Count) + " requirements and " + (_snapshot.KeyIssueCount > 0 ? _snapshot.KeyIssueCount : _keyIssues.Count) + " key issues." + Environment.NewLine +
                         "Evidence Used: You can ask about summary statistics, Not Met requirements, requirements by discipline, key issues, or a specific row number." + Environment.NewLine +
                         "Recommended Actions: Try questions like 'What is Not Met?', 'Tell me about row 606', or 'Plumbing requirements'." + Environment.NewLine +
                         "Model Used / Fallback Used: Deterministic report summary generated without an AI model."
            };
        }

        private void BuildIndexes()
        {
            _byStatus.Clear();
            _byDiscipline.Clear();
            _byType.Clear();
            _requirementsByRow.Clear();

            foreach (JsonElement req in _requirements)
            {
                IndexBy(_byStatus, req, "status");
                IndexBy(_byDiscipline, req, "discipline");
                IndexBy(_byType, req, "requirementType");
                int row = GetRequirementRow(req);
                if (row > 0 && !_requirementsByRow.ContainsKey(row))
                {
                    _requirementsByRow[row] = req;
                }
            }
        }

        private static void IndexBy(Dictionary<string, List<JsonElement>> index, JsonElement el, string key)
        {
            string value = GetString(el, key);
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!index.ContainsKey(value)) index[value] = new List<JsonElement>();
            index[value].Add(el);
        }

        private static string ExtractDiscipline(string lower)
        {
            string[] disciplines = { "electrical", "lighting", "mechanical", "plumbing", "technology" };
            return disciplines.FirstOrDefault(d => lower.Contains(d));
        }

        private static string ExtractRequirementType(string lower)
        {
            string[] types = {
                "grounding", "conduit", "plumbing", "flush valve", "water hammer", "hose bibb",
                "manufacturer", "labeling", "identification", "panel", "circuit", "commissioning",
                "demolition", "level_location", "unknown_ambiguous"
            };
            return types.FirstOrDefault(t => lower.Contains(t));
        }

        private static string ExtractHiddenJson(string html)
        {
            Match match = Regex.Match(
                html,
                @"<script[^>]+id=""ema-ai-report-context""[^>]*>([\s\S]*?)</script>",
                RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        public string BuildReportDataStatusMessage()
        {
            if (DataState == ReportDataState.ReportDataParseFailed)
            {
                return "Report visual file found, but embedded report data could not be parsed.";
            }

            if (!HasReportData)
            {
                return "No report data loaded. Use Reload Latest or Browse to select an EMA AI report.";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Report data loaded: {0} requirements | {1} elements | latest report.",
                RequirementCount > 0 ? RequirementCount : _requirements.Count,
                ModelElementCount);
        }

        public string BuildClipboardSummary()
        {
            if (DataState == ReportDataState.ReportDataParseFailed)
            {
                return string.IsNullOrWhiteSpace(_lastErrorMessage)
                    ? "Report visual file found, but embedded report data could not be parsed."
                    : "Report visual file found, but embedded report data could not be parsed." + Environment.NewLine + _lastErrorMessage;
            }

            if (!HasReportData)
            {
                return "No report data loaded.";
            }

            var lines = new List<string>
            {
                "EMA AI Owner Requirements Report Summary",
                "Project: " + SafeSummaryValue(_snapshot.ProjectName),
                "Model: " + SafeSummaryValue(_snapshot.ModelName),
                "Scope: " + SafeSummaryValue(_snapshot.Scope),
                "Requirements: " + (RequirementCount > 0 ? RequirementCount : _requirements.Count),
                "Elements: " + ModelElementCount,
                "Met: " + MetCount,
                "Not Met: " + NotMetCount,
                "Needs Human Review: " + NeedsHumanReviewCount,
                "Insufficient Model Data: " + InsufficientModelDataCount,
                "Not Applicable: " + NotApplicableCount
            };

            List<string> keyIssueRefs = _keyIssues
                .Take(5)
                .Select(issue =>
                {
                    string row = GetString(issue, "source_row") ?? GetString(issue, "sourceRow") ?? string.Empty;
                    string title = GetString(issue, "issue_title") ?? GetString(issue, "issueTitle") ?? GetString(issue, "requirementText") ?? string.Empty;
                    return string.IsNullOrWhiteSpace(row)
                        ? title
                        : "Row " + row + ": " + title;
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (keyIssueRefs.Count > 0)
            {
                lines.Add("Top Key Issues:");
                lines.AddRange(keyIssueRefs.Select(item => "- " + item));
            }

            lines.Add("This report is a first-pass evidence review. It does not certify compliance.");
            return string.Join(Environment.NewLine, lines);
        }

        private static string SafeSummaryValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(not set)" : value;
        }

        private void ClearLoadedData(ReportDataState reportDataState, string errorMessage = null)
        {
            _requirements.Clear();
            _keyIssues.Clear();
            _requirementsByRow.Clear();
            _byStatus.Clear();
            _byDiscipline.Clear();
            _byType.Clear();
            _loaded = false;
            _parseFailed = reportDataState == ReportDataState.ReportDataParseFailed;
            _lastErrorMessage = errorMessage ?? string.Empty;
            DataState = reportDataState;
            _snapshot = new ReportContextSnapshot();
        }

        private static int GetRequirementRow(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            if (el.TryGetProperty("source_row", out JsonElement rowEl))
            {
                if (rowEl.ValueKind == JsonValueKind.Number && rowEl.TryGetInt32(out int n))
                {
                    return n;
                }

                if (rowEl.ValueKind == JsonValueKind.String && int.TryParse(rowEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    return parsed;
                }
            }

            string requirementId = GetString(el, "requirementId") ?? GetString(el, "RequirementId") ?? string.Empty;
            if (int.TryParse(requirementId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requirementRow))
            {
                return requirementRow;
            }

            return 0;
        }

        private static string FormatRowReference(JsonElement item)
        {
            int row = GetRequirementRow(item);
            string discipline = GetString(item, "discipline") ?? GetString(item, "Discipline") ?? "Unknown";
            string status = StatusDisplayName(GetString(item, "status") ?? string.Empty);
            string type = GetString(item, "requirementType") ?? GetString(item, "requirement_type") ?? "unknown";
            if (row <= 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} | {1} | {2}", discipline, status, type);
            }

            return string.Format(CultureInfo.InvariantCulture, "Row {0} | {1} | {2} | {3}", row, discipline, status, type);
        }

        private static string StatusDisplayName(string status)
        {
            if (string.Equals(status, "Met", StringComparison.OrdinalIgnoreCase)) return "Met";
            if (string.Equals(status, "NotMet", StringComparison.OrdinalIgnoreCase)) return "Not Met";
            if (string.Equals(status, "NeedsHumanReview", StringComparison.OrdinalIgnoreCase)) return "Needs Human Review";
            if (string.Equals(status, "InsufficientModelData", StringComparison.OrdinalIgnoreCase)) return "Insufficient Model Data";
            if (string.Equals(status, "NotApplicable", StringComparison.OrdinalIgnoreCase)) return "Not Applicable";
            return string.IsNullOrWhiteSpace(status) ? "Unknown" : status;
        }

        private static string GetNestedString(JsonElement root, string parentKey, string childKey)
        {
            try
            {
                if (root.TryGetProperty(parentKey, out JsonElement parent) &&
                    parent.ValueKind == JsonValueKind.Object &&
                    parent.TryGetProperty(childKey, out JsonElement child) &&
                    child.ValueKind == JsonValueKind.String)
                {
                    return child.GetString();
                }
            }
            catch { }

            return string.Empty;
        }

        private static int GetNestedInt(JsonElement root, string parentKey, string childKey)
        {
            try
            {
                if (root.TryGetProperty(parentKey, out JsonElement parent) &&
                    parent.ValueKind == JsonValueKind.Object &&
                    parent.TryGetProperty(childKey, out JsonElement child))
                {
                    if (child.ValueKind == JsonValueKind.Number && child.TryGetInt32(out int value))
                    {
                        return value;
                    }

                    if (child.ValueKind == JsonValueKind.String && int.TryParse(child.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        return parsed;
                    }
                }
            }
            catch { }

            return 0;
        }

        private static List<string> GetList(JsonElement el, string key)
        {
            List<string> values = new List<string>();
            try
            {
                if (!el.TryGetProperty(key, out JsonElement prop))
                {
                    return values;
                }

                if (prop.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in prop.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            string value = item.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                values.Add(value);
                            }
                        }
                    }
                }
                else if (prop.ValueKind == JsonValueKind.String)
                {
                    string value = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }
            catch { }

            return values;
        }

        private static string GetListSummary(JsonElement el, string key)
        {
            List<string> values = GetList(el, key);
            return values.Count == 0 ? string.Empty : string.Join(", ", values.Take(4));
        }

        private static string GetString(JsonElement el, string key)
        {
            // Try the key as-is (camelCase from tests, snake_case from actual hidden JSON)
            if (el.TryGetProperty(key, out JsonElement val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();

            // Try PascalCase
            if (key.Length > 0)
            {
                string pascal = char.ToUpperInvariant(key[0]) + key.Substring(1);
                if (el.TryGetProperty(pascal, out JsonElement val2) && val2.ValueKind == JsonValueKind.String)
                    return val2.GetString();
            }

            // Try converting camelCase to snake_case for hidden-JSON compatibility
            string snake = CamelToSnake(key);
            if (snake != key && el.TryGetProperty(snake, out JsonElement val3) && val3.ValueKind == JsonValueKind.String)
                return val3.GetString();

            // Specific alias: requirementId → source_row (hidden JSON uses source_row for the row number)
            if (string.Equals(key, "requirementId", StringComparison.Ordinal) ||
                string.Equals(key, "RequirementId", StringComparison.Ordinal))
            {
                if (el.TryGetProperty("source_row", out JsonElement sr))
                    return sr.ValueKind == JsonValueKind.String ? sr.GetString() :
                           sr.ValueKind == JsonValueKind.Number ? sr.GetInt32().ToString() : null;
            }

            return null;
        }

        private static string CamelToSnake(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c) && i > 0)
                {
                    sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }
    }
}
