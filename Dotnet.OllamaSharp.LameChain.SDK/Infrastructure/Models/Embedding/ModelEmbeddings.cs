namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding
{
    public class ModelEmbeddings
    {
        public string Model { get; set; }
        public List<TextEmbedding> GeneratedEmbeddings { get; set; }
    }
}
