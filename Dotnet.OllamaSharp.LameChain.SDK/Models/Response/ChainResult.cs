using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs;
using OllamaSharp.Models.Chat;
using System.Text.Json.Nodes;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Response
{
    public class ChainResult
    {
        public ChainResult() { }
        public ChainResult(object result, JsonNode resultSchema, string chainInput)
        {
            Result = result;
            Schema = resultSchema;
            InputPrompt = chainInput;
            Message = null;
            SystemInstructions = new ChatMessage(ChatRole.System.ToString(), string.Empty);
        }

        public ChainResult(object result, JsonNode resultSchema, string chainInput, List<string> stepsLog, ChatMessage? processedResult = null) : this(result, resultSchema, chainInput) 
        {
            ChainStepsLog = stepsLog;
            Message = processedResult;
            
            if(stepsLog.Count > 0)
                for (int i = 0; i < stepsLog.Count; i++)
                    SystemInstructions.Content += $"\n{stepsLog[i]}";
        }

        public ChainResult( List<ReplayLog> logs, object result, JsonNode resultSchema, string chainInput, List<string> stepsLog, ChatMessage? processedResult = null) 
            : this(result, resultSchema, chainInput, stepsLog, processedResult)
        {
            ChainLogs = logs;
        }
        public ChatMessage? Message { get; set; }
        public ChatMessage SystemInstructions { get; set; }
        public object Result { get; set; }
        public JsonNode Schema { get; set; }
        public string InputPrompt { get; set; }
        public List<string> ChainStepsLog { get; set; }
        public List<ReplayLog> ChainLogs { get; set; }

    }
}
