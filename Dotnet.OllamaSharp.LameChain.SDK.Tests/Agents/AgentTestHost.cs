using System.ComponentModel;
using System.Text.Json.Nodes;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OllamaSharp.Models.Chat;
using DotnetLlamaSharp.Domain.Services.Inference;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents
{
    // Test-only ToolService registered through WithToolsFrom<T>, so its [Description] methods are
    // discoverable by the Toolbox and can be equipped by skills through their 'allowed-tools' field.
    public class AgentTestTools : ToolsService<AgentTestTools>
    {
        [Description("Returns the weather for a city")]
        public string LookupWeather(string city) => $"sunny in {city}";

        [Description("Searches the docs")]
        public Task<string> SearchDocs(string query) => Task.FromResult($"docs about {query}");

        // No [Description] -> must NOT be exposed as a tool by the Toolbox or the catalogue.
        public string NotATool() => "nope";
    }

    // Shared skill files used across the agent tests. They follow the SDK skill format documented in
    // the README: YAML frontmatter (heading) + markdown body, stored as <skill-name>/SKILL.md.
    public static class TestSkills
    {
        public const string PirateBody = "Always answer like a pirate, using nautical slang.";
        public const string ReviewerBody = "You are a senior .NET engineer doing a code review.";
        public const string DocsBody = "Use the tools to search the documentation before answering.";

        public const string PirateAssistant = $@"---
name: pirate-assistant
description: Makes the assistant talk like a pirate.
version: 1.0.0
type: behavioral
---

# Pirate Assistant

{PirateBody}
";

        public const string SeniorCodeReviewer = $@"---
name: senior-code-reviewer
description: Makes the assistant answer as a strict senior code reviewer.
version: 1.0.0
type: behavioral
---

# Senior Code Reviewer

{ReviewerBody}

## Tone

Direct and concise.
";

        // Tool skill: 'allowed-tools' is the last frontmatter field (as in the README example).
        // 'UnknownTool' is not registered in any ToolService and must be silently skipped.
        public const string DocsQuery = $@"---
name: docs-query
description: Answers questions using the documentation tools.
version: 1.0.0
type: tools
allowed-tools: [LookupWeather, SearchDocs, UnknownTool]
---

# Docs Query

{DocsBody}
";

        // Same tool skill, but 'allowed-tools' is NOT the last frontmatter field.
        public const string DocsQueryToolsNotLast = $@"---
name: docs-query-reordered
description: Answers questions using the documentation tools.
allowed-tools: [LookupWeather, SearchDocs]
version: 1.0.0
---

# Docs Query

{DocsBody}
";
    }

    // Builds a real DI container (Toolbox, markdown skill loader, agents factory) around a mocked
    // IOllamaInferenceService, and a temporary resources folder for the skill files.
    public sealed class AgentTestHost : IDisposable
    {
        public const string DefaultModel = "default-model";

        public string ResourcesPath { get; }
        public Mock<IOllamaInferenceService> Inference { get; } = new();
        public Mock<IPromptCommandsFactory> CommandsFactory { get; } = new();
        public ServiceProvider Services { get; }

        // Last ChatPrompt call captured by the mocked inference service.
        public ChatRequest? LastRequest { get; private set; }
        public string? LastProvider { get; private set; }
        public Dictionary<string, ToolBinding>? LastTools { get; private set; }
        public int ChatPromptCalls { get; private set; }

        public AgentTestHost(string llmResponse = "llm answer")
        {
            ResourcesPath = Path.Combine(Path.GetTempPath(), "lamechain-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ResourcesPath, "markdowns", "skills"));

            Inference
                .Setup(s => s.ChatPrompt(It.IsAny<ChatRequest>(), It.IsAny<string>(), It.IsAny<Dictionary<string, ToolBinding>?>()))
                .Callback<ChatRequest, string, Dictionary<string, ToolBinding>?>((request, provider, tools) =>
                {
                    ChatPromptCalls++;
                    LastRequest = request;
                    LastProvider = provider;
                    LastTools = tools;
                })
                .ReturnsAsync(new Message(ChatRole.Assistant, llmResponse));

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DocumentLoader:ResourcesPath"] = ResourcesPath })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(Inference.Object);
            services.AddSingleton(Options.Create(new OllamaSettings { DefaultModel = DefaultModel }));
            services.AddSingleton(CommandsFactory.Object);
            services.AddScoped<IAgentsFactory, AgentsFactory>();
            services.AddMarkdownDocumentLoader();
            services.WithToolsFrom<AgentTestTools>();

            Services = services.BuildServiceProvider();
        }

        public AgentTestHost WithSkill(string name, string content)
        {
            var folder = Path.Combine(ResourcesPath, "markdowns", "skills", name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "SKILL.md"), content);

            return this;
        }

        public AgentTestHost WithDefaultSkills()
            => WithSkill("pirate-assistant", TestSkills.PirateAssistant)
              .WithSkill("senior-code-reviewer", TestSkills.SeniorCodeReviewer)
              .WithSkill("docs-query", TestSkills.DocsQuery)
              .WithSkill("docs-query-reordered", TestSkills.DocsQueryToolsNotLast);

        public List<Message> LastMessages => LastRequest!.Messages.ToList();
        public string LastSystemMessage => LastMessages.First(m => m.Role == ChatRole.System).Content!;

        // Tool names advertised to the LLM in the last request (OllamaTools.FromMethod definitions).
        public List<string> LastToolDefinitionNames
            => LastRequest!.Tools == null ? [] : LastRequest.Tools.Cast<JsonObject>().Select(t => t["function"]!["name"]!.GetValue<string>()).ToList();

        public void Dispose()
        {
            Services.Dispose();

            try { Directory.Delete(ResourcesPath, recursive: true); } catch { /* best effort temp cleanup */ }
        }
    }
}
