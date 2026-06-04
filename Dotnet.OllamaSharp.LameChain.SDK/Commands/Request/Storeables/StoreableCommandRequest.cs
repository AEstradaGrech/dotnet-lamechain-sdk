using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.QueryCommands;

namespace Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Storeables
{
    public class StoreableCommandRequest<TStored> : PromptCommandRequest
    {
        public StoreableCommandRequest() { }


        // Use this constructor to setup commands that will DESERIALIZE the PREV_OUTPUT and store it using the give collectionName as params of the storingLambda
        // 
        public StoreableCommandRequest(string collectionName) : base()
        {
            CollectionName = collectionName;
            IsStoreOnly = true;
        }

        // Use this constructor when you just want to store data without using the LLM for any kind of process (for example, to store the output of a previous ChainStep)
        public StoreableCommandRequest(string collectionName, TStored stored) : base()
        {
            Stored = stored;
            CollectionName = collectionName;
            IsStoreOnly = true;
        }

        // Use this constructors when you want to GENERATE the TStored and then store it
        public StoreableCommandRequest(string collectionName, string message, string? model = null) : base(message, model) 
        { 
            CollectionName = collectionName;
            IsStoreOnly = false;
        }

        public StoreableCommandRequest(string collectionName, string message, string? guidanceMessage, bool isGuidanceAppend = false, string? model = null) 
            : base(message, guidanceMessage, isGuidanceAppend, model)
        {
            CollectionName = collectionName;
            IsStoreOnly = false;
        }

        public bool IsStoreOnly { get; private set; }
        public TStored? Stored { get; set; }
        public string CollectionName { get; set;}
    }
}
