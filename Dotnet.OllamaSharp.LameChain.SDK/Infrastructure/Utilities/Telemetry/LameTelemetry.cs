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

        public static Activity? StartRootActivity(string activityName)
        {
            // Ensure that the current activity is not from a different source before starting a new root activity
            if (Activity.Current?.Source != Source)
                Activity.Current = null;

            return Source.StartActivity(activityName, ActivityKind.Internal);
        }

        public static Activity? StartActivity(string activityName = "generation", ActivityKind kind = ActivityKind.Client)
        {
            if (_source == null)
                _source = CreateSource();
            
            return _source.StartActivity(activityName, kind);
        }

        public static void TraceGenerationRequest(Activity activity, string model, List<Message> input )
        {
            activity.SetTag("langfuse.observation.type", "generation");
            activity.SetTag("gen_ai.request.model", model);
            activity.SetTag("langfuse.observation.input", JsonSerializer.Serialize(input));
        }

        public static void TraceGenerationResponse(Activity activity, string output, int? inputTokens, int? outputTokens, bool shouldStop = false)
        {
            activity.SetTag("langfuse.observation.output", output);
            activity.SetTag("gen_ai.usage.input_tokens", inputTokens);
            activity.SetTag("gen_ai.usage.output_tokens", outputTokens);

            if (shouldStop)
                activity.Stop();
        }

        public static void TraceToolCall(Activity activity, string toolArgs)
        {
            activity.SetTag("langfuse.observation.type", "tool");
            activity.SetTag("langfuse.observation.input", toolArgs);
        }

        public static void TraceToolResponse(Activity activity, string toolResponse)
            => activity.SetTag("langfuse.observation.output", toolResponse);

        public static void TraceGenerationError(Activity activity, string errorMessage)
        {
            activity.SetTag("langfuse.observation.error", errorMessage);
            activity.Stop();
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
