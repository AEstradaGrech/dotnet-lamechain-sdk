using Microsoft.Extensions.Configuration;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public abstract class BaseHandler
    {
        protected readonly IConfiguration _config;
        protected readonly IServiceProvider _serviceProvider;
        private ChatRequest _commandRequest;
        protected readonly string _provider;

        public ChatRequest CommandRequest => _commandRequest;
        public bool IsProvider(string provider) => _provider == provider;

        public BaseHandler(IServiceProvider serviceProvider, IConfiguration config, ChatRequest commandRequest, string provider)
        {
            _provider = provider.Trim();

            if (string.IsNullOrEmpty(_provider))
                throw new InvalidDataException($"{GetType().Name} >> LLM provider name is empty");

            _config = config;
            _serviceProvider = serviceProvider;
            _commandRequest = commandRequest;
        }

        public bool IsOfType<T>() where T : BaseHandler => this is T;
        public T AsType<T>() where T : BaseHandler => (T)this;
        public BaseHandler UpdateHandler(string provider, ChatRequest commandRequest)
            => provider switch {
                "ollama" => new OllamaHandler(_serviceProvider, _config, commandRequest),
                "claude" => new ClaudeHandler(_serviceProvider, _config, commandRequest),
                _ => new OllamaHandler(_serviceProvider, _config, commandRequest)
            };

        public void SetCommandRequest(ChatRequest request)
        {
            _commandRequest = request;
        }
        public abstract Task<string> GetLlmResponse(); // override & serviceProvider.GetRequiredService<HandlerClient>() <- cada uno pide una copia de SU http client

        protected virtual bool isValid()
            => _config != null && _commandRequest != null;
    }
}
