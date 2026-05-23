namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding
{
    public class TextEmbedding
    {
        public TextEmbedding(string text, ReadOnlyMemory<float> embeddings, int dimensions)
        {
            Text = text;
            Vector = embeddings;
            Dimensions = dimensions;
        }
        public string Text { get; set; }
        public ReadOnlyMemory<float> Vector { get; set; }
        public int Dimensions { get; set; }
    }
}
