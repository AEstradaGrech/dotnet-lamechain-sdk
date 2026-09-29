namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.DocumentLoader
{
    public class Document
    {
        public Document() { }
        public Document(string name, string source, int totalPages, List<DocumentPage> pages)
        {
            Name = name;
            Source = source;
            TotalPages = totalPages;
            Pages = pages;
        }
        public string Name { get; set; }
        public string Source { get; set; }
        public int TotalPages { get; set; }
        public List<DocumentPage> Pages { get; set; }

    }
}
