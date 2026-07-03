using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;
using System.ComponentModel;
using System.Reflection;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services
{
    public abstract class ToolsService<TSelf> : IToolsService<TSelf> where TSelf : ToolsService<TSelf>
    {
        protected readonly IServiceProvider _services;
        public ToolsService() { }
        public ToolsService(IServiceProvider services) 
        {
            _services = services;
        }

        public IEnumerable<ToolInfo> GetToolsCatalogue()
        {
            var toolsCatalogue = new List<ToolInfo>();

            GetType().GetMethods().ToList().ForEach(method => toolsCatalogue.Add(new ToolInfo { Name = method.Name, Description = method.GetCustomAttribute<DescriptionAttribute>().Description }));
        
            return toolsCatalogue;
        }
    }
}
