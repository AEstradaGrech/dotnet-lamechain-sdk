using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting
{
    /// <summary>
    /// Runs shell scripts on the machine where the API runs (the container when the API is in Docker, the host otherwise).
    /// The implementation (Bash or PowerShell) is chosen on startup by WithScriptExecutor().
    /// None of the methods throws for execution problems: they come back in <see cref="ScriptExecutionResult.Error"/>.
    /// </summary>
    public interface IScriptExecutor
    {
        /// <summary>
        /// Shell of this executor (Bash or PowerShell, never Auto): the language the scripts have to be written in
        /// </summary>
        EScriptShell Shell { get; }

        /// <summary>
        /// Saves the script ready to run (right extension, encoding, line endings and the shell header, if any) and returns its path.
        /// Optionally, saved the LLM response from the ScriptGeneratorCommand as JSON
        /// Optionally, save a generated script with a desired extension independently of the injected Executor (in case you are asking for a PowerShell script running the API on a Docker Linux container)
        /// <paramref name="folder"/> null: a new run_&lt;guid&gt; folder inside WorkRoot. Useful to propose a script and run it later
        /// </summary>
        Task<string> SaveScriptAsync(string script, EScriptShell? desiredExtension = null, string? generationJson = null, string? folder = null, CancellationToken ct = default);

        /// <summary>
        /// Runs a script saved with <see cref="SaveScriptAsync"/>. The working directory is the script folder
        /// </summary>
        Task<ScriptExecutionResult> ExecuteFileAsync(string scriptPath, CancellationToken ct = default);

        /// <summary>
        /// Saves the script in a new run folder and runs it
        /// </summary>
        Task<ScriptExecutionResult> ExecuteAsync(string script, CancellationToken ct = default);
    }
}
