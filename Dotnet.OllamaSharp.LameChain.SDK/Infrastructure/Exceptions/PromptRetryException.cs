namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Exceptions
{
    public class PromptRetryException : Exception
    {
        private const string _message = "En error has occured while requesting a ollama prompt >> RETRIES LIMIT REACHED >> <RETRIES> >> MESSAGE: <EXCEPTION>";
        public PromptRetryException(string exMessage, int retries) : base(_message.Replace("<RETRIES>", $"{retries}").Replace("<EXCEPTION>", exMessage)) {}
    }
}
