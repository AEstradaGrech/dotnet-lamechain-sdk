using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding;
using System.Net.Http.Json;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding
{
    /// <summary>
    /// Checks the dependencies against OSV.dev (Google's open source vulnerabilities database), which includes the malicious
    /// packages reported by the OpenSSF (ids starting with "MAL-"). One batch request for all the dependencies.
    ///
    /// Any MAL- report blocks the whole package: the versions listed in those reports are only the ones known when they were
    /// reported, a new version of a malicious package is just as malicious. Regular vulnerabilities (GHSA / PYSEC) are not blocked.
    /// </summary>
    public sealed class OsvMaliciousPackageChecker : IMaliciousPackageChecker
    {
        public const string HttpClientName = "lamechain-osv";
        private const string _maliciousPrefix = "MAL-";
        private const string _ecosystem = "PyPI";
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        private readonly IHttpClientFactory _httpFactory;

        public OsvMaliciousPackageChecker(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        }

        public async Task<MaliciousPackageReport> CheckAsync(IReadOnlyList<PythonDependency> dependencies, CancellationToken ct = default)
        {
            if (dependencies.Count == 0) return new MaliciousPackageReport([], null);

            try
            {
                var query = new OsvBatchQuery([.. dependencies.Select(d => new OsvQuery(new OsvPackage(PythonDependencyPolicy.Normalize(d.Name), _ecosystem)))]);

                using var response = await _httpFactory.CreateClient(HttpClientName).PostAsJsonAsync("v1/querybatch", query, _json, ct);

                response.EnsureSuccessStatusCode();

                var batch = await response.Content.ReadFromJsonAsync<OsvBatchResponse>(_json, ct);

                // OSV returns one result per query, in the same order
                if (batch?.Results == null || batch.Results.Count != dependencies.Count)
                    return new MaliciousPackageReport([], $"Unexpected OSV.dev response ({batch?.Results?.Count ?? 0} results for {dependencies.Count} packages)");

                var malicious = dependencies
                    .Select((dependency, i) => new MaliciousPackage(dependency.Name,
                        [.. (batch.Results[i].Vulns ?? []).Select(v => v.Id).Where(id => id.StartsWith(_maliciousPrefix, StringComparison.OrdinalIgnoreCase))]))
                    .Where(package => package.AdvisoryIds.Count > 0)
                    .ToList();

                return new MaliciousPackageReport(malicious, null);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
            {
                return new MaliciousPackageReport([], ex.Message);
            }
        }

        private sealed record OsvBatchQuery(IReadOnlyList<OsvQuery> Queries);
        private sealed record OsvQuery(OsvPackage Package);
        private sealed record OsvPackage(string Name, string Ecosystem);
        private sealed record OsvBatchResponse(List<OsvBatchResult>? Results);
        private sealed record OsvBatchResult(List<OsvVulnerabilityId>? Vulns);
        private sealed record OsvVulnerabilityId(string Id);
    }
}
