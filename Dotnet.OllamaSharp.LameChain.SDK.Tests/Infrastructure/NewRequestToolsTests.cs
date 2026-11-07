using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Request;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Response.GroqProvider;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Agents;
using Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    // Tools loaded while a request is running (e.g. by AutoSkilledAgent.SkillsLoaderTool) are cached in the
    // handler (CacheNewRequestTool) and added to the next LLM call of the same request (handleNewLoadedTools),
    // so providers that validate the called tool against request.tools (Groq) accept them.
    public class NewRequestToolsTests
    {
        private readonly Mock<IServiceProvider> _serviceProvider = new();
        private readonly Mock<IConfiguration> _config = new();
        private readonly Mock<IOllamaApiClient> _ollamaClient = new();
        private readonly Mock<IGroqClient> _groqClient = new();
        private readonly List<List<string>> _ollamaToolNamesPerCall = new();

        private static readonly ToolBinding EchoBinding = new(typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))!);

        public NewRequestToolsTests()
        {
            _serviceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(_ollamaClient.Object);
            _serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<OllamaSettings>))).Returns(Options.Create(new OllamaSettings { DefaultModel = "test-model" }));
            _serviceProvider.Setup(sp => sp.GetService(typeof(IGroqClient))).Returns(_groqClient.Object);
            _serviceProvider.Setup(sp => sp.GetService(typeof(IOptions<GroqSettings>))).Returns(Options.Create(BaseHandlerTests.BuildTestGroqSettings()));

            _ollamaClient.Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns((ChatRequest request, CancellationToken _) =>
                {
                    _ollamaToolNamesPerCall.Add(ToolNames(request.Tools));
                    return Answer("answer");
                });
        }

        #region Handler Tests

        [Fact]
        public async Task GetLlmResponse_WithCachedRequestTool_AddsItsDefinitionToTheRequestKeepingTheExistingTools()
        {
            // Arrange
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object);
            handler.CacheNewRequestTool(nameof(TestToolsService.Echo), EchoBinding);
            var request = UserRequest(OllamaTools.FromMethod(typeof(TestToolsService).GetMethod(nameof(TestToolsService.EchoAsync))!));

            // Act
            await handler.GetLlmResponse(request);

            // Assert
            _ollamaToolNamesPerCall.Single().Should().Equal(nameof(TestToolsService.EchoAsync), nameof(TestToolsService.Echo));
        }

        [Fact]
        public async Task GetLlmResponse_AfterAddingTheCachedTools_ClearsTheCacheSoTheNextRequestDoesNotGetThem()
        {
            // Arrange
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object);
            handler.CacheNewRequestTool(nameof(TestToolsService.Echo), EchoBinding);
            await handler.GetLlmResponse(UserRequest());

            // Act
            await handler.GetLlmResponse(UserRequest());

            // Assert
            _ollamaToolNamesPerCall[0].Should().Equal(nameof(TestToolsService.Echo));
            _ollamaToolNamesPerCall[1].Should().BeEmpty();
        }

        [Fact]
        public async Task CacheNewRequestTool_WithTheSameToolTwice_AddsItOnlyOnce()
        {
            // Arrange
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object);

            // Act
            handler.CacheNewRequestTool(nameof(TestToolsService.Echo), EchoBinding);
            handler.CacheNewRequestTool(nameof(TestToolsService.Echo), EchoBinding);
            await handler.GetLlmResponse(UserRequest());

            // Assert
            _ollamaToolNamesPerCall.Single().Should().Equal(nameof(TestToolsService.Echo));
        }

        [Fact]
        public void CacheNewRequestTool_WithNotifyActionConfigured_NotifiesTheLoadedTool()
        {
            // Arrange
            var notifications = new List<(string Title, string Message)>();
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object, (title, message) => notifications.Add((title, message)));

            // Act
            handler.CacheNewRequestTool(nameof(TestToolsService.Echo), EchoBinding);

            // Assert
            notifications.Should().Contain(n => n.Message == $"Loading new request tool: {nameof(TestToolsService.Echo)}");
        }

        [Fact]
        public async Task GetLlmResponse_WithoutCachedTools_LeavesTheRequestToolsUntouched()
        {
            // Arrange
            var handler = new OllamaHandler(_serviceProvider.Object, _config.Object);
            var tools = new List<object> { OllamaTools.FromMethod(typeof(TestToolsService).GetMethod(nameof(TestToolsService.Echo))!) };
            var request = UserRequest(tools.ToArray());
            var originalTools = request.Tools;

            // Act
            await handler.GetLlmResponse(request);

            // Assert
            request.Tools.Should().BeSameAs(originalTools);
        }

        [Fact]
        public async Task Groq_GetLlmResponse_WithCachedRequestTool_SendsItInTheGroqRequestTools()
        {
            // Arrange: the reason this feature exists - Groq rejects calls to tools missing from request.tools.
            GroqChatRequest? sent = null;
            _groqClient.Setup(c => c.GetChatCompletion(It.IsAny<GroqChatRequest>()))
                .Callback<GroqChatRequest>(request => sent = request)
                .ReturnsAsync(new GroqChatCompletion { Choices = new List<GroqCompletionChoice> { new GroqCompletionChoice { Message = new Message(ChatRole.Assistant, "answer") } } });
            var handler = new GroqHandler(_serviceProvider.Object, _config.Object);
            handler.CacheNewRequestTool(nameof(TestToolsService.Echo), EchoBinding);

            // Act
            await handler.GetLlmResponse(UserRequest());

            // Assert
            ToolNames(sent!.Tools).Should().Equal(nameof(TestToolsService.Echo));
        }

        #endregion

        #region AutoSkilledAgent End To End

        [Fact]
        public async Task RunPrompt_WhenSkillsLoaderToolLoadsAToolSkill_TheNextLlmCallOfTheSameRequestAdvertisesTheNewTools()
        {
            // Arrange: real inference service + handler, only the Ollama HTTP client is mocked.
            using var host = new InferenceAgentTestHost().WithSkill("docs-query", TestSkills.DocsQuery);
            host.RespondsWithToolCall(nameof(AutoSkilledAgent.SkillsLoaderTool), InferenceAgentTestHost.Args(("skills", new[] { "docs-query" })))
                .RespondsWithText("done");
            var agent = new AutoSkilledAgent(host.Services, "llama3");
            agent.WithSkills(["docs-query"]);

            // Act
            await agent.RunPrompt("what is the weather in Kingston?");

            // Assert
            host.Requests.Should().HaveCount(2);
            host.Requests[0].ToolNames.Should().BeEquivalentTo(nameof(AutoSkilledAgent.SkillsDiscoveryTool), nameof(AutoSkilledAgent.SkillsLoaderTool));
            // 'UnknownTool' in the skill's allowed-tools is not registered, so it is skipped.
            host.Requests[1].ToolNames.Should().BeEquivalentTo(
                nameof(AutoSkilledAgent.SkillsDiscoveryTool), nameof(AutoSkilledAgent.SkillsLoaderTool),
                nameof(AgentTestTools.LookupWeather), nameof(AgentTestTools.SearchDocs));
        }

        [Fact]
        public async Task RunPrompt_WhenTheLlmCallsAToolLoadedInTheSameRequest_ExecutesItAndAdvertisesItOnlyOnce()
        {
            // Arrange
            using var host = new InferenceAgentTestHost().WithSkill("docs-query", TestSkills.DocsQuery);
            host.RespondsWithToolCall(nameof(AutoSkilledAgent.SkillsLoaderTool), InferenceAgentTestHost.Args(("skills", new[] { "docs-query" })))
                .RespondsWithToolCall(nameof(AgentTestTools.LookupWeather), InferenceAgentTestHost.Args(("city", "Kingston")))
                .RespondsWithText("It is sunny in Kingston");
            var agent = new AutoSkilledAgent(host.Services, "llama3");
            agent.WithSkills(["docs-query"]);

            // Act
            var result = await agent.RunPrompt("what is the weather in Kingston?");

            // Assert
            result.Should().Be("It is sunny in Kingston");
            agent.Tools.Should().ContainKey(nameof(AgentTestTools.LookupWeather));
            host.Requests.Should().HaveCount(3);
            host.Requests[2].ToolNames.Count(name => name == nameof(AgentTestTools.LookupWeather)).Should().Be(1);
            host.Requests[2].Messages.Should().Contain(m => m.Role == ChatRole.Tool
                && m.ToolName == nameof(AgentTestTools.LookupWeather)
                && m.Content!.Contains("sunny in Kingston"));
        }

        #endregion

        #region Helper Methods

        private static ChatRequest UserRequest(params object[] tools)
            => new ChatRequest { Model = "test-model", Messages = new List<Message> { new Message(ChatRole.User, "hi") }, Tools = tools.ToList() };

        private static List<string> ToolNames(IEnumerable<object>? tools)
            => tools == null ? [] : tools.Select(t => JsonSerializer.SerializeToNode(t)!["function"]!["name"]!.GetValue<string>()).ToList();

        private static async IAsyncEnumerable<ChatResponseStream?> Answer(string text)
        {
            await Task.Yield();
            yield return new ChatDoneResponseStream { Message = new Message(ChatRole.Assistant, text), Done = true };
        }

        #endregion
    }
}
