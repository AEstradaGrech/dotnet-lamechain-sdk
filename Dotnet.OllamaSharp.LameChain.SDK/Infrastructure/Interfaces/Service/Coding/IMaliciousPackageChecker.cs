using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding
{
    public interface IMaliciousPackageChecker
    {
        /// <summary>
        /// Looks the dependencies up in a known malicious packages database. Never throws for network / service problems:
        /// they come back in <see cref="MaliciousPackageReport.Error"/> so the caller decides whether to fail open or closed.
        /// </summary>
        Task<MaliciousPackageReport> CheckAsync(IReadOnlyList<PythonDependency> dependencies, CancellationToken ct = default);
    }
}
