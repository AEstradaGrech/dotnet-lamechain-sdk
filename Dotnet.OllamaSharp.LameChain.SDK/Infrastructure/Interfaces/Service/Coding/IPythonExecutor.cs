using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding
{
    public interface IPythonExecutor
    {
        /// <summary>
        /// Runs a generated Python script and returns its result envelope. Never throws for execution problems:
        /// they come back in <see cref="ExecutionResults.Error"/> (DependencyError, Timeout, python exceptions...).
        /// The implementation (uv or local python) is chosen on startup by WithPythonExecutor().
        /// </summary>
        /// <param name="code">Python script following the execution contract (final answer in `result`, files in OUTPUT_DIR)</param>
        /// <param name="dependencies">PyPI requirements of the script (e.g. "pandas", "matplotlib>=3.8")</param>
        Task<ExecutionResults> ExecuteAsync(string code, IReadOnlyList<string> dependencies, CancellationToken ct = default);
    }
}
