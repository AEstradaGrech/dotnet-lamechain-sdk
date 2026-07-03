using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Reflection;
using System.Text;
using System.Text.Json;


namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public class OllamaHandler : BaseHandler
    {
        private IOllamaApiClient _client;
        public OllamaHandler(IServiceProvider provider, IConfiguration config, ChatRequest commandRequest, Dictionary<string, MethodInfo>? toolsLookup = null) 
            : base(provider, config, commandRequest, "ollama", toolsLookup) 
        {
            _client = provider.GetRequiredService<IOllamaApiClient>();
        }

        public override async Task<string> GetLlmResponse()
        {
            if (!isValid())
                throw new InvalidOperationException($"{nameof(OllamaHandler)} >> {nameof(GetLlmResponse)} >> {nameof(isValid)}");

            var sb = new StringBuilder();

            if (CommandRequest.Tools != null && CommandRequest.Tools.Count() > 0)
            {
                await foreach (var part in _client.ChatAsync(CommandRequest))
                {
                    if(part?.Message.ToolCalls.Count() > 0)
                    {
                        var call = part?.Message.ToolCalls.FirstOrDefault();

                        var name = call.Function.Name;

                        if (!_toolsLookup.ContainsKey(name))
                            throw new InvalidOperationException($"{nameof(GetLlmResponse)} >> No function MethodInfo found for function name: {name}");

                        var finalMessage = await handleFunctionCall(_toolsLookup[name], call.Function.Arguments); // this will try to execute the Tool (which may include LLM requestS) in the same request context (that's the problem I gues)
                        //sb.append(finalMessage.Content)
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(part?.Message.Content))
                            sb.Append(part.Message.Content);
                    }
                }
            } 
            else
            {
                await foreach (var part in _client.ChatAsync(CommandRequest)) // Tool's UserIntentCommand prompt ends here and crashes
                    if (!string.IsNullOrEmpty(part?.Message.Content))
                        sb.Append(part.Message.Content);
            }
        
            
            return sb.ToString().Trim();
        }

        public async Task<string> GenerateLlmResponse(GenerateRequest request)
        {
            if (_client == null)
                throw new ArgumentNullException($"{nameof(OllamaHandler)}.{nameof(GenerateLlmResponse)} >> {nameof(IOllamaApiClient)} is null");

            if (string.IsNullOrEmpty(request.System) && string.IsNullOrEmpty(request.Prompt))
                throw new InvalidDataException($"{nameof(OllamaHandler)}.{nameof(GenerateLlmResponse)} >> No system message or user prompt present in the request");

            var sb = new StringBuilder();

            await foreach (var part in _client.GenerateAsync(request))
                if (!string.IsNullOrEmpty(part?.Response))
                    sb.Append(part.Response);

            return sb.ToString().Trim();
        }

        private async Task<ChatMessage> handleFunctionCall(MethodInfo methodInfo, IDictionary<string, object> arguments)
        {
            var toolResult = await getToolResult(methodInfo, arguments); // real await, no blocking .Result

            var toolMessage = new Message(ChatRole.Tool, JsonSerializer.Serialize(toolResult)); // now serializes the unwrapped value, not a Task

            var finalMesage = new ChatMessage(ChatRole.Assistant.ToString(), string.Empty);

            return finalMesage;
        }

        private async Task<object?> getToolResult(MethodInfo methodInfo, IDictionary<string, object> arguments)
        {
            var toolInterface = typeof(IToolsService<>).MakeGenericType(methodInfo.DeclaringType);

            var toolSource = _serviceProvider.GetRequiredService(toolInterface);

            //This might be a Task in progress and its result must be awaited
            var invokeResult = methodInfo.Invoke(toolSource, OllamaTools.ParseToolCallArguments(methodInfo, arguments));

            // Reflection's Invoke doesn't know the method is `async`
            // - for a Task/Task<T> returning method it just hands back the (possibly still-running) Task object itself.
            if (invokeResult is Task task)
            {
                // Claude says: `dynamic` re-resolves the await pattern at runtime against the task's *actual*
                // type (e.g. Task<string>), so this awaits it AND unwraps .Result in one step -
                // without us needing to know T at compile time.
                // For dummies, it means:
                // By reassigning it to dynamic, we tell the compiler "don't bind the await now — defer that decision to the DLR (Dynamic Language Runtime) at the moment this line actually executes."
                // When it executes, the DLR looks at the object's actual runtime type (Task<string>), finds its real GetAwaiter(), and
                // the await expression evaluates to the unwrapped string.
                dynamic awaitableTask = task;
                return await awaitableTask;
            }

            return invokeResult;
        }
        protected override bool isValid()
            => base.isValid() && _client != null;
    }
}
