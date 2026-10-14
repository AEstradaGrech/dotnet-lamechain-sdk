using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace DotnetLlamaSharp.Domain.Services.Inference
{
    public interface IOllamaInferenceService
    {
        Task<Message> GeneratePrompt(GenerateRequest request, string provider);
        IAsyncEnumerable<GenerateResponseStream?> GeneratePromptStream(GenerateRequest request);
        Task<Message> ChatPrompt(ChatRequest request, string provider, Dictionary<string, ToolBinding>? requestTools = null);
        IAsyncEnumerable<ChatResponseStream?> ChatPromptStream(ChatRequest request);
        Task<EmbedResponse> GetEmbeddings(EmbedRequest request);
        Task<T> StructuredPrompt<T>(string prompt, string model, string provider, string? systemGuidance = null, RequestOptions? options = null) where T : class;
        Task<T> CommandPrompt<T>(GenerateRequest request, CommandPromptValidation<T>? validation = null, string provider = "ollama", bool withJsonInfo = true) where T : class;
        Task<T> CommandPrompt<T>(ChatRequest chatRequest, CommandPromptValidation<T>? validation = null, string provider = "ollama", Dictionary<string, ToolBinding>? requestTools = null, bool withJsonInfo = true) where T : class;

        void SubscribeNotifier(Action<string> notifier);
        void LoadNewRequestTools(string toolName, ToolBinding binding);

    }
}
