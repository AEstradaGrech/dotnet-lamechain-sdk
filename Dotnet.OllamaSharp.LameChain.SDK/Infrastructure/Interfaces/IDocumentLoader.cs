
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader
{
    public interface IDocumentLoader<T> where T : BaseDocumentLoader
    {
        Task<Document> LoadDocument(string fileName);
        Task<DocumentPage> LoadPage(string fileName, int page);
        Task<List<DocumentPage>> LoadPages(string fileName, int startIndex, int batchSize);
    }
}
