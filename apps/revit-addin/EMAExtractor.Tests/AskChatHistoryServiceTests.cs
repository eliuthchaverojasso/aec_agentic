using System;
using System.IO;
using System.Linq;
using EMAExtractor.Models;
using EMAExtractor.Services;
using Xunit;

namespace EMAExtractor.Tests
{
    public class AskChatHistoryServiceTests
    {
        [Fact]
        public void SaveSessionAndLoadSessions_ReturnRecentChatsInDescendingOrder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "EMA_AI_Chat_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "ask_chat_history.json");
            string reportPath = Path.Combine(folder, "EMA_AI_Requirement_Check_Test_All_20260608_132154.html");

            try
            {
                AskChatSession older = AskChatHistoryService.CreateSession(reportPath, "Report A", "Plumbing thread", "Summary");
                AskChatHistoryService.AppendMessage(older, "user", "Tell me about row 606");
                AskChatHistoryService.AppendMessage(older, "assistant", "Answer A");
                older.UpdatedAtUtc = DateTime.UtcNow.AddHours(-2);
                AskChatHistoryService.SaveSession(older, path);

                AskChatSession newer = AskChatHistoryService.CreateSession(reportPath, "Report A", "Electrical thread", "Current Discipline");
                AskChatHistoryService.AppendMessage(newer, "user", "What is Not Met?");
                AskChatHistoryService.AppendMessage(newer, "assistant", "Answer B");
                newer.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
                AskChatHistoryService.SaveSession(newer, path);

                var recent = AskChatHistoryService.GetRecentSessionsForReport(reportPath, 8, null, path);

                Assert.Equal(2, recent.Count);
                Assert.Equal("Electrical thread", recent[0].Title);
                Assert.Equal("Plumbing thread", recent[1].Title);
            }
            finally
            {
                SafeDelete(folder);
            }
        }

        [Fact]
        public void CreateSessionAndAppendMessage_PersistsMessageHistory()
        {
            string folder = Path.Combine(Path.GetTempPath(), "EMA_AI_Chat_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "ask_chat_history.json");
            string reportPath = Path.Combine(folder, "EMA_AI_Requirement_Check_Test_All_20260608_132154.html");

            try
            {
                AskChatSession session = AskChatHistoryService.CreateSession(reportPath, "Report A", "New chat", "Summary");
                AskChatHistoryService.EnsureSessionTitle(session, "What is Not Met?");
                AskChatHistoryService.AppendMessage(session, "user", "What is Not Met?");
                AskChatHistoryService.AppendMessage(session, "assistant", "Answer text");
                AskChatHistoryService.SaveSession(session, path);

                var loaded = AskChatHistoryService.LoadSessionsFromPath(path);

                Assert.Single(loaded);
                Assert.Equal("What is Not Met?", loaded[0].Title);
                Assert.Equal(2, loaded[0].Messages.Count);
                Assert.Contains("What is Not Met?", loaded[0].DisplayText);
            }
            finally
            {
                SafeDelete(folder);
            }
        }

        private static void SafeDelete(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
