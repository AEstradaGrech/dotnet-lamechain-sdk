using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using System.ComponentModel;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools
{
    public sealed record ToolsServiceDescriptor(Type ServiceType);
    public sealed record ToolBinding(MethodInfo method, object? target);
    public class Toolbox : IToolbox
    {
        private readonly Dictionary<string, MethodInfo> _tools = new Dictionary<string, MethodInfo>();

        public Toolbox(IEnumerable<ToolsServiceDescriptor> descriptors)
        {
            descriptors
                .SelectMany(d => d.ServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttribute<DescriptionAttribute>() != null)
                .ToList()
                .ForEach(m => _tools.Add($"{m.Name}", m));
        }
        public bool TryGetMethodInfo(string methodName, out MethodInfo methodInfo)
        {
            if (_tools.TryGetValue(methodName, out methodInfo))
                return true;
            
            methodInfo = null;

            return false;
        }
    }
}
