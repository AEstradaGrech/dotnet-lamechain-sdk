using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces
{
    public interface IToolbox
    {
        bool TryGetMethodInfo(string methodName, out MethodInfo methodInfo);
    }
}
