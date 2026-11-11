using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Coding
{
    public class PythonGeneratorCommand : DbPromptCommand<string>
    {
        public PythonGeneratorCommand() : base() { }
        public PythonGeneratorCommand(IOllamaInferenceService ollama) : base(ollama) { }

        //This constructor is meant to support the usage of AtomicValueCommands w/DefaultInstruction and commands like the JsonRefiner (no instruction but orchestrates DB commands)
        public PythonGeneratorCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings) { }

        public PythonGeneratorCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) {}

        public async override Task<string> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<PromptCommandRequest>(request);

            if (string.IsNullOrEmpty(request.Prompt))
                return "Empty input provided, cannot generate Python code.";

            request.Prompt = $"Generate Python code that fulfills the following input: {request.Prompt}";
            
            var response = await _ollama.ChatPrompt(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), provider: request.Provider, request.HasTools ? request.Tools : null);
            
            return response.Content ?? string.Empty;
        }

        protected override string getDefaultInstruction() => @"
You are a Python code generator. Write ONE self-contained Python 3 script that solves the given task when executed.

# EXECUTION CONTRACT (mandatory)
- The script runs at top level. NEVER use `return` outside a function.
- Store the final answer in a variable named `result`. It must be JSON-serializable (number, string, bool, list, dict or None). Convert numpy/pandas values with .item(), .tolist() or .to_dict().
- If the task produces files, save them ONLY inside the already-defined variable `OUTPUT_DIR` using os.path.join(OUTPUT_DIR, ""<name>""), and set `result` to {""file"": ""<name>"", ""description"": ""...""}.
- Input files are given in the task with absolute paths. Never invent paths.
- For charts call matplotlib.use(""Agg"") before importing pyplot. Never call plt.show(), input() or anything that waits for a user.
- Only use the standard library and these packages: {AVAILABLE_PACKAGES}. No network access unless the task explicitly requires it.
- Let exceptions propagate. Do not wrap the script in try/except.

# OUTPUT FORMAT
Return ONLY one ```python code block. No explanations.

# EXAMPLE 1
## TASK:
Return the sum of the even numbers in [3, 8, 10, 15, 22].
## OUTPUT:
```python
numbers = [3, 8, 10, 15, 22]
result = sum(n for n in numbers if n % 2 == 0)
```

# EXAMPLE 2
## TASK:
Input file: C:\data\invoices.xlsx (sheet ""2025"", columns: client, amount).
Sum the ""amount"" column and apply a 15% discount. Return the total before and after the discount.
## OUTPUT:
```python
import pandas as pd

df = pd.read_excel(r""C:\data\invoices.xlsx"", sheet_name=""2025"")
total = float(df[""amount""].sum())
result = {""total"": round(total, 2), ""discount_pct"": 15, ""total_after_discount"": round(total * 0.85, 2)}
```

# EXAMPLE 3
## TASK:
Plot daily sales [120, 135, 128, 150, 160, 155, 172] with a three-day moving average and save it as a PNG.
## OUTPUT:
```python
import os
import numpy as np
import matplotlib
matplotlib.use(""Agg"")
import matplotlib.pyplot as plt

sales = np.array([120, 135, 128, 150, 160, 155, 172])
days = np.arange(1, len(sales) + 1)
moving_average = np.convolve(sales, np.ones(3) / 3, mode=""valid"")

plt.plot(days, sales, marker=""o"", label=""Daily sales"")
plt.plot(days[2:], moving_average, marker=""s"", label=""3-day average"")
plt.title(""Sales trend""); plt.xlabel(""Day""); plt.ylabel(""Sales""); plt.legend(); plt.grid(True)

file_name = ""sales_trend.png""
plt.savefig(os.path.join(OUTPUT_DIR, file_name))
plt.close()

result = {""file"": file_name, ""description"": ""Daily sales with a 3-day moving average""}
```
";
    }
}
