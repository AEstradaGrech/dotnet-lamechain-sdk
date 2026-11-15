using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;

namespace Dotnet.OllamaSharp.LameChain.SDK.Services.Command
{
    public class CommandSettingsService : ICommandSettingsService
    {
        public CommandSettings PresetByEnum(ESettingsPreset preset, string? model = null, int contextLength = 10000, int maxTokens = 1000)
            => preset switch {
                ESettingsPreset.FOCUSED => new CommandSettings {
                    Model = model,
                    ContextLength = contextLength,
                    MaxTokens = maxTokens,
                    Temperature = 0.1f,
                    TopP = 0.3f,
                    TopK = 20,
                    RepeatPenalty = 0.9f,
                    RepeatLastN = 2147483647
                },
                ESettingsPreset.MEDIUM => new CommandSettings
                {
                    Model = model,
                    ContextLength = contextLength,
                    MaxTokens = maxTokens,
                    Temperature = 0.5f,
                    TopP = 0.5f,
                    TopK = 40,
                    RepeatPenalty = 1.0f,
                    RepeatLastN = 2147483647
                },
                ESettingsPreset.CREATIVE => new CommandSettings
                {
                    Model = model,
                    ContextLength = contextLength,
                    MaxTokens = maxTokens,
                    Temperature = 1.0f,
                    TopP = 0.9f,
                    TopK = 60,
                    RepeatPenalty = 1.1f,
                    RepeatLastN = 2147483647
                },
                _ => new CommandSettings
                {
                    Model = model,
                    ContextLength = contextLength,
                    MaxTokens = maxTokens,
                    Temperature = 0.7f,
                    TopP = 0.6f,
                    TopK = 40,
                    RepeatPenalty = 1.0f,
                    RepeatLastN = 2147483647
                }
            };
    }
}
