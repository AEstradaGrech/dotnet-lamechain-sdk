using OllamaSharp.Models;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration
{
    public class PromptSettings
    {
        public PromptSettings() { }
        public PromptSettings(string? model) { Model = model; }
        public PromptSettings(int maxTokens, float temperature, string? model = null) : this(model) 
        {
            MaxTokens = maxTokens;
            Temperature = temperature;
        }
        public PromptSettings(int maxTokens, float temperature, int contextLength, string? model = null) : this(maxTokens, temperature, model) { ContextLength = contextLength; }
        public PromptSettings(int maxTokens, float temperature, float topP, string? model = null) : this(maxTokens, temperature, model)  { TopP = topP; }
        public PromptSettings(int maxTokens, float temperature, float topP, int topK, string? model = null) : this(maxTokens, temperature, topP, model) { TopK = topK; }
        //requested or ApiSettings default
        public string? Model { get; set; }
        //requested or Ollama RequestOptions defaults
        public int? MaxTokens { get; set; }
        public int? ContextLength { get; set; } = null;
        public float? Temperature { get; set; } = null;
        //
        // Summary:
        //     Works together with top-k. A higher value (e.g., 0.95) will lead to more diverse
        //     text, while a lower value (e.g., 0.5) will generate more focused and conservative
        //     text. (Default: 0.9)
        public float? TopP { get; set; } = null;
        //
        // Summary:
        //     Reduces the probability of generating nonsense. A higher value (e.g. 100) will
        //     give more diverse answers, while a lower value (e.g. 10) will be more conservative.
        //     (Default: 40)
        public int? TopK { get; set; } = null;
        //
        // Summary:
        //     Enable Mirostat sampling for controlling perplexity. (default: 0, 0 = disabled,
        //     1 = Mirostat, 2 = Mirostat 2.0)
        public int? MiroStat { get; set; } = null;

        //
        // Summary:
        //     Influences how quickly the algorithm responds to feedback from the generated
        //     text. A lower learning rate will result in slower adjustments, while a higher
        //     learning rate will make the algorithm more responsive. (Default: 0.1)
        public float? MiroStatEta { get; set; } = null;

        //
        // Summary:
        //     Controls the balance between coherence and diversity of the output. A lower value
        //     will result in more focused and coherent text. (Default: 5.0)
        public float? MiroStatTau { get; set; } = null;

        //
        // Summary:
        //     Sets how strongly to penalize repetitions. A higher value (e.g., 1.5) will penalize
        //     repetitions more strongly, while a lower value (e.g., 0.9) will be more lenient.
        //     (Default: 1.1)
        public float? RepeatPenalty { get; set; } = null;

        //
        // Summary:
        //     Sets how far back for the model to look back to prevent repetition. (Default:
        //     64, 0 = disabled, -1 = num_ctx)
        public int? RepeatLastN { get; set; } = null;


        public RequestOptions ToOllamaRequest()
            => new RequestOptions {
               NumCtx = ContextLength ?? 4096,
               NumPredict = MaxTokens ?? 300,
               Temperature = Temperature ?? 0f,
               TopP = TopP ?? .1f,
               TopK = TopK ?? 10,
               RepeatPenalty = RepeatPenalty,
               RepeatLastN = RepeatLastN,
               MiroStat = MiroStat,
               MiroStatEta = MiroStatEta,
               MiroStatTau = MiroStatTau
           };
    }
}
