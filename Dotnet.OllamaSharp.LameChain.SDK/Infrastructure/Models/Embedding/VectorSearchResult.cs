
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Model;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding
{
    public class VectorSearchResult : ILameSearchResult
    {
        public VectorSearchResult(string text, float distance, string searchIndex, string? document  = null)
        {
            Text = text;
            Distance = distance;
            SearchIndex = searchIndex;
            Document = document;
        }

        public string SearchIndex { get; }
        public string? Document { get; }
        public string Text { get; }
        public float Distance { get; }
    }
}
