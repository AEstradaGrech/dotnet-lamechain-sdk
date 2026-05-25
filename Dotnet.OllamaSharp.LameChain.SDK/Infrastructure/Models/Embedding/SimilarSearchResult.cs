using System;
using System.Collections.Generic;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding
{
    public class SimilarSearchResult
    {
        public SimilarSearchResult(string text, float distance)
        {
            Text = text;
            Distance = distance;
        }

        public string Text { get; set; }
        public float Distance { get; set; }
    }
}
