using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Scripting;
using Microsoft.Extensions.Options;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Scripting
{
    /// <summary>
    /// Runs .sh scripts with bash, without profile / rc files so the result does not depend on the user's shell configuration
    /// </summary>
    public sealed class BashScriptExecutor : BaseScriptExecutor
    {
        public BashScriptExecutor(IOptions<ScriptExecutionOptions> options) : base(options) { }

        public override EScriptShell Shell => EScriptShell.Bash;

        protected override string fileExtension => ".sh";

        protected override (string FileName, string[] Args) getLauncher(string scriptPath)
            => (_options.BashPath, ["--noprofile", "--norc", scriptPath]);

        // CRLF breaks bash ($'\r': command not found): scripts written on Windows or by an LLM may come with it
        protected override string prepareScript(string script) => ScriptFormat.PrepareBash(script);
    }
}
