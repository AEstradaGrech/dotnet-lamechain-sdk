namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Step.ValueObjects.Outputs
{
    public class ReplayLog : ForgeLog
    {
        public ReplayLog() {}

        public ReplayLog(ForgeLog forgeLog)
        {
            RunnerId = forgeLog.RunnerId;
            PrevId = forgeLog.PrevId;
            NextId = forgeLog.NextId;
            CommandInstruction = forgeLog.CommandInstruction;
            StepInstruction = forgeLog.StepInstruction;
            FeedForwardMessage = forgeLog.FeedForwardMessage;
            Prompt = forgeLog.Prompt;
            ForgeTimestamp = forgeLog.ForgeTimestamp;
            JsonResult = forgeLog.JsonResult;
            JsonSchema = forgeLog.JsonSchema;
        }
        public DateTime PassCatchTimestamp { get; set; }

        public string FeededMessage { get; set; }

        public Dictionary<Guid, List<ReplayLog>> BranchLogs { get; set; }
    }
}
