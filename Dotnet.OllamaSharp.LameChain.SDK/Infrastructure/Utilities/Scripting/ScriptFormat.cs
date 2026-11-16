using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using System.Text;
using System.Text.RegularExpressions;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Scripting
{
    internal sealed record PowerShellParameter(string Name, bool Mandatory, bool IsSwitch);

    /// <summary>
    /// File format of each shell (content, encoding, extension) and a light analysis of the script parameters.
    /// Used by the executors to save scripts for any shell (the user may ask for a script of another shell to run it on their machine)
    /// and by the ScriptGeneratorCommand to check that the generated arguments match the parameters the script reads.
    /// </summary>
    internal static partial class ScriptFormat
    {
        // Added to every PowerShell script (it is saved in the file, so what is reviewed is exactly what runs).
        // UTF-8 output: Windows PowerShell writes redirected output in the console code page otherwise (accents get broken).
        // No progress bars: with redirected output they end up in stderr as noise.
        public const string PowerShellHeader = """
            # --- LameChain: UTF-8 output and no progress bars (they break redirected output) ---
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            $ProgressPreference = 'SilentlyContinue'
            # ---
            """;

        public static string Extension(EScriptShell shell) => shell == EScriptShell.PowerShell ? ".ps1" : ".sh";

        // Windows PowerShell 5.1 reads .ps1 files without BOM in the ANSI code page: with the BOM accents are not broken.
        // Bash does not want a BOM (it would be part of the shebang)
        public static Encoding FileEncoding(EScriptShell shell) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: shell == EScriptShell.PowerShell);

        public static string Prepare(EScriptShell shell, string script) => shell == EScriptShell.PowerShell ? PreparePowerShell(script) : PrepareBash(script);

        // CRLF breaks bash ($'\r': command not found): scripts written on Windows or by an LLM may come with it
        public static string PrepareBash(string script) => script.ReplaceLineEndings("\n");

        // The param() block must be the first statement of a PowerShell script: the header goes right after it when there is one
        // (before it, PowerShell takes 'param' as an unknown command and the parameters are never bound)
        public static string PreparePowerShell(string script)
        {
            if (FindPowerShellParamBlock(script) is not { } block)
                return $"{PowerShellHeader}{Environment.NewLine}{script}";

            var end = block.Close + 1;

            return $"{script[..end]}{Environment.NewLine}{Environment.NewLine}{PowerShellHeader}{script[end..]}";
        }

        /// <summary>
        /// Top level param() block of a PowerShell script: only comments, blank lines and attributes ([CmdletBinding()]) can come before it.
        /// Returns the indexes of its '(' and ')' or null when the script has no parameters block.
        /// </summary>
        public static (int Open, int Close)? FindPowerShellParamBlock(string script)
        {
            var i = 0;

            while (i < script.Length)
            {
                while (i < script.Length && char.IsWhiteSpace(script[i])) i++;

                if (i >= script.Length) return null;

                if (script.AsSpan(i).StartsWith("<#"))
                {
                    var endComment = script.IndexOf("#>", i + 2, StringComparison.Ordinal);
                    if (endComment < 0) return null;
                    i = endComment + 2;
                }
                else if (script[i] == '#')
                {
                    var newLine = script.IndexOf('\n', i);
                    if (newLine < 0) return null;
                    i = newLine + 1;
                }
                else if (script[i] == '[')
                {
                    var closeAttribute = matchingClose(script, i, '[', ']');
                    if (closeAttribute < 0) return null;
                    i = closeAttribute + 1;
                }
                else break;
            }

            var match = ParamKeywordRegex().Match(script, i);

            if (!match.Success || match.Index != i) return null;

            var open = match.Index + match.Length - 1;
            var close = matchingClose(script, open, '(', ')');

            return close < 0 ? null : (open, close);
        }

        /// <summary>
        /// Parameters the script reads: Required = arguments it needs to run, Max = arguments it accepts (null: any number).
        /// A heuristic, not a parser: it is used to catch an LLM that declares parameters and forgets their values (or the opposite).
        /// </summary>
        public static (int Required, int? Max) AnalyzeParameters(EScriptShell shell, string script)
            => shell == EScriptShell.PowerShell ? analyzePowerShell(script) : analyzeBash(script);

        private static (int Required, int? Max) analyzePowerShell(string script)
        {
            // Without param() a script can still read $args: any number of arguments
            if (PowerShellParameters(script) is not { } parameters)
                return PowerShellArgsRegex().IsMatch(script) ? (0, null) : (0, 0);

            return (parameters.Count(p => p.Mandatory), parameters.Count);
        }

        /// <summary>
        /// Bash: the variable each positional parameter is assigned to (folder="$1", days="${2:-7}"), by position.
        /// Only plain assignments of the whole parameter count; the first one of each position wins.
        /// </summary>
        public static IReadOnlyDictionary<int, string> BashParameterNames(string script)
        {
            var names = new Dictionary<int, string>();

            foreach (Match match in BashAssignmentRegex().Matches(script))
                names.TryAdd(int.Parse(match.Groups["position"].Value), match.Groups["name"].Value);

            return names;
        }

        /// <summary>
        /// Parameters declared in the top level param() block, in order. Null when the script has no param() block.
        /// </summary>
        public static IReadOnlyList<PowerShellParameter>? PowerShellParameters(string script)
        {
            if (FindPowerShellParamBlock(script) is not { } block) return null;

            return splitTopLevel(script[(block.Open + 1)..block.Close])
                .Select(declaration =>
                {
                    // Attributes and the type ([Parameter(Mandatory = $true)][string]) go before the name and may contain '$' too
                    var withoutAttributes = removeBracketGroups(declaration);
                    var name = ParameterNameRegex().Match(withoutAttributes);

                    return name.Success ? new PowerShellParameter(
                        name.Groups[1].Value,
                        MandatoryRegex().IsMatch(declaration) && !NotMandatoryRegex().IsMatch(declaration),
                        SwitchTypeRegex().IsMatch(declaration)) : null;
                })
                .OfType<PowerShellParameter>()
                .ToList();
        }

        // Removes the [...] groups (attributes and types) outside quotes
        private static string removeBracketGroups(string text)
        {
            var result = new StringBuilder();
            var depth = 0;
            char? quote = null;

            foreach (var c in text)
            {
                if (quote != null)
                {
                    if (c == quote) quote = null;
                    if (depth == 0) result.Append(c);
                    continue;
                }

                if (c is '\'' or '"') quote = c;

                if (c == '[') depth++;
                else if (c == ']' && depth > 0) depth--;
                else if (depth == 0) result.Append(c);
            }

            return result.ToString();
        }

        private static (int Required, int? Max) analyzeBash(string script)
        {
            // Single quoted text is never expanded (awk '{print $1}') and comment lines do not count
            var code = SingleQuotedRegex().Replace(script, "''");
            code = CommentLineRegex().Replace(code, string.Empty);

            var highest = PositionalRegex().Matches(code).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
            var readsAll = AllArgumentsRegex().IsMatch(code);

            return (highest, readsAll ? null : highest);
        }

        // Index of the character closing the one at 'open', skipping quoted text. -1 when it is not closed
        private static int matchingClose(string text, int open, char openChar, char closeChar)
        {
            var depth = 0;
            char? quote = null;

            for (var i = open; i < text.Length; i++)
            {
                var c = text[i];

                if (quote != null)
                {
                    if (c == quote) quote = null;
                    continue;
                }

                if (c is '\'' or '"') quote = c;
                else if (c == openChar) depth++;
                else if (c == closeChar && --depth == 0) return i;
            }

            return -1;
        }

        // Splits by the commas that are not inside (), [], {} or quotes: one item per parameter declaration
        private static List<string> splitTopLevel(string text)
        {
            var items = new List<string>();
            var current = new StringBuilder();
            var depth = 0;
            char? quote = null;

            foreach (var c in text)
            {
                if (quote != null)
                {
                    if (c == quote) quote = null;
                }
                else if (c is '\'' or '"') quote = c;
                else if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    items.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            items.Add(current.ToString());

            return items;
        }

        [GeneratedRegex(@"\Gparam\s*\(", RegexOptions.IgnoreCase)]
        private static partial Regex ParamKeywordRegex();

        [GeneratedRegex(@"Mandatory", RegexOptions.IgnoreCase)]
        private static partial Regex MandatoryRegex();

        [GeneratedRegex(@"Mandatory\s*=\s*\$false", RegexOptions.IgnoreCase)]
        private static partial Regex NotMandatoryRegex();

        [GeneratedRegex(@"'[^']*'")]
        private static partial Regex SingleQuotedRegex();

        [GeneratedRegex(@"(?m)^\s*#.*$")]
        private static partial Regex CommentLineRegex();

        // Not escaped (\$1 is a literal text, e.g. inside an awk program in double quotes)
        [GeneratedRegex(@"(?<!\\)\$\{?([1-9])\}?")]
        private static partial Regex PositionalRegex();

        [GeneratedRegex(@"(?<!\\)\$\{?[@*]")]
        private static partial Regex AllArgumentsRegex();

        [GeneratedRegex(@"\$args\b", RegexOptions.IgnoreCase)]
        private static partial Regex PowerShellArgsRegex();

        [GeneratedRegex(@"^\s*\$(\w+)")]
        private static partial Regex ParameterNameRegex();

        // name="$1" / name=$1 / name="${1}" / name="${1:-default}" (optionally after local / readonly / declare), alone in its line
        [GeneratedRegex(@"(?m)^\s*(?:(?:local|readonly|declare(?:\s+-\w+)*)\s+)?(?<name>[A-Za-z_]\w*)=([""']?)\$\{?(?<position>[1-9])(?::-[^}]*)?\}?\1\s*(?:#.*)?$")]
        private static partial Regex BashAssignmentRegex();

        [GeneratedRegex(@"\[\s*switch\s*\]", RegexOptions.IgnoreCase)]
        private static partial Regex SwitchTypeRegex();
    }
}
