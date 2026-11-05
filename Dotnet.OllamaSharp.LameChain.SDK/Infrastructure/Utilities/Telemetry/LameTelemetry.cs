using OllamaSharp.Models.Chat;
using System.Diagnostics;
using System.Text.Json;


namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Telemetry
{
    public static class LameTelemetry
    {
        public static bool IsEnabled { get; set; } = false;
        private static ActivitySource? _source;
        public static ActivitySource CreateSource(string source = "LameChain")
        {
            _source = new ActivitySource(source);
            
            IsEnabled = true;
            
            return _source;
        }

        public static ActivitySource Source { get { return _source ?? CreateSource(); } }

        public static Activity StartActivity(string activityName = "generation", ActivityKind kind = ActivityKind.Server)
        {
            if (_source == null)
                _source = CreateSource();
            
            return _source.StartActivity(activityName, kind) ?? new Activity(activityName);
        }

        public static void TraceGenerationRequest(Activity activity, string model, List<Message> input )
        {
            activity.SetTag("langfuse.observation.type", "generation");
            activity.SetTag("gen_ai.request.model", model);
            activity.SetTag("langfuse.observation.input", JsonSerializer.Serialize(input));
        }

        public static void TraceGenerationResponse(Activity activity, string output, int? inputTokens, int? outputTokens)
        {
            activity.SetTag("langfuse.observation.output", output);
            activity.SetTag("gen_ai.usage.input_tokens", inputTokens);
            activity.SetTag("gen_ai.usage.output_tokens", outputTokens);
        }

        public static void StopSource()
        {
            if (_source != null)
            {
                _source.Dispose();
                _source = null;
            }

            IsEnabled = false;
        }

    }
}
