using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding
{
    public interface IPythonExecutor
    {
        Task<PythonExecutionResult> ExecuteAsync(string code, PythonExecutionOptions options, CancellationToken ct = default);
    }
}
