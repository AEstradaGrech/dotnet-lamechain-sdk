
using Anthropic.Models.Messages;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration
{
    public class ClaudeSettings
    {
        public string ApiKey { get; set; }
        public string DefaultModel { get; set; }
        public IEnumerable<string> Models => Enum.GetValues<Model>().ToList().Select(model => ModelIdFor(model));
        public string ModelIdFor(Model model)
            => model switch {
                Model.ClaudeSonnet5 => "claude-sonnet-5",
                Model.ClaudeFable5 => "claude-fable-5",
                Model.ClaudeMythos5 => "claude-mythos-5",
                Model.ClaudeOpus4_8 => "claude-opus-4-8",
                Model.ClaudeOpus4_7 => "claude-opus-4-7",
                Model.ClaudeMythosPreview => "claude-mythos-preview",
                Model.ClaudeOpus4_6 => "claude-opus-4-6",
                Model.ClaudeSonnet4_6 => "claude-sonnet-4-6",
                Model.ClaudeHaiku4_5 => "claude-haiku-4-5",
                Model.ClaudeHaiku4_5_20251001 => "claude-haiku-4-5-20251001",
                Model.ClaudeOpus4_5 => "claude-opus-4-5",
                Model.ClaudeOpus4_5_20251101 => "claude-opus-4-5-20251101",
                Model.ClaudeSonnet4_5 => "claude-sonnet-4-5",
                Model.ClaudeSonnet4_5_20250929 => "claude-sonnet-4-5-20250929",
                Model.ClaudeOpus4_1 => "claude-opus-4-1",
                Model.ClaudeOpus4_1_20250805 => "claude-opus-4-1-20250805",
                _ => "claude-sonnet-4-6"
            };
    }
}
