using Microsoft.Extensions.Configuration;
using OllamaSharp.Models.Chat;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.InferenceHandlers
{
    public abstract class BaseHandler
    {
        protected readonly IConfiguration _config;
        protected readonly IServiceProvider _serviceProvider;
        protected readonly string _provider;

        public bool IsProvider(string provider) => _provider == provider;

        public BaseHandler(IServiceProvider serviceProvider, IConfiguration config, string provider)
        {
            _provider = provider.Trim();

            if (string.IsNullOrEmpty(_provider))
                throw new InvalidDataException($"{GetType().Name} >> LLM provider name is empty");

            _config = config;
            _serviceProvider = serviceProvider;
        }

        public bool IsOfType<T>() where T : BaseHandler => this is T;
        public T AsType<T>() where T : BaseHandler => (T)this;
        public BaseHandler UpdateHandler(string provider)
            => provider switch {
                "ollama" => new OllamaHandler(_serviceProvider, _config),
                "claude" => new ClaudeHandler(_serviceProvider, _config),
                "groq" => new GroqHandler(_serviceProvider, _config),
                _ => new OllamaHandler(_serviceProvider, _config)
            };

        public abstract Task<string> GetLlmResponse(ChatRequest request, Dictionary<string, MethodInfo>? requestTools = null); // override & serviceProvider.GetRequiredService<HandlerClient>() <- cada uno pide una copia de SU http client

        protected virtual bool isValid()
            => _serviceProvider != null && _config != null && !string.IsNullOrEmpty(_provider);
    }
}
