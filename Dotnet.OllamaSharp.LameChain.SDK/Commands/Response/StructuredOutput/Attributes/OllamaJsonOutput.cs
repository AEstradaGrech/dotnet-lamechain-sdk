namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutput.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class OllamaJsonOutput : Attribute
    {
        public string Title { get; set; }
        public string Description { get; set;  } // What does the schema represent
        
        public OllamaJsonOutput(string title) 
        {
            Title = title;
        }
    }
}
