using System;
using System.Collections.Generic;
using System.Text;

namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Telemetry
{
    public class TraceConfig
    {
        public string? TraceName { get; set; }
        public string? SessionId { get; set; }
        public string? UserId { get; set; }
        public List<string> Tags { get; private set; }
        public Dictionary<string, string> Metadata { get; private set; }
        public TraceConfig(string? traceName = null, string? sessionId = null, string? userId = null, List<string>? tags = null, Dictionary<string, string>? metadata = null)
        {
            TraceName = traceName;
            SessionId = sessionId;
            UserId = userId;
            Tags = tags ?? new List<string>();
            Metadata = metadata ?? new Dictionary<string, string>();
        }
    }
}
