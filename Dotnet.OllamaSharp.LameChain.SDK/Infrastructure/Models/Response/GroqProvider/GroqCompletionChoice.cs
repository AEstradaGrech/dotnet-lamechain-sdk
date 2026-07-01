using OllamaSharp.Models.Chat;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Response.GroqProvider
{
    public class GroqCompletionChoice
    {
        public int Index { get; set;  }
        public Message Message{ get; set; } 
    }
}
