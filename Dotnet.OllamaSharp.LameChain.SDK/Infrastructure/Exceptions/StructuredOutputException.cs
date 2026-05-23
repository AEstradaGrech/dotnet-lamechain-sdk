namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Exceptions
{
    public class StructuredOutputException : Exception
    {
        private const string _message = "Invalid Structured Output response >> VALIDATION FAIL >> REASON: <REASON> >> CONFIDENCE SCORE: <SCORE>";
        public StructuredOutputException(string reason, float confidence) : base(_message.Replace("<REASON>", reason).Replace("<SCORE>", $"{confidence}")) {}
    }
}
