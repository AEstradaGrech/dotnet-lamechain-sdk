
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration
{
    public class CommandSettings : PromptSettings
    {
        public CommandSettings() : base() { }
        public CommandSettings(string? model) { Model = model; }
        public CommandSettings(int maxTokens, float temperature, string? model = null) : base(maxTokens, temperature, model) { }
        public CommandSettings(int maxTokens, float temperature, int contextLength, string? model = null) : base(maxTokens, temperature, contextLength, model) { }
        public CommandSettings(int maxTokens, float temperature, float topP, string? model = null) : base(maxTokens, temperature, topP, model) { }
        public CommandSettings(int maxTokens, float temperature, float topP, int topK, string? model = null) : base(maxTokens, temperature, topP, topK, model) { }
        public int CommandValidations { get; set; } = 0;
        public EPromptValidation ValidationType { get; set; }
        public bool UseDefaultCommandMessage { get; set; }
    }
}
