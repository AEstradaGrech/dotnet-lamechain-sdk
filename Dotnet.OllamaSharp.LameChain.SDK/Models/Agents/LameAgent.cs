using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities;
using DotnetLlamaSharp.Domain.Services.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    public class LameAgent //ChromaAgent (guarda SysMessageNames para buscar en chroma. Incluye persistencia en chroma (y Chroma sourceables) #CHECK: MongoAgent? | StreamingAgent
    {
        private readonly Guid _id;

        public Guid Id => _id;
        // LLM model
        public string Model { get; set; }
        // LLM provider. Set on construction from modelName (<provider>/<model>)and cannot be changed later.
        public string Provider { get; private set; }

        // Add to sysmessage (if not null) to provide context to the model about the agent's purpose and behavior.
        public string? Name { get; private set; }
        // Add to sysmessage (if not null) to provide context to the model about the agent's purpose and behavior.
        public string? Description { get; private set; }

        public bool HasTools => Tools.Count > 0;
        private PromptSettings? _settings = null;

        public Dictionary<string, MethodInfo> Tools { get; private set; } = new Dictionary<string, MethodInfo>();
        public List<string> Skills { get; private set; } = new List<string>();
        
        public Dictionary<string, List<string>> DataSources { get; private set; } = new Dictionary<string, List<string>>();

        // Memory?

        private readonly IServiceProvider _serviceProvider;
        private readonly IOllamaInferenceService _inferenceService;

        private Action<Guid, string, LogLevel>? _onBroadcast;
        public LameAgent(IServiceProvider serviceProvider, string model, PromptSettings? settings = null, string? name = null, string? description = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException($"{nameof(LameAgent)} >> {nameof(IServiceProvider)}");
            _inferenceService = _serviceProvider.GetRequiredService<IOllamaInferenceService>() ?? throw new ArgumentNullException($"{nameof(LameAgent)} >> {nameof(IOllamaInferenceService)}");
            _settings = settings;

            if (string.IsNullOrEmpty(model))
            {
                var options = _serviceProvider.GetRequiredService<IOptions<OllamaSettings>>() ?? throw new ArgumentNullException($"{nameof(LameAgent)} >> {nameof(IOptions<OllamaSettings>)}");
                Model = options.Value.DefaultModel;
                Provider = "ollama";
            }
            else
            {
                var split = model.Split('_');

                Model = split.Count() == 1 ? model : split[1];
                Provider = split.Count() == 1 ? "ollama" : split[0];
            }

            _id = Guid.NewGuid();
            Name = name;
            Description = description;
        }

        //SETTINGS


        /// <summary>
        /// Runs a prompt through the agent's model and returns the generated response as a string.
        /// Use this for simple / non structured prompts.
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="maxTokens"></param>
        /// <param name="temperature"></param>
        /// <param name="topP"></param>
        /// <returns></returns>
        public async Task<string> RunPrompt(string prompt, string? systemInstruction = null, List<ChatMessage>? chatHistory = null, EReasoning? reasoning = null)
        {
            if (string.IsNullOrEmpty(prompt.Trim()))
                throw new InvalidOperationException($"{nameof(LameAgent)} >> {nameof(RunPrompt)} >> No User Prompt to run");

            var systemMessage = buildSystemMessage();

            var finalInstruction = string.IsNullOrEmpty(systemInstruction) ? systemMessage : $"{systemInstruction}\n\n{systemMessage}";

            if(chatHistory != null && chatHistory.Count > 0)
            {
                var messages = new List<ChatMessage> { new ChatMessage(ChatRole.System.ToString(), finalInstruction) };

                if (chatHistory.First().Role == ChatRole.System.ToString())
                    messages.AddRange(chatHistory.Skip(1));

                else messages.AddRange(chatHistory);

                messages.Add(new ChatMessage(ChatRole.User.ToString(), prompt));

                chatHistory = messages;
            }
            else
            {
                chatHistory = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System.ToString(), finalInstruction),
                    new ChatMessage(ChatRole.User.ToString(), prompt)
                };
            }

            var request = new ChatRequest
            {
                Model = Model,
                Messages = chatHistory.Select(m => new Message(new ChatRole(m.Role), m.Content)),
                Stream = false,
                Think = reasoning.HasValue ? new ThinkValue(reasoning.Value.ToString().ToLower()) : new ThinkValue(reasoning),
                Tools = HasTools ? OllamaTools.GetDefinitions(Tools) : null,
                Options = _settings == null ? new RequestOptions() : _settings.ToOllamaRequest()
            };

            if (_onBroadcast != null)
            {
                _onBroadcast(Id, $"{nameof(RunPrompt)} >> {broadcastId()} >> {broadcastAgentSetup(finalInstruction)}", LogLevel.Information);
                _onBroadcast(Id, $"{nameof(RunPrompt)} >> {broadcastId()} >> Sending request to LLM >> {JsonSerializer.Serialize<ChatRequest>(request)}", LogLevel.Information);
                //Note: right now this is being logged twice, once in the inference service and once here, but the goal of the agent _onBroadcast is to provide
                // a way to log all the agent's activities in one place for telemetry and debugging purposes so you can store the broadcasted messages in a database (logging it is just an example)
                _inferenceService.SubscribeNotifier(onInferenceServiceNotify);
            }

            var response = await _inferenceService.ChatPrompt(request, Provider, Tools);

            if (response == null)
                throw new ArgumentNullException($"{nameof(LameAgent)} >> {nameof(RunPrompt)} >> An error has occured while getting the LLM response");

            if(_onBroadcast != null)
                _onBroadcast(Id, $"{nameof(RunPrompt)} >> {broadcastId()} >> LLM Response >> {response.Content ?? ""}", LogLevel.Information);

            return response.Content ?? "";
        }
        /// <summary>
        /// Runs a prompt using the LameChain Commands system to get structured responses.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prompt"></param>
        /// <param name="maxTokens"></param>
        /// <param name="temperature"></param>
        /// <param name="topP"></param>
        /// <returns></returns>
        public async Task<T> RunCommand<T>(ChatCommandRequest request) where T : BasePromptCommand<T>
        {
            var systemMessage = buildSystemMessage();

            var finalInstruction = string.IsNullOrEmpty(request.GuidanceMessage) ? 
                $"{systemMessage}" : request.IsGuidanceAppend ? 
                $"{systemMessage}.\n\n{request.GuidanceMessage}" :
                $"{request.IsGuidanceAppend}\n\n{systemMessage}";

            

            if (request.ChatHistory.Count > 0)
            {
                var messages = new List<ChatMessage> { new ChatMessage(ChatRole.System.ToString(), finalInstruction) };

                if (request.ChatHistory.First().Role == ChatRole.System.ToString())
                    messages.AddRange(request.ChatHistory.Skip(1));

                else messages.AddRange(request.ChatHistory);
            }

            else request.ChatHistory.Add(new ChatMessage(ChatRole.System.ToString(), finalInstruction));

            if(Tools.Count > 0)
                Tools.Keys.ToList().ForEach(key => request.AddTool(key, Tools[key]));

            if (_onBroadcast != null) 
            {
                _onBroadcast(Id, $"{nameof(RunCommand)} >> {broadcastId()} >> {broadcastAgentSetup(finalInstruction)}", LogLevel.Information);
                _onBroadcast(Id, $"{nameof(RunCommand)} >> {broadcastId()} >> Running command of type {typeof(T).Name}", LogLevel.Information);
                _onBroadcast(Id, $"{nameof(RunCommand)} >> {broadcastId()} >> Sending request to LLM >> {JsonSerializer.Serialize<ChatCommandRequest>(request)}", LogLevel.Information);
                //Note: right now this is being logged twice, once in the inference service and once here, but the goal of the agent _onBroadcast is to provide
                // a way to log all the agent's activities in one place for telemetry and debugging purposes so you can store the broadcasted messages in a database (logging it is just an example)
                _inferenceService.SubscribeNotifier(onInferenceServiceNotify);
            }

            return await _inferenceService.CommandPrompt<T>(request.ToOllamaChat(settings: _settings));
        }

        public LameAgent AddTool(string toolName, MethodInfo method)
        {
            if (!Tools.ContainsKey(toolName))
                Tools.Add(toolName, method);

            return this;
        }
        public LameAgent AddTools(Dictionary<string, MethodInfo> tools)
        {
            foreach (var tool in tools)
                AddTool(tool.Key, tool.Value);

            return this;
        }

        public LameAgent WithSkills(List<string> skills)
        {
            foreach(var skill in skills)
                if (!Skills.Contains(skill))
                    Skills.Add(skill);

            return this;
        }

        public LameAgent WithBroadcast(Action<Guid, string, LogLevel> broadcastAction)
        {
            _onBroadcast += broadcastAction;

            return this;
        }
        public LameAgent AddDataSources(string sourceName, List<string> data, bool isOverride = false)
        {
            if (isOverride)
            {
                if (DataSources.ContainsKey(sourceName))
                    DataSources[sourceName] = data;

                else DataSources[sourceName] = data;
            }
            else
            {
                if (DataSources.ContainsKey(sourceName))
                    DataSources[sourceName] = data;

                else DataSources.Add(sourceName, data);
            }

            return this;
        }

        private string buildSystemMessage()
        {
            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Name))
                sb.AppendLine($"# Agent Name: {Name}\n");

            if (!string.IsNullOrEmpty(Description))
                sb.AppendLine($"# Agent Description: {Description}\n");

            using var scope = _serviceProvider.CreateScope();

            var loader = scope.ServiceProvider.GetRequiredService<IDocumentLoader<MarkdownLoaderService>>();
            
            if(Skills.Count > 0)
            {
                sb.AppendLine("# SKILLS");

                foreach (var skill in Skills)
                {
                    var doc = loader.LoadDocument($"skills\\{skill}").Result;

                    if (doc != null && doc.Pages.Count() > 0)
                        sb.AppendLine(doc.Pages.Last().Text.Trim());
                }
            }
            
            if (DataSources.Count > 0)
            {
                sb.AppendLine("# Data Sources:\n");

                foreach (var source in DataSources)
                    sb.AppendLine($"- {source.Key}: {string.Join("\n\n", source.Value)}");
            }

            return sb.ToString();
        }

        private string broadcastAgentSetup(string systemMessage)
        {
            var sb = new StringBuilder();

            sb.AppendLine("> Agent Setup:")
              .AppendLine(systemMessage);

            if (HasTools)
            {
                sb.AppendLine("# Tools Available:");

                Tools.Keys.ToList().ForEach(key => sb.AppendLine($"- {key}"));
            }

            return sb.ToString().Trim();
        }
        private void onInferenceServiceNotify(string message)
        { 
            if(_onBroadcast != null)
                _onBroadcast(Id, $"{nameof(LameAgent)} >> {broadcastId()} >> {message}", LogLevel.Information);
        }

        private string broadcastId() => $"{Provider}/{Model}";
    }
}
