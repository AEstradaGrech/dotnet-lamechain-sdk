using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Response;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command
{
    public interface IJsoneable
    {
        //preInstruction: the last chance to insert an instruction, guide, tag or section tag of any kind BEFORE the _systemInstruction to enrich the instruction in some way
        // the final command system messages appends (depending on empty / filled):
        // (preInstruction) + _systemMessage (base guidance | base core message) + request.GuidanceMessage (dynamic context)  + dbMessage | defaultInstruction (core message if empty then _systemMessage becomes the core instruction)
        
        // > PreInstruction: guide _systemMessage (if any) or core message
        // > _systemMessage: might be a guidance for the core message that is set on construction OR the core message itself if there is no dbMessageName and no defaultInstruction()
        // > request.GuidanceMessage: allows to insert data IN-BETWEEN _systemMessage AND core message from one instruction to another. It might be contextual data for the core message or a guidance message for it
        //                            if there is no core message, then it can be used along with the PreInstruction to complement the _systemMessage with new relevant data
        // > dbMessage | default: If it is a DB command that uses a DB instruction or a framework command with a hardcoded defaultInstruction, then that is the core message that might be guided or enhanced dynamically
        //                        Using all the above mentioned options (by setting the value or leaving it empty to compose different system message | messages with different changing sections
        Task<JsonPromptResult> JsonPrompt(PromptCommandRequest request, CommandSettings? settingsOverride = null, bool returnFullInstruction = false, string? preInstruction = null);
        IOllamaInferenceService BorrowLlama { get; }
        CommandSettings? CommandSettings { get; }
    }
}
