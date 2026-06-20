using System.Threading;
using System.Threading.Tasks;

namespace EMAExtractor.Services
{
    public interface IAiModelProvider
    {
        string ProviderName { get; }
        bool IsAvailable { get; }

        Task<AiCompletionResult> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            int maxTokens = 512,
            CancellationToken cancellationToken = default);
    }

    public sealed class AiCompletionResult
    {
        public bool Success { get; set; }
        public string Content { get; set; }
        public string ErrorMessage { get; set; }
        public bool UsedFallback { get; set; }
        public string ProviderName { get; set; }
    }
}
