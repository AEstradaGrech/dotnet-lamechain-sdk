using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EReasoning
    {
        [JsonStringEnumMemberName("none")]
        None,
        [JsonStringEnumMemberName("low")]
        Low,
        [JsonStringEnumMemberName("medium")]
        Medium,
        [JsonStringEnumMemberName("high")]
        High
    }
}
