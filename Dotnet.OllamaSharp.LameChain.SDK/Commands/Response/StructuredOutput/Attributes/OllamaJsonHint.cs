namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutput.Attributes
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
