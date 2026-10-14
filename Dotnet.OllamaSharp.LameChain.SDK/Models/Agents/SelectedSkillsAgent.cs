

using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.AtomicValues;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    public class SelectedSkillsAgent : LameAgent
    {
        private const string SELECTION_MESSAGE = "Select all the SKILL NAMES from the choices list that might help you to generate a response given this USER INPUT: ";
        private readonly IPromptCommandsFactory _commandsFactory;
        public SelectedSkillsAgent(IServiceProvider serviceProvider, IPromptCommandsFactory commandsFactory, string model, PromptSettings? settings = null, string? name = null, string? description = null) 
            : base(serviceProvider, model, settings, name, description) 
        {
            _commandsFactory = commandsFactory ?? throw new ArgumentNullException($"{nameof(IPromptCommandsFactory)}");
        }

        public override async Task<string> HandleSystemMessageSkills(List<string> skills)
        {
            if(skills.Count == 0) return string.Empty;

            using var scope = _serviceProvider.CreateScope();

            var docLoader = scope.ServiceProvider.GetRequiredService<IDocumentLoader<SkillsLoaderService>>();

            var choices = new List<string>();

            var sb = new StringBuilder();

            foreach(var skill in skills)
            {
                // TODO / WIP --> SkillsLoader
                var skillDoc = await docLoader.LoadDocument($"{skill}");

                if(skillDoc != null && skillDoc.Pages.Count > 0 && !string.IsNullOrEmpty(skillDoc.Pages.FirstOrDefault().Text))
                {
                    var split = skillDoc.Pages.FirstOrDefault().Text.Split("\n");

                    for(int i = 0; i < split.Length; i++)
                    {
                        var fieldSplit = split[i].Split(":");

                        if (fieldSplit.Length > 1)
                        {
                            if (fieldSplit[0].ToLower() == "name")
                                sb.AppendLine()
                                  .Append(fieldSplit[1].Trim());

                            if (fieldSplit[0].ToLower() == "description")
                                sb.Append(": ").Append(fieldSplit[1]);
                        }

                        else sb.AppendLine(fieldSplit[0]);
                    }

                    choices.Add(sb.ToString().Trim());
                    sb.Clear();
                }
            }

            var command = _commandsFactory.GetMultiChoiceCommand();

            var selectRequest = new MultiChoiceRequest(selections: null, choices, SELECTION_MESSAGE + UserInput, isGuidanceAppend: false, model: FullHandlerModel);

            var selectedSkills = await command.Prompt(selectRequest);
   
            return selectedSkills == null ? string.Empty : await base.HandleSystemMessageSkills(selectedSkills);
        }
    }
}
