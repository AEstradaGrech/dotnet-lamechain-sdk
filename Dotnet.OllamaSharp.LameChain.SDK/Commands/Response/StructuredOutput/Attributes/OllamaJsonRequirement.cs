namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutput.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = true)]
    public class OllamaJsonRequirement : Attribute
    {
        public string Requirement { get; set; }
        public OllamaJsonRequirement(string requirement)
        {
            Requirement = requirement;
        }
    }
}
