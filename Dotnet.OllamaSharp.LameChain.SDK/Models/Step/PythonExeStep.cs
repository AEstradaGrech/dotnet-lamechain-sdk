using Dotnet.OllamaSharp.LameChain.SDK.Commands.Core.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step
{
    public class PythonExeStep : SingleThrowStep
    {
        public PythonExeStep() : base() { }

        public PythonExeStep(StepSettings settings) : base(settings)
        {
            validateInputCommand();
        }

        public PythonExeStep(IJsoneable command, StepSettings settings) : base(command, settings)
        {
            validateInputCommand();
        }
 
        public override bool CanBeForged(IChaineable previous)
            => base.CanBeForged(previous) && previous.IsForged && previous.GetOutputAs<PythonGeneratorResponse>() != null && _stepSettings.CommandRequest is PythonCodingRequest;
        
        private void validateInputCommand()
        {
            if (_commands.Count() == 0)
                throw new ArgumentNullException($"{nameof(PythonExeStep)} >> No input command has been passed in the StepSettings");

            if (!(_commands.First() is PythonCodeExecutorCommand))
                throw new InvalidOperationException($"{nameof(PythonExeStep)} >> the step command is not an instance of {nameof(PythonCodeExecutorCommand)}");
        }
        protected override async Task runStep(IChaineable previous)
        {
            try
            {
                ((PythonCodingRequest)_stepSettings.CommandRequest).GeneratorParams = previous.GetOutputAs<PythonGeneratorResponse>();

                await base.runStep(previous);

            }
            catch(Exception ex)
            {
                notify($"{nameof(runStep)} >> AN ERROR HAS OCCURED WHILE EXECUTING THE PYTHON CODE GENERATED IN THE PREVIOUS STEP >> EXCEPTION: {ex.Message}");

                throw;
            }
        }
    }
}
