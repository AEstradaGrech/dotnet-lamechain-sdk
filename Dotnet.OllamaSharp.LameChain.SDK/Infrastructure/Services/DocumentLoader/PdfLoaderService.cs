using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.DocumentLoader;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader
{
    public class PdfLoaderService : BaseDocumentLoader, IDocumentLoader<PdfLoaderService> 
    {
        const string LOADER = "pdfs";
        public PdfLoaderService(IConfiguration config) : base(
            docsFolder: LOADER,
            basePath: config["DocumentLoader:ResourcesPath"])
        {
            validate(LOADER);
        }
        public override Task<Document> LoadDocument(string fileName)
        {
            var fullPath = Path.Combine(_basePath, $"{fileName}.pdf");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"PDF not found: {fileName}");

            var pages = new List<DocumentPage>();

            using var document = PdfDocument.Open(fullPath); 

            foreach (var page in document.GetPages())
                if(!string.IsNullOrEmpty(page.Text.Trim()))
                    pages.Add(new DocumentPage(page.Number, page.Text.Trim().Replace("\n", " ").Replace("\r\n", " ").Replace("\t", " ").Replace("\\", "")));
            
            return Task.FromResult(new Document(fileName, "pdf", document.NumberOfPages, pages));
        }

        public override Task<DocumentPage> LoadPage(string fileName, int index)
        {
            if (index < 0)
                throw new InvalidOperationException($"Invalid Index {index}");

            var fullPath = Path.Combine(_basePath, $"{fileName}.pdf");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"PDF not found: {fileName}");

            var document = PdfDocument.Open(fullPath);

            if (index > document.NumberOfPages)
                throw new InvalidOperationException($"Requested index ({index}) out of range ({document.NumberOfPages})");

            var page = document.GetPage(index);

            return Task.FromResult(new DocumentPage(index, page.Text.Trim().Replace("\n", " ").Replace("\r\n", " ").Replace("\t", " ").Replace("\\", "")));
        }

        public override Task<List<DocumentPage>> LoadPages(string fileName, int startIndex, int batchSize)
        {
            if (startIndex < 0)
                throw new InvalidOperationException($"Invalid Index {startIndex}");

            var fullPath = Path.Combine(_basePath, $"{fileName}.pdf");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"PDF not found: {fileName}");

            var document = PdfDocument.Open(fullPath);

            if (startIndex > document.NumberOfPages)
                throw new InvalidOperationException($"Requested index ({startIndex}) out of range ({document.NumberOfPages})");
            
            if (batchSize <= 0)
                batchSize = 1;

            var pages = new List<DocumentPage>();
            for(int i = startIndex; i < (startIndex + batchSize); i++) 
            {
                if (i > document.NumberOfPages) break;

                var page = document.GetPage(i);

                pages.Add(new DocumentPage(i, page.Text));
            }

            return Task.FromResult(pages);
        }
    }
}
