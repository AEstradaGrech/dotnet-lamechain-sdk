
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.DocumentLoader;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader
{
    
    public class MarkdownLoaderService : BaseDocumentLoader, IDocumentLoader<MarkdownLoaderService>
    {
        const string LOADER = "markdowns";

        public MarkdownLoaderService(IConfiguration config) : base(docsFolder: LOADER, basePath: config["DocumentLoader:ResourcesPath"]) { validate(LOADER); }

        /// <summary>
        /// Loads a skill and returns it as a Single Page Document
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public override async Task<Document> LoadDocument(string fileName)
        {
            var fullFile = await readFile(fileName);

            var pipeline = new MarkdownPipelineBuilder()
                    .UseYamlFrontMatter()
                    .Build();

            var markdown = Markdown.Parse(fullFile, pipeline);

            var frontmatter = markdown.Descendants<YamlFrontMatterBlock>().FirstOrDefault();

            var pages = frontmatter == null ? new List<DocumentPage>() : new List<DocumentPage> { new DocumentPage(1, string.Join(Environment.NewLine, frontmatter.Lines.Lines.Select(x => x.ToString().Trim()))) };

            var sections = extractSections(markdown, pipeline, pages.Count);

            if (sections.Count > 0)
                pages.AddRange(sections);

            pages.Add(new DocumentPage(pages.Count + 1, fullFile));

            return new Document(fileName, "markdown", pages.Count, pages);
        }

        /// <summary>
        /// Loads a skill and returns the corresponding section as 'page'
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override async Task<DocumentPage> LoadPage(string fileName, int page)
        {
            var document = await LoadDocument(fileName);

            if(document.Pages.Count <= 0)
                throw new FileNotFoundException($"No content found for skill: {fileName}");

            if (page <= 0)
                page = 1;

            if(page > document.Pages.Count)
                throw new ArgumentOutOfRangeException($"Requested page {page} is out of range. Total pages {document.Pages.Count}");

            return document.Pages.Skip(page -1).FirstOrDefault();
        }

        /// <summary>
        /// Loads a skill and returns the corresponding sections as 'pages'
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="startIndex"></param>
        /// <param name="batchSize"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override async Task<List<DocumentPage>> LoadPages(string fileName, int startIndex, int batchSize)
        {
            var document = await LoadDocument(fileName);

            if (document.Pages.Count <= 0)
                throw new FileNotFoundException($"No content found for skill: {fileName}");

            if(startIndex > document.Pages.Count)
                throw new ArgumentOutOfRangeException($"Start index {startIndex} is out of range for skill: {fileName}");

            if(startIndex <= 0)
                startIndex = 1;

            return document.Pages.Skip(startIndex - 1).Take(batchSize).ToList();
        }

        private async Task<string> readFile(string fileName)
        {
            var fullPath = Path.Combine(_basePath, fileName, $"SKILL.md");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"SKILL MD file not found in folder: {fileName}");

            return await File.ReadAllTextAsync(fullPath);
        }

        private List<DocumentPage> extractSections(MarkdownDocument markdown, MarkdownPipeline pipeline, int pageOffset = 0)
        {
            var sections = new List<DocumentPage>();
            for(int i = 0; i < markdown.Count; i++)
            {
                var block = markdown[i];
                if (block is HeadingBlock headingBlock && headingBlock.Level <= 2)
                    sections.Add(sectionAsPage(i, headingBlock, markdown, pipeline, pageOffset + sections.Count + 1));
            }
            return sections;
        }

        private DocumentPage sectionAsPage(int headingIndex, HeadingBlock headingBlock, MarkdownDocument markdown, MarkdownPipeline pipeline, int docPage = 0)
        {
            var sectionText = new StringBuilder().Append(blockLevelTag(headingBlock.Level)).Append(" ").Append(headingBlock.Inline?.FirstChild?.ToString()).Append("\n");

            for (int i = headingIndex + 1; i < markdown.Count; i++)
            {
                var block = markdown[i];

                if (block is HeadingBlock nextHeading)
                {
                    if (nextHeading.Level <= 2) 
                        break;

                    sectionText.Append(blockLevelTag(nextHeading.Level)).Append(" ").Append(nextHeading.Inline?.FirstChild?.ToString()).Append("\n");
                }
                else
                {
                    if (block is ListBlock listBlock)
                    {
                        if (int.TryParse(listBlock.BulletType.ToString(), out int bulletType))
                            blockToPlainText(block, pipeline).Split("\n").ToList().ForEach(line => {
                                if(!string.IsNullOrEmpty(line.Trim()))
                                    sectionText.AppendLine($"{bulletType++}: {line}");
                            });
                        else blockToPlainText(block, pipeline).Split("\n").ToList().ForEach(line => { 
                            if(!string.IsNullOrEmpty(line.Trim()))
                                sectionText.AppendLine($"- {line}"); 
                            });
                    }

                    if(block is FencedCodeBlock codeBlock)
                        sectionText.AppendLine($"```{codeBlock.Info}").AppendLine(blockToPlainText(block, pipeline)).AppendLine("```");
                    
                    if (block is ParagraphBlock)
                        sectionText.Append(blockToPlainText(block, pipeline));
                }
            }
            return new DocumentPage(docPage, sectionText.ToString().Trim());
        }

        private string blockLevelTag(int level) => string.Join("", Enumerable.Repeat("#", level));
        private string blockToPlainText(MarkdownObject block, MarkdownPipeline pipeline)
        {
            var writer = new StringWriter();

            var renderer = new HtmlRenderer(writer)
            {
                EnableHtmlForBlock = false,
                EnableHtmlForInline = false,
                EnableHtmlEscape = false
            };

            pipeline.Setup(renderer);

            renderer.Render(block);

            return writer.ToString();
        }
    }
}
