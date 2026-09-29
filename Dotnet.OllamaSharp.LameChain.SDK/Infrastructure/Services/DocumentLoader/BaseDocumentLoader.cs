using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.DocumentLoader;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader
{
    public abstract class BaseDocumentLoader
    {
        protected readonly string _basePath;
        public BaseDocumentLoader(string docsFolder, string? basePath = null)
        {
            if (string.IsNullOrEmpty(basePath))
                throw new ArgumentNullException($"{nameof(BaseDocumentLoader)} >> {nameof(basePath)}");

            _basePath = Path.Combine(basePath, docsFolder);
        }
        public abstract Task<Document> LoadDocument(string fileName);
        public abstract Task<DocumentPage> LoadPage(string fileName, int page);
        public abstract Task<List<DocumentPage>> LoadPages(string fileName, int startIndex, int batchSize);

        protected void validate(string loaderName)
        {
            if (string.IsNullOrEmpty(_basePath))
                throw new ArgumentNullException($"No folder path specified for {loaderName} loader");
        }
    }
}
