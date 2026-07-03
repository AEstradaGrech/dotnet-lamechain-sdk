namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared
{
    /// <summary>
    /// All ToolServices use this model to expose a catalogue of their available methods / tools.
    /// This is usefult for tool selectors
    /// </summary>
    public class ToolInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
