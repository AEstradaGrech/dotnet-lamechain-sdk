using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;

namespace DotnetLlamaSharp.Domain.Services.Embeddings
{
    public interface IEmbeddingsService
    {
        Task<ModelEmbeddings> GenerateEmbeddings(string text, int? dimensions = null, string? model = null);
        Task<ModelEmbeddings> GenerateEmbeddings(List<string> texts, int? dimensions = null, string? model = null);
    }
}
