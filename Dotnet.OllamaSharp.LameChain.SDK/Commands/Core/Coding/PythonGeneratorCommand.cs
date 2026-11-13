using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Coding
{
    public class PythonGeneratorCommand : DbPromptCommand<PythonGeneratorResponse>
    {
        public PythonGeneratorCommand() : base() { }
        public PythonGeneratorCommand(IOllamaInferenceService ollama) : base(ollama) { }

        //This constructor is meant to support the usage of AtomicValueCommands w/DefaultInstruction and commands like the JsonRefiner (no instruction but orchestrates DB commands)
        public PythonGeneratorCommand(IOllamaInferenceService ollama, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings) { }

        public PythonGeneratorCommand(IOllamaInferenceService ollama, string messageSourceName, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null)
            : base(ollama, messageSourceName, messageName, retrieverLambda, guidanceMessage, settings) {}

        public async override Task<PythonGeneratorResponse> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<PromptCommandRequest>(request);

            if (string.IsNullOrEmpty(request.Prompt))
                return new PythonGeneratorResponse { Code = "Error >> Empty input provided, cannot generate Python code." };

            request.Prompt = $"Generate Python code that fulfills the following input: {request.Prompt}";
            
            return await _ollama.CommandPrompt<PythonGeneratorResponse>(
                request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend), _settings), 
                validation: _settings != null ? validatorFor<PythonGeneratorResponse>(_settings.CommandValidations, _settings.ValidationType) : null);
        }

        // Raw string literal: the JSON examples need no quote escaping, and "\n" inside them is a literal JSON escape (not a C# one).
        // The PythonGeneratorResponse schema (code + dependencies) is appended to this instruction by CommandPrompt<T>.
        protected override string getDefaultInstruction() => """
You are a Python code generator. Write ONE self-contained Python 3 script that solves the given task when executed, and list the third-party packages it needs.

# EXECUTION CONTRACT (mandatory)
- The script runs at top level. NEVER use `return` outside a function.
- Store the final answer in a variable named `result`. It must be JSON-serializable (number, string, bool, list, dict or None). Convert numpy/pandas values with .item(), .tolist() or .to_dict().
- If the task produces files, save them ONLY inside the already-defined variable `OUTPUT_DIR` using os.path.join(OUTPUT_DIR, '<name>'), and set `result` to {'file': '<name>', 'description': '...'}.
- Input files are given in the task with absolute paths. Never invent paths.
- For charts call matplotlib.use('Agg') before importing pyplot. Never call plt.show(), input() or anything that waits for a user.
- No network access unless the task explicitly requires it. Never use services that require API keys, accounts or payment.
- Let exceptions propagate. Do not wrap the script in try/except.

# DEPENDENCIES (mandatory)
- When the script runs, only the standard library and the packages listed in `dependencies` are available.
- Prefer the standard library. When a third-party package is needed, use well-known, widely used libraries (pandas, numpy, matplotlib, scikit-learn, requests...). NEVER invent package names.
- If a list of available packages is provided, use only those.
- List EVERY third-party package the script needs, using its PyPI install name instead of its import name: scikit-learn (not sklearn), pillow (not PIL), opencv-python (not cv2), pyyaml (not yaml), beautifulsoup4 (not bs4).
- Include the packages needed indirectly too, e.g. openpyxl for pandas.read_excel / DataFrame.to_excel with .xlsx files.
- Never list standard library modules (json, os, math, datetime, csv...). Use an empty list when the script only needs the standard library.

# OUTPUT FORMAT
A JSON object with two fields: `code` (the full script as a single JSON string, with \n for line breaks) and `dependencies` (list of PyPI package names).
Prefer single quotes for Python strings inside the code, so the JSON needs fewer escapes.

# EXAMPLE 1
## TASK:
Return the sum of the even numbers in [3, 8, 10, 15, 22].
## OUTPUT:
{"code": "numbers = [3, 8, 10, 15, 22]\nresult = sum(n for n in numbers if n % 2 == 0)", "dependencies": []}

# EXAMPLE 2
## TASK:
Input file: /data/invoices.xlsx (sheet '2025', columns: client, amount).
Sum the 'amount' column and apply a 15% discount. Return the total before and after the discount.
## OUTPUT:
{"code": "import pandas as pd\n\ndf = pd.read_excel('/data/invoices.xlsx', sheet_name='2025')\ntotal = float(df['amount'].sum())\nresult = {'total': round(total, 2), 'discount_pct': 15, 'total_after_discount': round(total * 0.85, 2)}", "dependencies": ["pandas", "openpyxl"]}

# EXAMPLE 3
## TASK:
Plot daily sales [120, 135, 128, 150, 160, 155, 172] with a three-day moving average and save it as a PNG.
## OUTPUT:
{"code": "import os\nimport numpy as np\nimport matplotlib\nmatplotlib.use('Agg')\nimport matplotlib.pyplot as plt\n\nsales = np.array([120, 135, 128, 150, 160, 155, 172])\ndays = np.arange(1, len(sales) + 1)\nmoving_average = np.convolve(sales, np.ones(3) / 3, mode='valid')\n\nplt.plot(days, sales, marker='o', label='Daily sales')\nplt.plot(days[2:], moving_average, marker='s', label='3-day average')\nplt.title('Sales trend')\nplt.xlabel('Day')\nplt.ylabel('Sales')\nplt.legend()\nplt.grid(True)\n\nfile_name = 'sales_trend.png'\nplt.savefig(os.path.join(OUTPUT_DIR, file_name))\nplt.close()\n\nresult = {'file': file_name, 'description': 'Daily sales with a 3-day moving average'}", "dependencies": ["numpy", "matplotlib"]}
""";
    }
}
