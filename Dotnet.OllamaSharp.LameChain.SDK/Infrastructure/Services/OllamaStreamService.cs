using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using DotnetLlamaSharp.Domain.Services.Inference;
using Microsoft.Extensions.Options;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services
{
    public class OllamaStreamService : IOllamaStreamService
    {
        private readonly IOllamaInferenceService _ollamaService;
        private readonly OllamaSettings _settings;
       
        public OllamaStreamService(IOllamaInferenceService ollamaService, IOptions<OllamaSettings> settings)
        {
            _ollamaService = ollamaService;
            _settings = settings.Value;
        }

        public IAsyncEnumerable<GenerateResponseStream?> SimplePromptStream(Instruction request)
           => _ollamaService.GeneratePromptStream(getGenerateRequest(request));

        public IAsyncEnumerable<ChatResponseStream?> ChatPromptStream(ChatInstruction request, bool bWithSysmsgUpdate = false)
            => _ollamaService.ChatPromptStream(getChatRequest(request.ForChat(bWithSysmsgUpdate)));
        

        private GenerateRequest getGenerateRequest(Instruction promptRequest)
        {
            var settings = promptRequest.Settings .ToOllamaRequest() ??  _settings;
            settings.NumPredict = promptRequest.Settings.MaxTokens;
            settings.Temperature = promptRequest.Settings.Temperature;
            return new GenerateRequest
            {
                Model = promptRequest.Settings.Model ?? _settings.DefaultModel,
                Prompt = promptRequest.Prompt,
                System = promptRequest.SystemMessage,
                Stream = true,
                Options = settings
            };
        }

        private ChatRequest getChatRequest(ChatInstruction promptRequest)
        {
            var messages = new List<Message>();

            promptRequest.ChatHistory.ForEach(x => messages.Add(new Message(new ChatRole(x.Role), x.Content)));

            return new ChatRequest
            {
                Model = promptRequest.Settings.Model ?? _settings.DefaultModel,
                Messages = messages,
                Stream = true,
                Options = promptRequest.Settings.ToOllamaRequest() ?? _settings
            };
        }
    }
}
