using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands
{
    public class PromptCommandRequest
    {
        private string? _model = null;
        private Dictionary<string, MethodInfo> _tools = new Dictionary<string, MethodInfo>();
        public PromptCommandRequest() { }

        // By default it is expected a command with _systemMessage + req.Guidance + core message (db || hardcoded)
        // In case there is no DB || Hardcoded message, change 'isGuidanceAppend' to true so the context is added to the _system instruction
        public PromptCommandRequest(string message, bool isGuidanceAppend = true, string? model = null) : this()
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
        public bool HasTools => _tools.Count > 0;
        public EReasoning? Reasoning { get; set; }
        public IEnumerable<object> GetToolDefinitions()
        {
            if (!HasTools) return [];

            var definitions = new List<object>();

            foreach (var key in _tools.Keys)
                definitions.Add(OllamaTools.FromMethod(_tools[key]));

            return definitions;
        }

        public Dictionary<string, string> NestedGuidances = new Dictionary<string, string>();// FOR CHAIN SUPPORT --> Step reads its NestedFeeds list -> if feed is tagged as CMD then it creates a request.GuidanceMessage from the feed and adds it here with the subCommandName&Tag to use it
        public Dictionary<string, MethodInfo> Tools => _tools;
        public virtual ChatRequest ToOllamaChat(string commandSysmsg, CommandSettings settings = null)
            => new ChatRequest {
                Model = getModelForRequest(settings),
                Messages = [new Message(ChatRole.System, commandSysmsg), new Message(ChatRole.User, Prompt)],
                Stream = false,
                Think = Reasoning.HasValue ? new ThinkValue(Reasoning.ToString().ToLower()) : new ThinkValue(Reasoning),
                Tools = HasTools ? GetToolDefinitions() : null,
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

        public PromptCommandRequest AddTool(string name, MethodInfo methodInfo)
        {
            if (!_tools.ContainsKey(name))
                _tools.Add(name, methodInfo);

            return this;
        }

        public void WithTools(Dictionary<string, MethodInfo> tools, bool isOverride = false)
        {
            if (!isOverride)
            {
                foreach (var key in tools.Keys)
                    if (_tools.ContainsKey(key))
                        _tools.Add(key, tools[key]);
            }

            else _tools = tools;
        }
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
