using System;
using System.Collections.Generic;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared
{
    public class ClaudeSettings
    {
        public string ApiKey { get; set; }
        public List<string> Models { get; set; }
        public string Endpoint { get; set; }
    }
}
