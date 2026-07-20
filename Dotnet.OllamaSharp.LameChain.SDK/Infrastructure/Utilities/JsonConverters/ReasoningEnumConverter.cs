using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.JsonConverters
{
    public class ReasoningEnumConverter : JsonStringEnumConverter<EReasoning>
    {
    }
}
