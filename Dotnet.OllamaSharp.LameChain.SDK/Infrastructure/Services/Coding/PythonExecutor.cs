
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using System.Diagnostics;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding
{
    public class PythonExecutor : IPythonExecutor
    {
        private readonly PythonExecutionOptions _options;

        public PythonExecutor(PythonExecutionOptions options)
        {
            _options = options;
        }

        public async Task<PythonExecutionResult> ExecuteAsync(string code, CancellationToken ct = default)
        {
            var runDir = Directory.CreateDirectory(Path.Combine(_options.WorkRoot, $"run_{Guid.NewGuid():N}")).FullName;
            var codePath = Path.Combine(runDir, "generated.py");
            var outDir = Path.Combine(runDir, "out");                   
            var envelopePath = Path.Combine(runDir, "envelope.json");

            await File.WriteAllTextAsync(codePath, code, ct);

            var psi = new ProcessStartInfo(_options.PythonExe)                // C:\...\venv\Scripts\python.exe
            {
                WorkingDirectory = runDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            
            foreach (var a in new[] { "-X", "utf8", _options.HarnessPath, codePath, outDir, envelopePath })
                psi.ArgumentList.Add(a);
            
            psi.Environment["MPLBACKEND"] = "Agg";                       // matplotlib sin intentar abrir ventanas

            using var proc = Process.Start(psi)!;

            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            
            _ = proc.StandardOutput.ReadToEndAsync(ct);                  // vaciar el pipe para que no se bloquee

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            
            cts.CancelAfter(_options.Timeout);

            try 
            { 
                await proc.WaitForExitAsync(cts.Token); 
            }
            catch (OperationCanceledException)
            {
                proc.Kill(entireProcessTree: true);
                return new() { TimedOut = true, Error = new("Timeout", $"Exceeded {_options.Timeout}", "") };
            }

            if (!File.Exists(envelopePath))                              // harness fail (crash, os._exit...)
                return new()
                {
                    ExitCode = proc.ExitCode,
                    Stderr = await stderrTask,
                    Error = new("HarnessFailure", "No envelope produced", "")
                };

            var json = await File.ReadAllTextAsync(envelopePath, ct);

            return JsonSerializer.Deserialize<PythonExecutionResult>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })! with { ExitCode = proc.ExitCode };
        }
    }
}

