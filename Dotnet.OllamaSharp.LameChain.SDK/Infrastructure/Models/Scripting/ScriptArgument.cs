using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting
{
    /// <summary>
    /// One script parameter and the value the script is executed with.
    /// Shared by the ScriptGeneratorResponse (LLM output) and the IScriptExecutor (execution).
    /// A list keeps the order, which Bash needs ($1, $2...); PowerShell receives them by name (-Name value).
    /// </summary>
    public sealed class ScriptArgument
    {
        public ScriptArgument() { }
        public ScriptArgument(string name, string value)
        {
            Name = name;
            Value = value;
        }

        // PowerShell: the parameter name as declared in param(), without '$'. Bash: the name the script gives the positional parameter
        [JsonPropertyName("name")]
        [JsonRequired]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        [JsonRequired]
        public string Value { get; set; } = string.Empty;

        public override string ToString() => $"{Name} = {Value}";
    }
}
