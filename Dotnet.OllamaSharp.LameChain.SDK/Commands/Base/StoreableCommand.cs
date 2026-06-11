using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Storeables;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Base
{
    // ESTO SON STEPS PUTO FUMAO
    // a - PREV CHECKPOINT --> si esta typado como el anterior, deserializa el opt y lo guarda
    // b - Prompt & Store --> hace un prompt con o sin Dbguidance y guarda el resultado donde apunte la lambda
    // c - Feed & Store --> guarda en boosters la info de feeds y la guarda cuando le llega el turno
    public class StoreableCommand<TStored> : DbPromptCommand<TStored> where TStored : class
    {
        // pass the document / chunk to store and the index / collection and return the db insert result (which might feed the next chain)
        protected readonly Func<TStored, string, Task<TStored>> _storingLambda;
        public StoreableCommand() : base() { }

        // a y c (guardar sin mas --> prev o feeds / boosters)
        public StoreableCommand(Func<TStored, string, Task<TStored>> storingLambda) { _storingLambda = storingLambda; }
        // b default y c
        public StoreableCommand(IOllamaInferenceService ollama, Func<TStored, string, Task<TStored>> storingLambda, string? systemMessage = null, CommandSettings? settings = null) : base(ollama, systemMessage, settings)
        {
            _storingLambda = storingLambda;
        }

        // b w/dbsys y c (comandos tipo resume contenido y luego guardar)
        public StoreableCommand(IOllamaInferenceService ollama, Func<TStored, string, Task<TStored>> storingLambda, string collection, string messageName, Func<string, string, Task<string>> retrieverLambda, string? guidanceMessage = null, CommandSettings? settings = null) 
            : base(ollama, collection, messageName, retrieverLambda, guidanceMessage, settings)
        {
            _storingLambda = storingLambda;
        }

        // Func<TStored, string TStored> storingLambda  --> var inserted = await _chromaChat(collection, TStored 
        public override async Task<TStored> Prompt(PromptCommandRequest request)
        {
            validateInputRequest<StoreableCommandRequest<TStored>>(request);

            var castedReq = (StoreableCommandRequest<TStored>)request;

            if (_storingLambda == null)
                throw new ArgumentNullException($"{nameof(StoreableCommand<TStored>)} >> {Prompt} >> NO STORING LAMBDA STORED!");

            if (string.IsNullOrEmpty(castedReq.CollectionName))
                throw new InvalidOperationException($"{nameof(StoreableCommand<TStored>)} >> NO COLLECTION NAME PRESENT IN THE REQUEST");

            if(!castedReq.IsStoreOnly)
            {
                var result = await _ollama.CommandPrompt<TStored>(request.ToOllamaChat(await getPromptInstruction(request.GuidanceMessage, request.IsGuidanceAppend)), _settings.CommandValidations, _settings.ValidationType, validatorFor<TStored>());

                castedReq.Stored = result;
            }
           
            return await _storingLambda(castedReq.Stored, castedReq.CollectionName);
        }

        //TODO: string getPromptInstruction() &&  Task<string> getPromptInstruction + IsDbCommand == typeof == DbCommand & !IsDefaultMode
        protected override async Task<string> getPromptInstruction(string? additionalData = null, bool isAfterCore = true)
        {
            var instruction = await base.getPromptInstruction(additionalData, isAfterCore);

            return string.IsNullOrEmpty(instruction) ? $">> STORING >> {nameof(StoreableCommand<TStored>)}" : instruction;
        }
    }
}
