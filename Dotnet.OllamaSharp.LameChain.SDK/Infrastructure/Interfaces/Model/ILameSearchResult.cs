namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Model
{
    public interface ILameSearchResult
    {
        string SearchIndex { get; }
        string? Document { get; }
        string Text { get; }
        float Distance { get; }
    }
}
