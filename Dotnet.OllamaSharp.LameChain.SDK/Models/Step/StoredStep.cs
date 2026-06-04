using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Storeables;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Steps;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step
{
    /// <summary>
    /// Attempts to deserialize a json of type TPrev from the previous output and stores it using a StoreableCommand
    /// It will also try to deserialize any output from any configured feed
    /// </summary>
    /// <typeparam name="TPrev"></typeparam>
    public class StoredStep<TPrev> : SingleThrowStep where TPrev : class
    {
        public StoredStep() { }

        public StoredStep(StepInstruction instruction) : base(instruction) { }

        protected override async Task runStep(IChaineable previous)
        {
            try
            {
                var stored = previous.GetOutputAs<TPrev>();

                ((StoreableCommandRequest<TPrev>)Request).Stored = stored;

                await base.runStep(previous);
            }
            catch(Exception ex)
            {
                notify($"{nameof(runStep)} >> AN ERROR HAS OCCURED WHILE DESERIALIZING THE PREVIOUS OUTPUT >> EXCEPTION: {ex.Message}");
            }
            
        }
        protected override void appendPreviousContext(StringBuilder sb, IChaineable previous) { /*SKIP ANY CONTEXTUAL MESSAGE FROM THE PREVIOUS*/ }
    }
}
