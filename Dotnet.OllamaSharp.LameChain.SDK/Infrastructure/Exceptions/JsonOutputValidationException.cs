namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Exceptions
{
    public class JsonOutputValidationException : Exception
    {
        private const string _message = "<<TRACE>> :: Invalid Structured Output response >> VALIDATION FAIL >> REASON: <<REASON>> ";
        public JsonOutputValidationException(string trace, string reason) : base(_message.Replace("<<TRACE>>", trace).Replace("<<REASON>>", reason)) {}
    }
}
