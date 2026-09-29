using Dotnet.OllamaSharp.LameChain.SDK.Command.Bases;
using DotnetLlamaSharp.Domain.Services.Inference;
using System.Reflection;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Models.Agents
{
    public class LameAgent
    {
        // LLM model
        public string Model { get; set; }
        // LLM provider. Set on construction from modelName (<provider>/<model>)and cannot be changed later.
        public string Provider { get; private set; }

        // Add to sysmessage (if not null) to provide context to the model about the agent's purpose and behavior.
        public string? Name { get; private set; }
        // Add to sysmessage (if not null) to provide context to the model about the agent's purpose and behavior.
        public string? Description { get; private set; }

        public Dictionary<string, MethodInfo> Tools { get; private set; } = new Dictionary<string, MethodInfo>();
        public List<string> Skills { get; private set; } = new List<string>();
        
        public Dictionary<string, List<string>> DataSources { get; private set; } = new Dictionary<string, List<string>>();

        // Tools
        // Skills
        // Memory?
        // DataSources (from Rebujito) <- se añaden siempre a SystemMessage

        private readonly IOllamaInferenceService _inferenceService;
        public LameAgent(IOllamaInferenceService inferenceService, string model, string? name = null, string? description = null)
        {
            _inferenceService = inferenceService;

            var split = model.Split('/');

            Model = model;
            Provider = split.Count() == 1 ? "ollama" : split[0];
            Name = name;
            Description = description;
        }

        /// <summary>
        /// Runs a prompt through the agent's model and returns the generated response as a string.
        /// Use this for simple / non structured prompts.
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="maxTokens"></param>
        /// <param name="temperature"></param>
        /// <param name="topP"></param>
        /// <returns></returns>
        public async Task<string> RunTask(string prompt, int maxTokens = 512, float temperature = 0.7f, float topP = 0.9f)
        {
            var systemMessage = buildSystemMessage();
            var fullPrompt = $"{systemMessage}\n{prompt}";
            return "";//_inferenceService.ChatPrompt(Model, fullPrompt, maxTokens, temperature, topP);
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
        public async Task<T> RunCommand<T>(string prompt, int maxTokens = 512, float temperature = 0.7f, float topP = 0.9f) where T : BasePromptCommand<T>
        {
            var systemMessage = buildSystemMessage();
            var fullPrompt = $"{systemMessage}\n{prompt}";
            return null;//_inferenceService.CommandPrompt<T>(Model, fullPrompt, maxTokens, temperature, topP);
        }

        public void AddTool(string toolName, MethodInfo method)
        {
            if (!Tools.ContainsKey(toolName))
                Tools.Add(toolName, method);
        }
        public void AddTools(Dictionary<string, MethodInfo> tools)
        {
            foreach (var tool in tools)
                AddTool(tool.Key, tool.Value);
        }

        public void WithSkills(List<string> skills)
        {
            foreach(var skill in skills)
                if (!Skills.Contains(skill))
                    Skills.Add(skill);
        }
        public void AddDataSources(string sourceName, List<string> data, bool isOverride = false)
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
        }

        private string buildSystemMessage()
        {
            StringBuilder sb = new StringBuilder();

            var sysMessage = "";
            if (!string.IsNullOrEmpty(Name))
                sb.AppendLine($"# Agent Name: {Name}\n");

            if (!string.IsNullOrEmpty(Description))
                sb.AppendLine($"# Agent Description: {Description}\n");
            if (DataSources.Count > 0)
            {
                sb.AppendLine($"# Data Sources:\n");
                foreach (var source in DataSources)
                    sb.AppendLine($"- {source.Key}: {string.Join("\n\n", source.Value)}");
            }

            return sb.ToString();
        }
    }
}
