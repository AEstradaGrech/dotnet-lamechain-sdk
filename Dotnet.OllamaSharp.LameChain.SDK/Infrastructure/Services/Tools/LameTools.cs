

using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text.Json;
using Chain = Dotnet.OllamaSharp.LameChain.SDK.Extensions.LameChain;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools
{
    public class LameTools : ToolsService<LameTools>
    {
        public LameTools() : base() { }
        public LameTools(IServiceProvider serviceProvider) : base(serviceProvider) { }

        // Add this tool to any agent or command
        [Description("Solves a task by writing and running a Python script by itself. You do NOT write any code: describe in plain language WHAT has to be done and this tool generates the Python code, runs it and returns the result. Use it for calculations, statistics, data processing (Excel, CSV...) and charts.")]
        public async Task<string> PythonExecutorTool(
            [Description("Definition of the task to be done based on the user request. NEVER Python code (this will be used to generate the python code to execute)")]
            string goal,
            [Description("ALL the data the task needs, copied verbatim from the user's messages: every value, list and table, or the absolute paths of the input files as the API sees them (e.g. /data/Resources/sales.xlsx). Do not summarize or round it. Empty only if the task needs no input data")]
            string? inputData = null)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();

            var task = string.IsNullOrWhiteSpace(inputData) ? goal : $"{goal}\n\nInput data:\n{inputData}";

            var result = await Chain.StartWith(
                new StepSettings(
                        factory.GetPythonGeneratorCommand(),
                        new PromptCommandRequest(message: task)
                    ),
                    defaultSettings: new CommandSettings(),
                    chainIntent: "Generate and execute Python code")
                .ExecutePython(
                    new StepSettings(
                        factory.GetPythonExecutorCommand(),
                        new PythonCodingRequest())
                )
                .ExecuteChainAsync();
            
            return result.ResultAs<ChatMessage>().Content;
        }

        [Description("Generates a shell script according to the user request and returns the generation params including the path to the generated script. ALWAYS inform the user of the file location found in the resulting 'ScriptPath'")]
        public async Task<string> ShellGeneratorTool(
           [Description("The user script request copied verbatim to be passed to the script generator")]
            string userScriptRequest,
           [Description("The shell type requested by the user (if any). Options: PowerShell | Bash. Pass null when no specific shell has been requested")] 
            string? requestedShell = null)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();

            var settingsService = _services.GetRequiredService<ICommandSettingsService>();

            var command = factory.GetScriptGeneratorCommand(settings: settingsService.PresetByEnum(ESettingsPreset.MEDIUM));

            EScriptShell? shellType = null;
            if(!string.IsNullOrEmpty(requestedShell))
            {
                if (!string.IsNullOrEmpty(requestedShell.Trim()))
                {
                    var comparisonTag = requestedShell.ToUpper();

                    shellType = comparisonTag == $"{EScriptShell.PowerShell}".ToUpper() ? EScriptShell.PowerShell : comparisonTag == $"{EScriptShell.Bash}".ToUpper() ? EScriptShell.Bash : null;
                }
            }

            var response = await command.Prompt(new ScriptGeneratorCommandRequest(userScriptRequest, shellType));

            return $"{JsonSerializer.Serialize<ScriptGeneratorResponse>(response)}\n# SCRIPT PATH: {response.ScriptPath}";
        }

        [Description("Executes the requested shell script on the app host machine. NEVER use this tool without a EXPLICIT APPROVAL from the user.")]
        public async Task<string> ShellExecutorTool(
           [Description("The path to the script to execute, taken from the user input")]
            string scriptPath)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();

            var settingsService = _services.GetRequiredService<ICommandSettingsService>();

            var command = factory.GetScriptExecutorCommand(settings: settingsService.PresetByEnum(ESettingsPreset.MEDIUM));

            var result = await command.Prompt(new ScriptExecutionCommandRequest(scriptPath));

            return result.Content;
        }

        // For bigger / smart models
        [Description("Think before acting. Writes an inner reflection about a request: the user's real intent, missing information, possible approaches (and which ones to avoid), risks worth warning the user about, and what a good result looks like. It does NOT solve the task. Use it at the start of ambiguous, complex or risky requests, or to rethink after a failed attempt. Skip it for simple, clear requests. This tool cannot see the conversation: pass everything relevant in the parameters.")]
        public async Task<string> AdvancedReasoningTool(
        [Description("The user's request copied verbatim, exactly as written. Do not summarize, translate or rephrase it.")]
        string userRequest,
        [Description("Everything relevant from the conversation that is not in the request itself: earlier messages, user preferences and constraints, files or data mentioned, results of previous tool calls or attempts. Empty if the request is self-contained.")]
        string? context = null,
        [Description("A specific question or concern to reflect on, e.g. 'Is the package X safe to use?' or 'Why did the last script fail with a KeyError?'. Empty for a general reflection.")]
        string? focus = null)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();
            var settingsService = _services.GetRequiredService<ICommandSettingsService>();
            var command = factory.GetMessagePromptCommand(LamePrompts.REFLECTION_SYSTEM, settingsService.PresetByEnum(ESettingsPreset.CREATIVE));

            var message = string.Join("\n",
                tag("user_request", userRequest),
                tag("context", context, "No additional context."),
                tag("focus", focus, "General reflection."),
                "Reflect on this request.");

            var result = await command.Prompt(new ChatCommandRequest(message, guidanceMessage: null));

            return result.Content;
        }

        // For bigger / smart models
        [Description("Generates a Chain-of-Thought: a numbered list of thinking steps, tailored to the task, to follow BEFORE writing your final response so it is more focused, complete and accurate. It does NOT answer and does NOT execute actions. Use it when the quality of the answer depends on careful reasoning (analysis, comparisons, explanations, decisions, in-character replies). Follow the returned steps in order.")]
        public async Task<string> AdvancedChainOfThoughtTool(
    [Description("The user's request copied verbatim, exactly as written.")]
    string userRequest,
    [Description("What your final response must achieve, in one or two sentences. Example: 'Explain to a beginner why the loop never ends, with a corrected example'.")]
    string goal,
    [Description("Names of the information sources you already have and can use while reasoning, e.g. 'character profile', 'retrieved docs about Docker volumes', 'output of the last Python run'. List them, do not paste their content. Empty if you only have the conversation.")]
    string? availableContext = null,
    [Description("Constraints for the final response: format, length, tone, audience, language. Empty if none.")]
    string? responseRequirements = null)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();
            var settingsService = _services.GetRequiredService<ICommandSettingsService>();
            var command = factory.GetMessagePromptCommand(LamePrompts.COT_SYSTEM, settingsService.PresetByEnum(ESettingsPreset.MEDIUM));

            var message = string.Join("\n",
                tag("user_request", userRequest),
                tag("goal", goal),
                tag("available_context", availableContext, "Only the user request and the conversation history."),
                tag("response_requirements", responseRequirements, "Not specified."),
                "Write the Chain-of-Thought for this task.");

            var result = await command.Prompt(new ChatCommandRequest(message, guidanceMessage: null));

            return result.Content;
        }

        // For dumb models (less params)
        [Description("Think before acting: reflects on the user's request (real intent, missing information, best approach, risks to warn about). It does NOT solve the task. Use it for ambiguous, complex or risky requests.")]
        public async Task<string> ReasoningTool(
        [Description("The user's message, exactly as written.")]
        string userRequest,
        [Description("Optional doubt to think about. Example: 'Is the package X safe to use?'")]
        string? focus = null)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();
            var settingsService = _services.GetRequiredService<ICommandSettingsService>();
            var command = factory.GetMessagePromptCommand(LamePrompts.REFLECTION_SYSTEM_LITE, settingsService.PresetByEnum(ESettingsPreset.CREATIVE));

            var message = string.Join("\n",
                tag("user_request", userRequest),
                tag("focus", focus, "General reflection."),
                "Reflect on this request.");

            var result = await command.Prompt(new ChatCommandRequest(message, guidanceMessage: null));

            return result.Content;
        }

        // For dumb models (less params)
        [Description("Returns a step-by-step thinking guide to follow before writing your answer. It does NOT answer. Use it when the answer needs careful reasoning.")]
        public async Task<string> ChainOfThoughtTool(
    [Description("The user's message, exactly as written.")]
    string userRequest,
    [Description("What your answer must achieve. Example: 'Explain why the loop never ends'")]
    string goal,
    [Description("Optional format, length or tone for the answer. Example: 'short, friendly, in Spanish'")]
    string? responseRequirements = null)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();
            var settingsService = _services.GetRequiredService<ICommandSettingsService>();
            var command = factory.GetMessagePromptCommand(LamePrompts.COT_SYSTEM_LITE, settingsService.PresetByEnum(ESettingsPreset.MEDIUM));

            var message = string.Join("\n",
                tag("user_request", userRequest),
                tag("goal", goal),
                tag("response_requirements", responseRequirements, "Not specified."),
                "Write the thinking steps for this request as specified in your instructions.");

            var result = await command.Prompt(new ChatCommandRequest(message, guidanceMessage: null));

            return result.Content;
        }

        [Description("Creates an ordered, actionable execution plan for a task that needs several steps or tool calls: concrete steps grouped into milestones, the kind of tool needed in each step, dependencies, completion criteria and risks. It does NOT execute anything. Use it before starting multi-step work, and again to re-plan when a step fails or new information changes the situation. Then carry out the plan step by step, checking each 'Done when' criterion.")]
        public async Task<string> TaskPlanningTool(
    [Description("The user's request copied verbatim, exactly as written.")]
    string userRequest,
    [Description("The verifiable final outcome: what must exist or be true when the task is finished. Example: 'A markdown file /out/report.md stating whether unit tests A, B and C compile, with the errors found'.")]
    string goal,
    [Description("Known facts the plan must rely on: files and paths, data, environment, user constraints and preferences, results already obtained. Empty if none.")]
    string? context = null,
    [Description("Only when re-planning: the previous plan with the status of each step (done / failed / pending) and what went wrong. Empty for a new plan.")]
    string? progress = null)
        {
            var factory = _services.GetRequiredService<IPromptCommandsFactory>();
            var settingsService = _services.GetRequiredService<ICommandSettingsService>();
            var command = factory.GetMessagePromptCommand(LamePrompts.PLAN_SYSTEM, settingsService.PresetByEnum(ESettingsPreset.FOCUSED));

            var message = string.Join("\n",
                tag("user_request", userRequest),
                tag("goal", goal),
                tag("context", context),
                tag("progress", progress, "None: this is a new plan."),
                "Write the execution plan.");

            var result = await command.Prompt(new ChatCommandRequest(message, guidanceMessage: null));

            return result.Content;
        }

        private string tag(string name, string? value, string fallback = "none")
            => $"<{name}>\n{(string.IsNullOrWhiteSpace(value) ? fallback : value.Trim())}\n</{name}>";

        
    }
}
