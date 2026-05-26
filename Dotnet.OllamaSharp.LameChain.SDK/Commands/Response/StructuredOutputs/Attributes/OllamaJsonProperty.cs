
namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Response.StructuredOutputs.Attributes
{
    //Add N stacked attributes on a property with Title - Description
    // And they will parsed as Section-Instruction
    // Final format is always --> > PROPERTY NAME: <class property name>
    //                                 - Att_1.Title: Att_1.Description
    //                                 - Att_2.Title: Att_2.Description
    //                                 - Att_X.Title: Att_X.Description 
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class OllamaJsonProperty : Attribute
    {
        //Game role for the character
        public string Title { get; set; }
        // This is the role that the character represents in the game and influences it's behavior blah blah blah (texto largo de lo que es)
        public string? PromptDescription { get; set; }
        
    }
}
