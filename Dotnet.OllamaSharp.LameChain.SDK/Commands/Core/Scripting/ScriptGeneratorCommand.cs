

using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Scripting
{
    public class ScriptGeneratorCommand : DbPromptCommand<ScriptGeneratorResponse>
    {
        private readonly IScriptExecutor _executor;
        public ScriptGeneratorCommand() : base() { }
        public ScriptGeneratorCommand(IOllamaInferenceService ollama, IScriptExecutor executor) : base(ollama) 
        { 
            _executor = executor ?? throw new ArgumentNullException($"{nameof(ScriptGeneratorCommand)} >> {nameof(IScriptExecutor)} >> Unable to save the generated script"); 
        }

        //This constructor is meant to support the usage of AtomicValueCommands w/DefaultInstruction and commands like the JsonRefiner (no instruction but orchestrates DB commands)
        public ScriptGeneratorCommand(IOllamaInferenceService ollama, IScriptExecutor executor, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings) 
        { 
            _executor = executor ?? throw new ArgumentNullException($"{nameof(ScriptGeneratorCommand)} >> {nameof(IScriptExecutor)} >> Unable to save the generated script"); 
        }

        public ScriptGeneratorCommand(IOllamaInferenceService ollama, IScriptExecutor executor, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) 
        { 
            _executor = executor ?? throw new ArgumentNullException($"{nameof(ScriptGeneratorCommand)} >> {nameof(IScriptExecutor)} >> Unable to save the generated script"); 
        }

        public async override Task<ScriptGeneratorResponse> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<PromptCommandRequest>(request);

            if (string.IsNullOrEmpty(request.Prompt))
                return new ScriptGeneratorResponse { Script = "Error >> Empty input provided, cannot generate shell Script." };

            request.Prompt = $"Write a script that performs the following task: {request.Prompt}";

            var response = await _ollama.CommandPrompt<ScriptGeneratorResponse>(
                request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings),
                validation: _settings != null ? validatorFor<ScriptGeneratorResponse>(_settings.CommandValidations, _settings.ValidationType) : null);

            if (response.Shell.ToUpper() != $"{_executor.Shell}".ToUpper())
                throw new InvalidOperationException($"{nameof(ScriptGeneratorCommand)} >> Invalid generated script shell {response.Shell} for executor shell ({_executor.Shell})");

            var savedResponse = JsonSerializer.Serialize<ScriptGeneratorResponse>(response, new JsonSerializerOptions { WriteIndented = true });

            await _executor.SaveScriptAsync(response.Script, savedResponse, response.OutputFolder);

            return response;
        }

        // The target shell comes from the executor: the script is always written for the shell that is going to run it,
        // and the prompt only carries the rules and examples of that shell
        protected override string getDefaultInstruction()
        {
            var shell = _executor?.Shell ?? (OperatingSystem.IsWindows() ? EScriptShell.PowerShell : EScriptShell.Bash);

            return shell == EScriptShell.PowerShell ?
                buildInstruction("PowerShell", _powerShellRules, _powerShellExamples) :
                buildInstruction("Bash", _bashRules, _bashExamples);
        }

        private static string buildInstruction(string shell, string shellRules, string examples) => $$"""
You are an expert system administrator. Write ONE {{shell}} script that performs the given task on the machine where it runs, and explain it to a user who is a programmer but NOT a system administrator.

# TARGET SHELL: {{shell}}
- Write the script ONLY for {{shell}}, even if the task mentions another shell.
{{shellRules}}

# EXECUTION CONTEXT
- The script runs non-interactively: there is no terminal and nobody can type anything.
- It runs with the permissions of the current user (NOT elevated) and with a time limit.
- Its working directory is the folder where it is saved. Only its standard output, standard error and exit code are captured.
- The user reads the script and your summary before approving its execution.

# SCRIPT RULES (mandatory)
- NEVER ask for input: no prompts, confirmations, pauses, password requests or interactive editors.
- NEVER elevate privileges (sudo, su, runas, Start-Process -Verb RunAs). If the task needs administrator or root rights, still write the script and set requires_admin to true.
- Do only what the task asks. For checks and diagnostics use read-only commands.
- NEVER download and run remote code, disable security features (firewall, antivirus, UAC, SELinux, execution policy) or delete data, unless the task explicitly and clearly asks for it.
- Use the tools that come with the operating system. If the task needs an optional tool, check that it exists first and print a clear message if it does not. Do not install software unless the task asks for it.
- Print clear, human-readable results: what was checked or changed, and the outcome.
- Exit with code 0 on success and with a non-zero code when the task could not be completed.
- Add a short comment before each logical block.
- Write the values given in the task directly in the script and leave arguments empty, unless the task asks for a reusable script with parameters.
- Set output_folder only when the task explicitly asks to save the script in a folder.

# REVIEW INFORMATION
- summary: what the script does and what the user should know before running it, in plain words and in the same language as the task.
- changes_system: true if the script modifies anything, false if it only reads information.
- undo: how to revert the changes, when the script makes any.

{{examples}}
""";

        private const string _powerShellRules = """
- Compatible with Windows PowerShell 5.1: do not use PowerShell 7-only syntax (??, ?:, &&, ||, ForEach-Object -Parallel).
- Start with $ErrorActionPreference = 'Stop' so any error stops the script.
- Add -Confirm:$false (and -Force when needed) to the commands that would ask for confirmation: the user approves the whole script beforehand.
- For tables, end with Format-Table -AutoSize | Out-String -Width 200 so the columns are not cut.
""";

        private const string _bashRules = """
- Start with #!/usr/bin/env bash and set -euo pipefail so any error stops the script.
- Use the non-interactive options of the commands that would ask for confirmation (-y, --yes, DEBIAN_FRONTEND=noninteractive): the user approves the whole script beforehand.
- Quote every variable expansion ("$var").
""";

        // Examples validated: valid JSON, the scripts parse (PowerShell 5.1 parser / bash -n) and example 1 runs without admin rights
        private const string _powerShellExamples = """
# EXAMPLE 1
## TASK:
List the TCP ports listening on this machine and the program behind each one.
## OUTPUT:
{"script": "$ErrorActionPreference = 'Stop'\n\n# Listening TCP ports with the program that owns each one\nGet-NetTCPConnection -State Listen |\n    Sort-Object LocalPort -Unique |\n    ForEach-Object {\n        $process = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue\n        [PSCustomObject]@{\n            Port    = $_.LocalPort\n            Address = $_.LocalAddress\n            PID     = $_.OwningProcess\n            Program = if ($process) { $process.ProcessName } else { 'unknown' }\n        }\n    } |\n    Format-Table -AutoSize |\n    Out-String -Width 200", "shell": "PowerShell", "summary": "Lists every TCP port that is waiting for connections on this machine and the program that opened it. It only reads information. Some system programs may show up as 'unknown' because the script does not run as administrator.", "changes_system": false, "requires_admin": false, "undo": null, "arguments": [], "output_folder": null}

# EXAMPLE 2
## TASK:
Create the user environment variable JAVA_HOME pointing to C:\Program Files\Java\jdk-21 and save the script in the folder java-setup.
## OUTPUT:
{"script": "$ErrorActionPreference = 'Stop'\n\n$path = 'C:\\Program Files\\Java\\jdk-21'\n\n# Warn if the folder does not exist (the variable is created anyway, as requested)\nif (-not (Test-Path $path)) { Write-Warning \"The folder $path does not exist\" }\n\n# User scope: no administrator rights needed. Terminals opened from now on will see it\n[Environment]::SetEnvironmentVariable('JAVA_HOME', $path, 'User')\n\nWrite-Output \"JAVA_HOME = $([Environment]::GetEnvironmentVariable('JAVA_HOME', 'User'))\"", "shell": "PowerShell", "summary": "Creates the JAVA_HOME environment variable for your user, pointing to C:\\Program Files\\Java\\jdk-21. It does not need administrator rights. Terminals and programs that are already open will not see it until you open them again.", "changes_system": true, "requires_admin": false, "undo": "[Environment]::SetEnvironmentVariable('JAVA_HOME', $null, 'User')", "arguments": [], "output_folder": "java-setup"}
""";

        private const string _bashExamples = """
# EXAMPLE 1
## TASK:
Check whether the services chromadb:8000 and ollama:11434 are reachable from this machine.
## OUTPUT:
{"script": "#!/usr/bin/env bash\nset -euo pipefail\n\n# Try a TCP connection to each host:port (bash built-in /dev/tcp, no extra tools needed)\nfailed=0\nfor target in chromadb:8000 ollama:11434; do\n    host=\"${target%%:*}\"\n    port=\"${target##*:}\"\n    if timeout 5 bash -c \"exec 3<>/dev/tcp/$host/$port\" 2>/dev/null; then\n        echo \"OK      $host:$port is reachable\"\n    else\n        echo \"FAILED  $host:$port is not reachable\"\n        failed=1\n    fi\ndone\n\nexit \"$failed\"", "shell": "Bash", "summary": "Checks whether this machine can open a connection to chromadb on port 8000 and to ollama on port 11434, and prints OK or FAILED for each one. It only reads information. It ends with an error code if any of them is not reachable.", "changes_system": false, "requires_admin": false, "undo": null, "arguments": [], "output_folder": null}

# EXAMPLE 2
## TASK:
Create the folder reports in my home directory, readable and writable only by me.
## OUTPUT:
{"script": "#!/usr/bin/env bash\nset -euo pipefail\n\nfolder=\"$HOME/reports\"\n\n# Create the folder if it does not exist\nmkdir -p \"$folder\"\n\n# Owner: read, write and enter. Group and others: nothing\nchmod 700 \"$folder\"\n\nls -ld \"$folder\"", "shell": "Bash", "summary": "Creates the folder reports in your home directory (if it does not exist yet) and makes it accessible only to your user: nobody else can open it or read its content.", "changes_system": true, "requires_admin": false, "undo": "chmod 755 ~/reports to give read access back to other users, or rmdir ~/reports to remove it while it is empty", "arguments": [], "output_folder": null}
""";
    }
}
