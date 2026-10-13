
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools
{
    public class LameTools : ToolsService<LameTools>
    {
        public LameTools(IServiceProvider serviceProvider) : base(serviceProvider) { }

        [Description("Load a list of skills into the LLM context")]
        public async Task<string> SkillsLoaderTool([Description("List of skills to load")] List<string> skills)
        {
            using var scope = _services.CreateScope();

            var docLoader = _services.GetService<IDocumentLoader<MarkdownLoaderService>>();

            if (docLoader == null) return string.Empty;

            StringBuilder sb = new StringBuilder();

            foreach(var skill in skills)
            {
                // Note: the expected folder structure is <PathToResourcesFromAppSettings>\markdowns\skills\<skillname>\SKILL.md
                // TODO / WIP --> SkillsLoader
                var skillDoc = await docLoader.LoadDocument($"skills\\{skill}.md");
                
                if(skillDoc != null && skillDoc.Pages.Count > 0 && !string.IsNullOrEmpty(skillDoc.Pages.FirstOrDefault().Text))
                    sb.AppendLine()
                      .AppendLine(skillDoc.Pages.FirstOrDefault().Text);
            }

            return sb.ToString();
        }
    }
}
