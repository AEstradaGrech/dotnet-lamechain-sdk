using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models.Chat;
using System.Reflection;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public abstract class BaseHandler
    {
        protected readonly IConfiguration _config;
        protected readonly IServiceProvider _serviceProvider;
        protected readonly string _provider;

        protected readonly Action<string, string> _onHandlerNotify;

        public bool IsProvider(string provider) => _provider == provider;

        public BaseHandler(IServiceProvider serviceProvider, IConfiguration config, string provider, Action<string, string>? notifyAction = null)
        {
            _provider = provider.Trim();

            if (string.IsNullOrEmpty(_provider))
                throw new InvalidDataException($"{GetType().Name} >> LLM provider name is empty");

            _config = config;
            _serviceProvider = serviceProvider;

            if(notifyAction != null)
                _onHandlerNotify += notifyAction;
        }

        public bool IsOfType<T>() where T : BaseHandler => this is T;
        public T AsType<T>() where T : BaseHandler => (T)this;
        public BaseHandler UpdateHandler(string provider)
            => provider switch {
                "ollama" => new OllamaHandler(_serviceProvider, _config, _onHandlerNotify),
                "anthropic" => new ClaudeHandler(_serviceProvider, _config, _onHandlerNotify),
                "groq" => new GroqHandler(_serviceProvider, _config, _onHandlerNotify),
                _ => new OllamaHandler(_serviceProvider, _config, _onHandlerNotify)
            };

        public abstract Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null); // override & serviceProvider.GetRequiredService<HandlerClient>() <- cada uno pide una copia de SU http client

        protected virtual bool isValid()
            => _serviceProvider != null && _config != null && !string.IsNullOrEmpty(_provider);

        protected virtual void validateFunctionCallMessage<T>(T message, Dictionary<string, MethodInfo>? toolsLookup) where T : class
        {
            var ollamaMessage = message as Message;

            if (toolsLookup == null)
                throw new ArgumentNullException($"{nameof(handleFunctionCall)} >> {nameof(toolsLookup)}");

            if(ollamaMessage.ToolCalls.Count() == 0)
                throw new InvalidOperationException($"{nameof(GetLlmResponse)} >> No ToolCalls present in the LLM tool request message");

            var toolCall = ollamaMessage.ToolCalls.FirstOrDefault();

            var toolName = toolCall.Function.Name;

            if (!toolsLookup.ContainsKey(toolName))
                throw new InvalidOperationException($"{nameof(GetLlmResponse)} >> No function MethodInfo found for function name: {toolName}");
        }

        protected async Task<string> handleFunctionCall<TReq, TMessage>(TReq request, TMessage message, Dictionary<string, MethodInfo>? toolsLookup)
            where TReq : class
            where TMessage : class
        {
            validateFunctionCallMessage<TMessage>(message, toolsLookup);

            var ollamaRequest = request as ChatRequest;

            var ollamaMessage = message as Message;

            var toolCall = ollamaMessage.ToolCalls.FirstOrDefault();

            var toolName = toolCall.Function.Name;

            notifyToolCall(ollamaRequest.Model, toolName);

            var toolResult = await getToolResult(toolsLookup[toolName], toolCall.Function.Arguments);

            var messages = ollamaRequest.Messages.ToList();

            messages.AddRange(getToolResponseMessages(toolName, ollamaMessage, toolResult));

            ollamaRequest.Messages = messages;

            return await GetLlmResponse(ollamaRequest, toolsLookup);
        }

        protected async Task<string> handleThinking<TRequest, TMessage>(TRequest request, TMessage thinkMessage, Dictionary<string, MethodInfo>? toolsLookup)
        {
            //validateThinkMessage<TMessage>(thinkMessage)
            var ollamaMessage = thinkMessage as Message;
            var ollamaRequest = request as ChatRequest;

            if (string.IsNullOrEmpty(ollamaMessage.Thinking))
                throw new InvalidDataException($"{GetType().Name} >> {nameof(handleThinking)} >> No thinking content found in message");

            var messages = ollamaRequest.Messages.ToList();

            messages.Add(ollamaMessage);
            
            return await GetLlmResponse(ollamaRequest, toolsLookup);
        }

        protected void notifyThinking(string model, string thinking)
        {
            if (_onHandlerNotify != null)
                _onHandlerNotify.Invoke($"- LLM THINKING: {_provider}/{model}", thinking);
        }

        protected void notifyToolCall(string model, string toolName)
        {
            if (_onHandlerNotify != null)
                _onHandlerNotify.Invoke($"- LLM THINKING: {_provider}/{model}", toolName);
        }

        protected virtual List<Message> getToolResponseMessages(string toolName, Message toolRequestMessage, object? toolResult)
        {
            var messages = new List<Message>();

            var toolResponseMessage = new Message(ChatRole.Tool, JsonSerializer.Serialize(toolResult));

            toolResponseMessage.ToolName = toolName;

            messages.Add(toolRequestMessage);

            messages.Add(toolResponseMessage);

            return messages;
        }

        protected async Task<object?> getToolResult(MethodInfo methodInfo, IDictionary<string, object> arguments)
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
    }
}
