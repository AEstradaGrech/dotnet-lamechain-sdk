namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.DocumentLoader
{
    public class DocumentPage
    {
        public DocumentPage() { }
        public DocumentPage(int number, string text)
        {
            PageNumber = number;
            Text = text;
            Length = text.Length;
        }
        public int PageNumber { get; set; }
        public string Text { get; set; }
        public int Length { get; set; }
    }
}
