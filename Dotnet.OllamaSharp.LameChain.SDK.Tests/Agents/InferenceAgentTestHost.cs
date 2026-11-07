using System.Text.Json;
using System.Text.Json.Nodes;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using DotnetLlamaSharp.Domain.Services.Inference;
using DotnetLlamaSharp.Infrastructure.Services.Inference;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents
{
    // Like AgentTestHost, but with the REAL OllamaInferenceService -> OllamaHandler pipeline and only the
    // Ollama HTTP client mocked. Used to test what happens below the agent: the recursive tool loop, the
    // tools loaded at request time (handleNewLoadedTools) and the traces of the whole run.
    public sealed class InferenceAgentTestHost : IDisposable
    {
        private readonly Queue<ChatResponseStream[]> _responses = new();

        public string ResourcesPath { get; }
        public string SkillResourcesPath => Path.Combine(ResourcesPath, "markdowns");
        public Mock<IOllamaApiClient> Client { get; } = new();
        public ServiceProvider Services { get; }

        // Snapshot of every request sent to Ollama, taken when the call happens (the handler mutates and reuses the request).
        public List<CapturedRequest> Requests { get; } = new();

        public InferenceAgentTestHost()
        {
            ResourcesPath = Path.Combine(Path.GetTempPath(), "lamechain-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ResourcesPath, "markdowns", "skills"));

            Client.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns((ChatRequest request, CancellationToken _) =>
                {
                    Requests.Add(new CapturedRequest(ToolNames(request), (request.Messages ?? []).ToList()));

                    if (_responses.Count == 0)
                        throw new InvalidOperationException($"No scripted Ollama response left for call #{Requests.Count}");

                    return Stream(_responses.Dequeue());
                });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> {
                    ["DocumentLoader:ResourcesPath"] = ResourcesPath,
                    ["DocumentLoader:SkillResourcesPath"] = SkillResourcesPath
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(Client.Object);
            services.AddSingleton(Options.Create(new OllamaSettings { DefaultModel = AgentTestHost.DefaultModel }));
            services.AddSingleton<ILogger<OllamaInferenceService>>(NullLogger<OllamaInferenceService>.Instance);
            services.AddTransient<IOllamaInferenceService, OllamaInferenceService>();
            services.AddSingleton(new Mock<IPromptCommandsFactory>().Object);
            services.AddMarkdownDocumentLoader();
            services.AddSkillsLoader();
            services.WithToolsFrom<AgentTestTools>();

            Services = services.BuildServiceProvider();
        }

        public InferenceAgentTestHost WithSkill(string name, string content)
        {
            var folder = Path.Combine(SkillResourcesPath, "skills", name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "SKILL.md"), content);

            return this;
        }

        // Next LLM turn: the model asks to run a tool.
        public InferenceAgentTestHost RespondsWithToolCall(string toolName, Dictionary<string, object?> arguments, int promptTokens = 10, int outputTokens = 5)
        {
            var toolCall = new Message.ToolCall { Id = $"call_{_responses.Count + 1}", Function = new Message.Function { Name = toolName, Arguments = arguments } };

            _responses.Enqueue([
                new ChatResponseStream { Message = new Message(ChatRole.Assistant, string.Empty) { ToolCalls = new List<Message.ToolCall> { toolCall } } },
                Done(string.Empty, promptTokens, outputTokens)
            ]);

            return this;
        }

        // Next LLM turn: the model answers with text.
        public InferenceAgentTestHost RespondsWithText(string text, int promptTokens = 10, int outputTokens = 5)
        {
            _responses.Enqueue([Done(text, promptTokens, outputTokens)]);

            return this;
        }

        public static Dictionary<string, object?> Args(params (string Name, object Value)[] arguments)
            => arguments.ToDictionary(a => a.Name, a => (object?)JsonSerializer.SerializeToElement(a.Value));

        public void Dispose()
        {
            Services.Dispose();

            try { Directory.Delete(ResourcesPath, recursive: true); } catch { /* best effort temp cleanup */ }
        }

        private static ChatDoneResponseStream Done(string content, int promptTokens, int outputTokens)
            => new ChatDoneResponseStream { Message = new Message(ChatRole.Assistant, content), Done = true, PromptEvalCount = promptTokens, EvalCount = outputTokens };

        private static List<string> ToolNames(ChatRequest request)
            => request.Tools == null ? [] : request.Tools.Cast<JsonObject>().Select(t => t["function"]!["name"]!.GetValue<string>()).ToList();

        private static async IAsyncEnumerable<ChatResponseStream?> Stream(ChatResponseStream[] parts)
        {
            foreach (var part in parts)
            {
                await Task.Yield();
                yield return part;
            }
        }

        public sealed record CapturedRequest(List<string> ToolNames, List<Message> Messages);
    }
}
