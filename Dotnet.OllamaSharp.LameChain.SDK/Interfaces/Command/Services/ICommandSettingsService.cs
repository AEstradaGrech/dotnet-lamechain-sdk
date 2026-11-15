
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services
{
    public interface ICommandSettingsService
    {
        CommandSettings PresetByEnum(ESettingsPreset preset, string? model = null, int contextLength = 10000, int maxTokens = 1000);
    }
}
