

using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Scripting;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Scripting
{
    public class ScriptGeneratorCommand : DbPromptCommand<ScriptGeneratorResponse>
    {
        private readonly IScriptExecutor _executor;
        private EScriptShell? _requestedShell = null;
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
            validateInputRequest<ScriptGeneratorCommandRequest>(request);

            if (string.IsNullOrEmpty(request.Prompt))
                return new ScriptGeneratorResponse { Script = "Error >> Empty input provided, cannot generate shell Script." };

            _requestedShell = ((ScriptGeneratorCommandRequest)request).RequestedShell;

            request.Prompt = $"Write a script that performs the following task: {request.Prompt}";

            var instruction = await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend);
            var shell = targetShell();

            var response = await generate(request, instruction);

            // Deterministic check of what the prompt can only ask for: a script that reads parameters must come with the values to run it with.
            // One retry telling the model what was wrong; if it fails again, a clear error instead of a script that cannot run
            if (parameterMismatch(response, shell) is { } problem)
            {
                request.Prompt = $"{request.Prompt}\n\nIMPORTANT: your previous answer was not valid because {problem}.";

                response = await generate(request, instruction);

                if (parameterMismatch(response, shell) is { } secondProblem)
                    throw new InvalidOperationException($"{nameof(ScriptGeneratorCommand)} >> {secondProblem}");
            }

            if (_requestedShell == null && response.Shell.ToUpper() != $"{_executor.Shell}".ToUpper())
                throw new InvalidOperationException($"{nameof(ScriptGeneratorCommand)} >> Invalid generated script shell {response.Shell} for executor shell ({_executor.Shell})");

            // Only a folder name inside the executor WorkRoot, whatever the model wrote (paths, file names...)
            response.OutputFolder = SafeFolderName(response.OutputFolder);

            foreach (var argument in response.Arguments)
                argument.Name = NormalizeArgumentName(argument.Name);

            var savedResponse = JsonSerializer.Serialize<ScriptGeneratorResponse>(response, new JsonSerializerOptions { WriteIndented = true });

            response.ScriptPath = await _executor.SaveScriptAsync(response.Script, desiredExtension: _requestedShell, savedResponse, response.OutputFolder);

            return response;
        }

        private async Task<ScriptGeneratorResponse> generate(PromptCommandRequest request, string instruction)
            => await _ollama.CommandPrompt<ScriptGeneratorResponse>(
                request.ToOllamaChat(instruction, _settings),
                validation: _settings != null ? validatorFor<ScriptGeneratorResponse>(_settings.CommandValidations, _settings.ValidationType) : null);

        /// <summary>
        /// Null when the arguments match the parameters the script reads, otherwise the problem (written for the model, it goes in the retry)
        /// </summary>
        internal static string? parameterMismatch(ScriptGeneratorResponse response, EScriptShell shell)
        {
            var arguments = response.Arguments ?? [];
            var (required, max) = ScriptFormat.AnalyzeParameters(shell, response.Script);
            var given = arguments.Count;

            if (given < required)
                return $"the script reads {required} parameter(s) but arguments has {given} value(s). Put in arguments one {{name, value}} per parameter, in the same order, with the values the user wants to use now";

            if (max is int accepted && given > accepted)
                return accepted == 0 ?
                    $"arguments has {given} value(s) but the script does not read any parameter. Either declare the parameters in the script or write the values directly in the script and leave arguments empty" :
                    $"arguments has {given} value(s) but the script only reads {accepted} parameter(s)";

            if (arguments.Any(a => string.IsNullOrWhiteSpace(NormalizeArgumentName(a.Name))))
                return "every argument needs the name of its parameter";

            // Bash receives them by position: each name must be the variable the script stores that position in (folder="$1")
            if (shell == EScriptShell.Bash)
            {
                var variables = ScriptFormat.BashParameterNames(response.Script);

                for (var i = 0; i < arguments.Count; i++)
                    if (variables.TryGetValue(i + 1, out var variable) && !variable.Equals(NormalizeArgumentName(arguments[i].Name), StringComparison.OrdinalIgnoreCase))
                        return $"argument {i + 1} is named '{arguments[i].Name}' but the script stores ${i + 1} in the variable '{variable}'. Use the variable names of the script as argument names, in the same order";
            }

            // PowerShell receives the arguments by name (-Name value): they must be the parameters declared in param()
            if (shell == EScriptShell.PowerShell && ScriptFormat.PowerShellParameters(response.Script) is { Count: > 0 } declared)
            {
                var names = arguments.Select(a => NormalizeArgumentName(a.Name)).ToList();

                var unknown = names.Where(n => !declared.Any(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();

                if (unknown.Count > 0)
                    return $"the argument name(s) {string.Join(", ", unknown)} are not parameters of the script. The declared parameters are: {string.Join(", ", declared.Select(p => p.Name))}";

                var missing = declared.Where(p => p.Mandatory && !names.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).Select(p => p.Name).ToList();

                if (missing.Count > 0)
                    return $"the mandatory parameter(s) {string.Join(", ", missing)} have no argument";
            }

            return null;
        }

        // The model sometimes writes '$Name' or '-Name': the name of the parameter is what matters
        internal static string NormalizeArgumentName(string? name) => (name ?? string.Empty).Trim().TrimStart('$', '-').Trim();

        /// <summary>
        /// Folder name from whatever the model wrote ("network-checks", "/data/x/network-checks/script.sh", "..\\scripts"...): last folder segment,
        /// only letters, numbers, '-' and '_'. Null when nothing usable is left
        /// </summary>
        internal static string? SafeFolderName(string? requested)
        {
            if (string.IsNullOrWhiteSpace(requested)) return null;

            var segments = requested.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s is not "." and not "..")
                .ToList();

            // A script file name at the end is not a folder
            if (segments.Count > 0 && (segments[^1].EndsWith(".sh", StringComparison.OrdinalIgnoreCase) || segments[^1].EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)))
                segments.RemoveAt(segments.Count - 1);

            if (segments.Count == 0) return null;

            var name = Regex.Replace(segments[^1], "[^A-Za-z0-9_-]", "-").Trim('-');

            return string.IsNullOrEmpty(name) ? null : name;
        }

        private EScriptShell targetShell()
            => _requestedShell is EScriptShell.Bash or EScriptShell.PowerShell ? _requestedShell.Value
                : _executor?.Shell ?? (OperatingSystem.IsWindows() ? EScriptShell.PowerShell : EScriptShell.Bash);

        // The target shell may come in the request if the user wants to generate and save a script of a for a specific shell. If null, it comes from the executor: the script is always written for the shell that is going to run it,
        // and the prompt only carries the rules and examples of that shell
        protected override string getDefaultInstruction()
        {
            return targetShell() == EScriptShell.PowerShell ?
                buildInstruction("PowerShell", "a param() block at the very top of the script", "the parameter name exactly as declared in param(), without '$' (they are passed by name: -Name value)", _powerShellRules, _powerShellExamples) :
                buildInstruction("Bash", "$1, $2... checking the count with $#", "the name of the variable the script assigns each positional parameter to (folder=\"$1\" -> folder). They are passed in order: $1, $2...", _bashRules, _bashExamples);
        }

        private static string buildInstruction(string shell, string parameterSyntax, string argumentNames, string shellRules, string examples) => $$"""
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
- Write every identifier in English (variables, parameters, functions), whatever the language of the task. Only the text for the user follows the language of the task: summary, undo and the messages the script prints. Parameter names inside those messages (e.g. a usage message) are written exactly as in the script, never translated.
- Set output_folder only when the task explicitly asks to save the script in a folder: just the folder name, never a path.

# ARGUMENTS
- If the task asks for a reusable script or for parameters: declare them ({{parameterSyntax}}) and put in arguments one {"name", "value"} per parameter, in the same order, with the values the user wants to use NOW.
  Example: "use it now with chromadb and port 8000" -> [{"name": "host", "value": "chromadb"}, {"name": "port", "value": "8000"}]
- name: {{argumentNames}}. In English and exactly the same name the script uses.
- The script is executed with exactly these arguments: a script that expects parameters and gets an empty arguments list fails.
- Otherwise, write the values of the task directly in the script and leave arguments empty.

# REVIEW INFORMATION
- summary: what the script does and what the user should know before running it, in plain words and in the same language as the task.
- changes_system: true if the script modifies anything, false if it only reads information.
- undo: how to revert the changes, when the script makes any.

{{examples}}
""";

        private const string _powerShellRules = """
- Compatible with Windows PowerShell 5.1: do not use PowerShell 7-only syntax (??, ?:, &&, ||, ForEach-Object -Parallel).
- Start with $ErrorActionPreference = 'Stop' so any error stops the script. If the script has parameters, the param() block goes first and $ErrorActionPreference right after it.
- Add -Confirm:$false (and -Force when needed) to the commands that would ask for confirmation: the user approves the whole script beforehand.
- For tables, end with Format-Table -AutoSize | Out-String -Width 200 so the columns are not cut.
""";

        private const string _bashRules = """
- Start with #!/usr/bin/env bash and set -euo pipefail so any error stops the script.
- Use the non-interactive options of the commands that would ask for confirmation (-y, --yes, DEBIAN_FRONTEND=noninteractive): the user approves the whole script beforehand.
- Quote every variable expansion ("$var").
- If the script has parameters, assign each one to an English variable at the start (folder="$1", days="$2") and use those names in the usage message (<folder> <days>).
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

# EXAMPLE 3
## TASK:
Create a reusable script that receives the name of a process and shows how much memory each running instance of it uses. Use it now with dotnet.
## OUTPUT:
{"script": "param(\n    [Parameter(Mandatory = $true)]\n    [string]$ProcessName\n)\n\n$ErrorActionPreference = 'Stop'\n\n# Running instances of the process (no error when there is none)\n$processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue\n\nif (-not $processes) {\n    Write-Output \"No running process called '$ProcessName'\"\n    exit 1\n}\n\n# Memory used by each instance, in MB\n$processes |\n    Select-Object Id, ProcessName, @{ Name = 'MemoryMB'; Expression = { [math]::Round($_.WorkingSet64 / 1MB, 1) } } |\n    Sort-Object MemoryMB -Descending |\n    Format-Table -AutoSize |\n    Out-String -Width 200", "shell": "PowerShell", "summary": "Shows how much memory each running instance of a program uses, from highest to lowest. It receives the program name as a parameter and now it will run with dotnet. It only reads information and ends with an error code if the program is not running.", "changes_system": false, "requires_admin": false, "undo": null, "arguments": [{"name": "ProcessName", "value": "dotnet"}], "output_folder": null}
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

# EXAMPLE 3
## TASK:
Create a reusable script that receives a folder and a number of days, and lists without deleting anything the files in that folder and its subfolders older than those days, with their date and size. Use it now with /var/log and 7 days. Save it in the folder maintenance.
## OUTPUT:
{"script": "#!/usr/bin/env bash\nset -euo pipefail\n\n# Parameters: 1) folder to inspect  2) minimum age in days\nif [[ $# -ne 2 ]]; then\n    echo \"Usage: $0 <folder> <days>\" >&2\n    exit 2\nfi\nfolder=\"$1\"\ndays=\"$2\"\n\nif [[ ! -d \"$folder\" ]]; then\n    echo \"The folder $folder does not exist\" >&2\n    exit 1\nfi\n\n# List (never delete) the files older than the given days: date, size and path.\n# Subfolders the current user cannot read are skipped\necho \"Files in $folder older than $days days:\"\n{ find \"$folder\" -type f -mtime +\"$days\" -printf '%TY-%Tm-%Td  %10s bytes  %p\\n' 2>/dev/null || true; } | sort", "shell": "Bash", "summary": "Lists, without deleting anything, the files of a folder and its subfolders that are older than a number of days, with their date and size. It receives the folder and the days as parameters and now it will run with /var/log and 7 days. Subfolders your user cannot read are skipped.", "changes_system": false, "requires_admin": false, "undo": null, "arguments": [{"name": "folder", "value": "/var/log"}, {"name": "days", "value": "7"}], "output_folder": "maintenance"}
""";
    }
}
