using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace DotnetLlamaSharp.Domain.Services.Inference
{
    public interface IOllamaInferenceService
    {
        Task<Message> GeneratePrompt(GenerateRequest request);
        IAsyncEnumerable<GenerateResponseStream?> GeneratePromptStream(GenerateRequest request);
        Task<Message> ChatPrompt(ChatRequest request);
        IAsyncEnumerable<ChatResponseStream?> ChatPromptStream(ChatRequest request);
        Task<EmbedResponse> GetEmbeddings(EmbedRequest request);
        Task<T> StructuredPrompt<T>(string prompt, string model, string? systemGuidance = null, RequestOptions? options = null) where T : class;
        Task<T> CommandPrompt<T>(GenerateRequest request, int validations = 0, EPromptValidation type = EPromptValidation.REVIEW_ONLY, JsonOutputRefinerCommand<T> validator = null, bool withJsonInfo = true) where T : class;
        Task<T> CommandPrompt<T>(ChatRequest chatRequest, int validations = 0, EPromptValidation type = EPromptValidation.REVIEW_ONLY, JsonOutputRefinerCommand<T> validator = null, bool withJsonInfo = true) where T : class;

    }
}
