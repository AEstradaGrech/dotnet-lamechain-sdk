using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs
{
    [OllamaJsonOutput("Python script generation", Description = "Self-contained Python script that solves the task plus the PyPI packages it needs")]
    public class PythonGeneratorResponse : StructuredOutput
    {
        [JsonPropertyName("code")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "The full Python script following the execution contract (final answer in `result`, files in OUTPUT_DIR)")]
        public string Code { get; set; }

        [JsonPropertyName("dependencies")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "Third-party packages imported by the script, using their PyPI install names")]
        [OllamaJsonRequirement("- Use PyPI names, not import names: scikit-learn (not sklearn), opencv-python (not cv2), pillow (not PIL), pyyaml (not yaml)")]
        [OllamaJsonRequirement("- NEVER include standard library modules (json, os, math, datetime, csv...)")]
        [OllamaJsonRequirement("- Empty list if the script only uses the standard library")]
        public List<string> Dependencies { get; set; } = [];
    }
}
