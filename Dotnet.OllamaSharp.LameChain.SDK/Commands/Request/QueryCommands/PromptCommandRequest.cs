using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands
{
    public class PromptCommandRequest
    {
        private string? _model = null;
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
        public string? Model { get { return _model; } set { _model = setModel(value); } }
        public string Provider { get; private set; }

        public Dictionary<string, string> NestedGuidances = new Dictionary<string, string>();// FOR CHAIN SUPPORT --> Step reads its NestedFeeds list -> if feed is tagged as CMD then it creates a request.GuidanceMessage from the feed and adds it here with the subCommandName&Tag to use it

        public virtual ChatRequest ToOllamaChat(string commandSysmsg, CommandSettings settings = null)
            => new ChatRequest {
                Model = getModelForRequest(settings),
                Messages = [new Message(ChatRole.System, commandSysmsg), new Message(ChatRole.User, Prompt)],
                Stream = false,
                Options = settings == null ? new RequestOptions() : settings.ToOllamaRequest()
            };

        public virtual GenerateRequest ToOllamaGenerate(string commandSysmsg, CommandSettings settings = null)
            => new GenerateRequest {
                Model = getModelForRequest(settings),
                Prompt = Prompt,
                System = commandSysmsg,
                Stream = false,
                Options = settings == null ? new RequestOptions() : settings.ToOllamaRequest() 
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

        protected string getModelForRequest(CommandSettings? settings) => string.IsNullOrEmpty(Model) ? settings != null && !string.IsNullOrEmpty(settings.Model) ? settings.Model : string.Empty : Model;
        private string? setModel(string? model)
        {
            //model = null -> use defaults (ollama + _settings.apiModels[0]
            //model != null & split -> is 'provider/model' format
            //model != null & !split -> default provider (ollama) + selected ollama model
            if (!string.IsNullOrEmpty(model))
            {
                var split = model.Split("/");

                if (split.Length > 1)
                {
                    Provider = split[0];
                    return string.Join("/", split.Skip(1));
                }

                Provider = "ollama";
                return model;
            }
            else
            {
                Provider = "ollama";
                return string.Empty; //will use _settings.DefaultModel
            }
        }
    }
}
