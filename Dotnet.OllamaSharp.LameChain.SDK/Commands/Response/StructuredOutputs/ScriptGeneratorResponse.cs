using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes;
using System.Text.Json.Serialization;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs
{
    // NOTE: Summary, ChangesSystem, RequiresAdmin and Undo are declared by the LLM: useful to inform the user before
    // approving a script, NEVER a security control by themselves
    [OllamaJsonOutput("Shell script generation", Description = "A shell script that performs a system administration task, plus the information a non-expert user needs to review it before approving its execution")]
    public class ScriptGeneratorResponse : StructuredOutput
    {
        [JsonPropertyName("script")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "The full script, written ONLY for the target shell given in the instructions")]
        [OllamaJsonRequirement("- Non-interactive: it must never ask for input, confirmation or passwords")]
        [OllamaJsonRequirement("- No privilege elevation (sudo, su, RunAs): if administrator rights are needed, set requires_admin to true instead")]
        public string Script { get; set; } = string.Empty;

        [JsonPropertyName("shell")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "Shell the script is written for")]
        [OllamaJsonRequirement("- Exactly the target shell given in the instructions: 'PowerShell' or 'Bash'")]
        public string Shell { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "What the script does, explained for a programmer who is not a system administrator")]
        [OllamaJsonRequirement("- 2 to 5 short sentences, in the same language as the task")]
        [OllamaJsonRequirement("- Say what it checks or changes, and anything the user should know before running it")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("changes_system")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "true if the script modifies anything: files, folders, users, permissions, environment variables, services, scheduled tasks, network or firewall settings, installed software. false if it only reads information")]
        public bool ChangesSystem { get; set; }

        [JsonPropertyName("requires_admin")]
        [JsonRequired]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "true if the script needs administrator (Windows) or root (Linux) rights to work")]
        public bool RequiresAdmin { get; set; }

        [JsonPropertyName("undo")]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "How to revert the changes made by the script: a command or short instructions, in the same language as the task")]
        [OllamaJsonRequirement("- null when changes_system is false")]
        [OllamaJsonRequirement("- If the changes cannot be reverted, say it here and in the summary")]
        public string? Undo { get; set; }

        [JsonPropertyName("arguments")]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "Values passed to the script parameters when it runs, in order")]
        [OllamaJsonRequirement("- Usually an empty list: write the values of the task directly in the script")]
        [OllamaJsonRequirement("- Only for scripts the task asks to be reusable, with declared parameters (PowerShell param() block / Bash $1, $2...)")]
        public List<string> Arguments { get; set; } = [];

        [JsonPropertyName("output_folder")]
        [OllamaJsonProperty(Title = "Description", PromptDescription = "Name of the folder where the task asks to save the script")]
        [OllamaJsonRequirement("- null when the task does not ask for a folder")]
        [OllamaJsonRequirement("- A short relative name (letters, numbers, '-' and '_'), never an absolute path, a drive letter or '..'")]
        public string? OutputFolder { get; set; }
    }
}
