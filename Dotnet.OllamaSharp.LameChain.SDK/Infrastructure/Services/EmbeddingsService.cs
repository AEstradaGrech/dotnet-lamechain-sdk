using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using Microsoft.Extensions.AI;

namespace DotnetLlamaSharp.Services.Embeddings
{
    public class EmbeddingsService : IEmbeddingsService
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
        public EmbeddingsService(IEmbeddingGenerator<string, Embedding<float>> generator)
        {
            _generator = generator;
        }

        public async Task<ModelEmbeddings> GenerateEmbeddings(string text, int? dimensions, string? model)
            => await GenerateEmbeddings(string.IsNullOrEmpty(text) ? new List<string>() : new List<string> { text }, dimensions, model);

        public async Task<ModelEmbeddings> GenerateEmbeddings(List<string> texts, int? dimensions = null, string? model = null)
        {
            if (texts.Count() == 0)
                throw new InvalidOperationException("No texts to embed found");

            var result = await _generator.GenerateAsync(texts, new EmbeddingGenerationOptions { ModelId = model, Dimensions = dimensions });

            if (result.Count() == 0)
                throw new ArgumentNullException("A problem has occured while generating the text embeddings");

            var sample = result.First();

            var textEmbeddings = new List<TextEmbedding>();

            for (int i = 0; i < texts.Count(); i++)
                textEmbeddings.Add(new TextEmbedding(texts[i], result[i].Vector, result[i].Dimensions));

            return new ModelEmbeddings { Model = sample.ModelId ?? "default", GeneratedEmbeddings = textEmbeddings };
        }
    }
}
