using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    public class AutoSkilledAgent : LameAgent
    {
        private const string SKILL_LOADING_MESSAGE = @"You have access to a set of skills. You must select the skills that might be useful to complete your task. 
Use the SkillsDiscoveryTool to get a list of the available skills to select, then use the SkillLoaderTool passing the selected skill names to
load the skill information and its associated tools (if any).

You don't need to load them all at once (you can load each skill progressively as you need. For example, you can load the 'chroma-dba' to gather information about the queried topic and then load a behavioral skill to
produce your final response according to the requested tone and mood ";

        public AutoSkilledAgent() : base() { }
        public AutoSkilledAgent(IServiceProvider serviceProvider, string model, PromptSettings? settings = null, string? name = null, string? description = null) : base(serviceProvider, model, settings, name, description)
        {
        }

        public override async Task<string> HandleSystemMessageSkills(List<string> skills)
        {
            if(skills.Count > 0)
            {
                AddTool(nameof(SkillsDiscoveryTool), GetType().GetMethod(nameof(SkillsDiscoveryTool)));
                AddTool(nameof(SkillsLoaderTool), GetType().GetMethod(nameof(SkillsLoaderTool)));
                return SKILL_LOADING_MESSAGE; // return the base instruction specifying how to use the tools / discover & load the assigned skills
            }

            return string.Empty; // configuration error. The AutoSkilledAgent requires at least one skill assigned to load the skill discovery & loader tools
        }

        [Description("Returns a catalogue of all the available skills to select, along with some metadata about the skill purpose")]
        public async Task<string> SkillsDiscoveryTool()
        {
            if (Skills.Count == 0) return string.Empty;

            using var scope = _serviceProvider.CreateScope();

            var docLoader = scope.ServiceProvider.GetService<IDocumentLoader<MarkdownLoaderService>>();

            if (docLoader == null) return string.Empty;

            StringBuilder sb = new StringBuilder();

            foreach (var skill in Skills)
            {
                // Note: the expected folder structure is <PathToResourcesFromAppSettings>\markdowns\skills\<skillname>\SKILL.md
                var skillDoc = await docLoader.LoadDocument($"skills\\{skill}.md");

                if (skillDoc != null && skillDoc.Pages.Count > 0 && !string.IsNullOrEmpty(skillDoc.Pages.FirstOrDefault().Text))
                    sb.AppendLine()
                      .AppendLine(skillDoc.Pages.FirstOrDefault().Text);
            }

            return sb.ToString().Trim();
        }

        [Description("Load a list of skills into the LLM context")]
        public async Task<string> SkillsLoaderTool([Description("List of skills to load")] List<string> skills)
        {
            using var scope = _serviceProvider.CreateScope();

            var docLoader = scope.ServiceProvider.GetService<IDocumentLoader<MarkdownLoaderService>>();

            if (docLoader == null) return string.Empty;

            StringBuilder sb = new StringBuilder();

            foreach (var skill in skills)
            {
                // Note: the expected folder structure is <PathToResourcesFromAppSettings>\markdowns\skills\<skillname>\SKILL.md
                var skillDoc = await docLoader.LoadDocument($"skills\\{skill}.md");

                if (skillDoc != null && skillDoc.Pages.Count > 0 && !string.IsNullOrEmpty(skillDoc.Pages.FirstOrDefault().Text))
                {
                    sb.AppendLine()
                      .AppendLine(skillDoc.Pages.FirstOrDefault().Text);

                    loadSkillTools(skillDoc.Pages.FirstOrDefault());
                }
            }

            return sb.ToString().Trim();
        }
    }
}
