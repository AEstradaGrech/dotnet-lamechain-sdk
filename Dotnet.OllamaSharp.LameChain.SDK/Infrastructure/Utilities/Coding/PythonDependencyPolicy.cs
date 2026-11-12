using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using System.Text.RegularExpressions;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding
{
    internal sealed record PythonDependencySelection(IReadOnlyList<PythonDependency> Accepted, IReadOnlyList<string> Errors);

    /// <summary>
    /// Validates the dependencies returned by the generator before anything is installed:
    /// only plain PyPI requirements (name, extras, version specifiers), no stdlib modules, no duplicates,
    /// and the Excluded / Allowed packages configuration.
    /// Error messages are meant to be fed back to the code generator.
    /// </summary>
    internal static partial class PythonDependencyPolicy
    {
        public static PythonDependencySelection Select(IEnumerable<string>? requested, PythonExecutionOptions options)
        {
            var accepted = new List<PythonDependency>();
            var errors = new List<string>();
            var seen = new HashSet<string>();
            var excluded = toNameSet(options.ExcludedPackages);
            var allowed = toNameSet(options.AllowedPackages);

            foreach (var raw in requested ?? [])
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var match = RequirementRegex().Match(raw.Trim());

                if (!match.Success)
                {
                    errors.Add($"'{raw}' is not a valid PyPI requirement. Use a package name with an optional version specifier (e.g. 'pandas' or 'pandas>=2'), no URLs, paths or flags");
                    continue;
                }

                var name = match.Groups["name"].Value;
                var normalized = Normalize(name);

                // Standard library module: nothing to install
                if (PythonStdlib.Contains(name)) continue;

                if (!seen.Add(normalized)) continue;

                if (excluded.Contains(normalized))
                {
                    errors.Add($"'{name}' is excluded by configuration. Use another library");
                    continue;
                }

                if (allowed.Count > 0 && !allowed.Contains(normalized))
                {
                    errors.Add($"'{name}' is not allowed. Allowed packages: {string.Join(", ", options.AllowedPackages!)}");
                    continue;
                }

                accepted.Add(new PythonDependency(name, WhitespaceRegex().Replace(match.Value, string.Empty)));
            }

            return new PythonDependencySelection(accepted, errors);
        }

        // PEP 503 normalization: "Scikit_Learn" == "scikit-learn"
        public static string Normalize(string name) => SeparatorsRegex().Replace(name.Trim(), "-").ToLowerInvariant();

        private static HashSet<string> toNameSet(IEnumerable<string>? packages)
            => (packages ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => RequirementRegex().Match(p.Trim()) is { Success: true } m ? m.Groups["name"].Value : p)
                .Select(Normalize)
                .ToHashSet();

        // PEP 508 subset: name + optional [extras] + optional version specifiers. No URLs, paths, markers or command line flags
        [GeneratedRegex(@"^(?<name>[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?)\s*(?:\[\s*[A-Za-z0-9._-]+(?:\s*,\s*[A-Za-z0-9._-]+)*\s*\])?\s*(?:(?:===?|!=|<=|>=|~=|<|>)\s*[A-Za-z0-9.*+!_-]+(?:\s*,\s*(?:===?|!=|<=|>=|~=|<|>)\s*[A-Za-z0-9.*+!_-]+)*)?$")]
        private static partial Regex RequirementRegex();

        [GeneratedRegex(@"[-_.]+")]
        private static partial Regex SeparatorsRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}
