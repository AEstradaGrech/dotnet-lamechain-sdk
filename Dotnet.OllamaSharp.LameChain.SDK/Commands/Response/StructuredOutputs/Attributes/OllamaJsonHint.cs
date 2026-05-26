namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes
{
    public class OllamaJsonHint : OllamaJsonProperty
    {
        public OllamaJsonHint(string hint)
        {
            PromptDescription = hint;
            Title = "HINT";
        }
    }
}
