using System;
using System.Collections.Generic;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Coding
{
    internal static class PythonHarness
    {
        // csproj resource LogicalName
        private const string ResourceName = "LameChain.Coding.lame_harness.py";

        private static readonly Lazy<string> _source = new(() =>
        {
            using var stream = typeof(PythonHarness).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });

        public static string WriteTo(string runDir)
        {
            var path = Path.Combine(runDir, "lame_harness.py");
            File.WriteAllText(path, _source.Value);
            return path;
        }
    }
}
