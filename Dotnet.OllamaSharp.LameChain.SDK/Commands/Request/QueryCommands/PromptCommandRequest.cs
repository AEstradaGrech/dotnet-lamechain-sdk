using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands
{
    public class PromptCommandRequest
    {
        public PromptCommandRequest() { }

        // By default it is expected a command with _systemMessage + req.Guidance + core message (db || hardcoded)
        // In case there is no DB || Hardcoded message, change 'isGuidanceAppend' to true so the context is added to the _system instruction
        public PromptCommandRequest(string message, bool isGuidanceAppend = true, string? model = null) 
        {
            IsGuidanceAppend = isGuidanceAppend;
            Prompt = message;
            Model = model;
        }

        public PromptCommandRequest(string message, string? guidanceMessage, bool isGuidanceAppend = true, string? model = null) : this(message, isGuidanceAppend, model) 
        {
            GuidanceMessage = guidanceMessage; 
        }
        public string Prompt { get; set; } = string.Empty;
        // an extra instruction appart of the _systemMessage stored on construction. Allows to insert data / guidance from events / LLM interactions that might have happened since the instantiation (a chained prompt, for example)
        public string? GuidanceMessage { get; set; } = null; 
        public bool IsGuidanceAppend { get; set; }
        public string? Model { get; set; }

        public Dictionary<string, string> NestedGuidances = new Dictionary<string, string>();// FOR CHAIN SUPPORT --> Step reads its NestedFeeds list -> if feed is tagged as CMD then it creates a request.GuidanceMessage from the feed and adds it here with the subCommandName&Tag to use it

        public virtual ChatRequest ToOllamaChat(string commandSysmsg, CommandSettings settings = null)
            => new ChatRequest {
                Model = getModelForRequest(settings),
                Messages = [new Message(ChatRole.System, commandSysmsg), new Message(ChatRole.User, Prompt)],
                Stream = false,
                Options = settings.ToOllamaRequest() ?? new RequestOptions()
            };

        public virtual GenerateRequest ToOllamaGenerate(string commandSysmsg, CommandSettings settings = null)
            => new GenerateRequest {
                Model = getModelForRequest(settings),
                Prompt = Prompt,
                System = commandSysmsg,
                Stream = false,
                Options = settings.ToOllamaRequest()
            };

        public PromptCommandRequest Clone()
        {
            var clone = Activator.CreateInstance(GetType()) as PromptCommandRequest;

            var thisProps = GetType().GetProperties();

            clone.GetType().GetProperties().ToList().ForEach(p =>
            {
                var originalProp = thisProps.SingleOrDefault(x => x.Name == p.Name);

                p.SetValue(clone, originalProp.GetValue(this));
            });

            return clone;
        }

        public TReq Clone<TReq>() where TReq : PromptCommandRequest
        {
            var clone = Activator.CreateInstance(typeof(TReq)) as TReq;

            var thisProps = GetType().GetProperties();

            clone.GetType().GetProperties().ToList().ForEach(p =>
            {
                var originalProp = thisProps.SingleOrDefault(x => x.Name == p.Name);

                p.SetValue(clone, originalProp.GetValue(this));
            });

            return clone;
        }

        protected string getModelForRequest(CommandSettings? settings) => string.IsNullOrEmpty(Model) ? settings != null && !string.IsNullOrEmpty(settings.Model) ? settings.Model : "qwen2.5:7b" : Model;
    }
}
