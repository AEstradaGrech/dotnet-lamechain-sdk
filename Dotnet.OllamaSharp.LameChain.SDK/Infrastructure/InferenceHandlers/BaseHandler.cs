using Microsoft.Extensions.Configuration;
using OllamaSharp.Models.Chat;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public abstract class BaseHandler
    {
        protected readonly IConfiguration _config;
        protected readonly IServiceProvider _serviceProvider;
        private ChatRequest _commandRequest;
        protected readonly string _provider;
        protected Dictionary<string, MethodInfo>? _toolsLookup = null;
        public ChatRequest CommandRequest => _commandRequest;
        public bool HasTools => _toolsLookup != null && _toolsLookup.Count() > 0;
        public bool IsProvider(string provider) => _provider == provider;

        public BaseHandler(IServiceProvider serviceProvider, IConfiguration config, ChatRequest commandRequest, string provider, Dictionary<string, MethodInfo>? toolsLookup = null)
        {
            _provider = provider.Trim();

            if (string.IsNullOrEmpty(_provider))
                throw new InvalidDataException($"{GetType().Name} >> LLM provider name is empty");

            _config = config;
            _serviceProvider = serviceProvider;
            _commandRequest = commandRequest;
            _toolsLookup = toolsLookup;
        }

        public bool IsOfType<T>() where T : BaseHandler => this is T;
        public T AsType<T>() where T : BaseHandler => (T)this;
        public BaseHandler UpdateHandler(string provider, ChatRequest commandRequest, Dictionary<string, MethodInfo>? requestTools = null)
            => provider switch {
                "ollama" => new OllamaHandler(_serviceProvider, _config, commandRequest, requestTools),
                "claude" => new ClaudeHandler(_serviceProvider, _config, commandRequest, requestTools),
                "groq" => new GroqHandler(_serviceProvider, _config, commandRequest, requestTools),
                _ => new OllamaHandler(_serviceProvider, _config, commandRequest, requestTools)
            };

        public void SetCommandRequest(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null)
        {
            _commandRequest = request;
            _toolsLookup = requestTools;
        }
        public abstract Task<string> GetLlmResponse(); // override & serviceProvider.GetRequiredService<HandlerClient>() <- cada uno pide una copia de SU http client

        protected virtual bool isValid()
            => _config != null && _commandRequest != null;
    }
}
