using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EMAExtractor.Models;

namespace EMAExtractor.Services
{
    public static class AskChatHistoryService
    {
        private const int DefaultRecentLimit = 8;
        private const int MaxSessionsPerReport = 16;
        private const int MaxMessagesPerSession = 24;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private sealed class ChatStore
        {
            public List<AskChatSession> Sessions { get; set; } = new List<AskChatSession>();
        }

        public static string StoragePath => Path.Combine(LoggingService.AppRoot, "ask_chat_history.json");

        public static List<AskChatSession> LoadSessions()
        {
            return LoadSessionsFromPath(StoragePath);
        }

        public static List<AskChatSession> LoadSessionsFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new List<AskChatSession>();
            }

            try
            {
                string json = File.ReadAllText(path);
                ChatStore store = JsonSerializer.Deserialize<ChatStore>(json, Options);
                return NormalizeSessions(store?.Sessions);
            }
            catch
            {
                return new List<AskChatSession>();
            }
        }

        public static void SaveSession(AskChatSession session)
        {
            SaveSession(session, StoragePath);
        }

        public static void SaveSession(AskChatSession session, string path)
        {
            if (session == null)
            {
                return;
            }

            List<AskChatSession> sessions = LoadSessionsFromPath(path);
            sessions.RemoveAll(item => item != null && string.Equals(item.SessionId, session.SessionId, StringComparison.OrdinalIgnoreCase));
            sessions.Add(CloneSession(session));

            sessions = sessions
                .Where(item => item != null)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ThenByDescending(item => item.CreatedAtUtc)
                .Take(MaxSessionsPerReport)
                .ToList();

            SaveSessionsToPath(sessions, path);
        }

        public static List<AskChatSession> GetRecentSessionsForReport(string reportPath, int maxCount = DefaultRecentLimit, string activeSessionId = null, string storagePath = null)
        {
            string normalizedReportPath = NormalizePath(reportPath);
            if (string.IsNullOrWhiteSpace(normalizedReportPath))
            {
                return new List<AskChatSession>();
            }

            List<AskChatSession> sessions = LoadSessionsFromPath(string.IsNullOrWhiteSpace(storagePath) ? StoragePath : storagePath)
                .Where(session => session != null && SamePath(session.ReportPath, normalizedReportPath))
                .Where(session =>
                    (session.Messages != null && session.Messages.Count > 0) ||
                    string.Equals(session.SessionId, activeSessionId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(session => session.UpdatedAtUtc)
                .ThenByDescending(session => session.CreatedAtUtc)
                .Take(Math.Max(1, maxCount))
                .Select(CloneSession)
                .ToList();

            return sessions;
        }

        public static AskChatSession CreateSession(string reportPath, string reportName = null, string title = null, string contextScope = null)
        {
            return new AskChatSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                ReportPath = NormalizePath(reportPath),
                ReportName = reportName ?? string.Empty,
                Title = string.IsNullOrWhiteSpace(title) ? "New chat" : title.Trim(),
                ContextScope = string.IsNullOrWhiteSpace(contextScope) ? "Summary" : contextScope.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Messages = new List<AskChatMessage>()
            };
        }

        public static AskChatMessage AppendMessage(AskChatSession session, string role, string content)
        {
            if (session == null)
            {
                return null;
            }

            session.Messages ??= new List<AskChatMessage>();
            var message = new AskChatMessage
            {
                Role = string.IsNullOrWhiteSpace(role) ? "user" : role.Trim(),
                Content = content ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            };

            session.Messages.Add(message);
            TrimSessionMessages(session);
            session.UpdatedAtUtc = DateTime.UtcNow;
            return message;
        }

        public static void EnsureSessionTitle(AskChatSession session, string question)
        {
            if (session == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(session.Title) && !string.Equals(session.Title, "New chat", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            session.Title = BuildTitle(question);
        }

        public static string BuildTitle(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return "New chat";
            }

            string text = question.Trim();
            if (text.Length <= 42)
            {
                return text;
            }

            return text.Substring(0, 39).TrimEnd() + "...";
        }

        private static void SaveSessionsToPath(List<AskChatSession> sessions, string path)
        {
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(path, JsonSerializer.Serialize(new ChatStore { Sessions = sessions ?? new List<AskChatSession>() }, Options));
            }
            catch
            {
                // Chat history is helpful, but it must never break Revit workflows.
            }
        }

        private static List<AskChatSession> NormalizeSessions(IEnumerable<AskChatSession> sessions)
        {
            List<AskChatSession> list = (sessions ?? Enumerable.Empty<AskChatSession>())
                .Where(item => item != null)
                .Select(CloneSession)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ThenByDescending(item => item.CreatedAtUtc)
                .ToList();

            foreach (AskChatSession session in list)
            {
                NormalizeSession(session);
            }

            return list;
        }

        private static AskChatSession CloneSession(AskChatSession session)
        {
            if (session == null)
            {
                return null;
            }

            return new AskChatSession
            {
                SessionId = string.IsNullOrWhiteSpace(session.SessionId) ? Guid.NewGuid().ToString("N") : session.SessionId,
                ReportPath = NormalizePath(session.ReportPath),
                ReportName = session.ReportName ?? string.Empty,
                Title = string.IsNullOrWhiteSpace(session.Title) ? "New chat" : session.Title.Trim(),
                ModelDisplayName = session.ModelDisplayName ?? string.Empty,
                ContextScope = string.IsNullOrWhiteSpace(session.ContextScope) ? "Summary" : session.ContextScope.Trim(),
                CreatedAtUtc = session.CreatedAtUtc == default ? DateTime.UtcNow : session.CreatedAtUtc,
                UpdatedAtUtc = session.UpdatedAtUtc == default ? DateTime.UtcNow : session.UpdatedAtUtc,
                Messages = (session.Messages ?? new List<AskChatMessage>())
                    .Where(message => message != null)
                    .Select(message => new AskChatMessage
                    {
                        Role = string.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role.Trim(),
                        Content = message.Content ?? string.Empty,
                        CreatedAtUtc = message.CreatedAtUtc == default ? DateTime.UtcNow : message.CreatedAtUtc
                    })
                    .ToList()
            };
        }

        private static void NormalizeSession(AskChatSession session)
        {
            if (session == null)
            {
                return;
            }

            session.SessionId = string.IsNullOrWhiteSpace(session.SessionId) ? Guid.NewGuid().ToString("N") : session.SessionId;
            session.ReportPath = NormalizePath(session.ReportPath);
            session.ReportName = session.ReportName ?? string.Empty;
            session.Title = string.IsNullOrWhiteSpace(session.Title) ? "New chat" : session.Title.Trim();
            session.ContextScope = string.IsNullOrWhiteSpace(session.ContextScope) ? "Summary" : session.ContextScope.Trim();
            session.Messages = (session.Messages ?? new List<AskChatMessage>())
                .Where(message => message != null)
                .Select(message => new AskChatMessage
                {
                    Role = string.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role.Trim(),
                    Content = message.Content ?? string.Empty,
                    CreatedAtUtc = message.CreatedAtUtc == default ? DateTime.UtcNow : message.CreatedAtUtc
                })
                .ToList();
            TrimSessionMessages(session);
            session.CreatedAtUtc = session.CreatedAtUtc == default ? DateTime.UtcNow : session.CreatedAtUtc;
            session.UpdatedAtUtc = session.UpdatedAtUtc == default ? session.CreatedAtUtc : session.UpdatedAtUtc;
        }

        private static void TrimSessionMessages(AskChatSession session)
        {
            if (session?.Messages == null || session.Messages.Count <= MaxMessagesPerSession * 2)
            {
                return;
            }

            int start = Math.Max(0, session.Messages.Count - (MaxMessagesPerSession * 2));
            session.Messages = session.Messages.Skip(start).ToList();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            string normalizedLeft = NormalizePath(left);
            string normalizedRight = NormalizePath(right);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
    }
}
