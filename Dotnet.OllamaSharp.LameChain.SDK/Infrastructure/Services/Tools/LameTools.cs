
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools
{
    public class LameTools : ToolsService<LameTools>
    {
        public LameTools(IServiceProvider serviceProvider) : base(serviceProvider) { }

        [Description("Usando process")]
        public async Task<string> PythonScriptRunnerTool(string taskDefinition)
        {
            using var scope = _services.CreateScope();

            // 1 - Generate python Code based on the taskDefinition
            // 2 - Generate python code to be saved into temporary directory
            // 3 - Execute the python code and capture the output
            // 4 - Remove the temporary python code file

            return "response from python script output";
        }

        /*Usando PythonNET (Python.NET) para ejecutar código Python desde C#, sin guardar archivos:
         
         PythonGeneratorCommand (genera el string en base a la definición de la tarea):

         PythonInterop.RunPythonCode(
@"import clr
clr.AddReference(""TestProject"");
from TestProject import MyClass;
csharpVariable=MyClass.MyVar
csharpVariable=csharpVariable + "" and this part was added by Python"";
print(csharpVariable);
");

        PythonCodeExecutorTool (ejecuta el código generado y captura la salida): || Rula el comando que genera el código, lo ejecuta y captura la salida

        UserIntentCommand || TaskAnalysisCommand + TaskDefinitionCommand (analiza la tarea (chapa reflexiva) y genera la definición de la tarea de forma concisa como input para la tool
            .Then(PythonGeneratorCommand)
            .Then(PythonCodeExecutorCommand);
         */
    }
}
