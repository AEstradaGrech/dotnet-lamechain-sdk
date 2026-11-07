using OllamaSharp.Models.Chat;
using System.Diagnostics;
using System.Text.Json;


namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Telemetry
{
    public static class LameTelemetry
    {
        // at least one source created and one root activity must be started to enable telemetry, otherwise it is considered disabled
        public static bool IsEnabled => _source != null && _rootActivities.Count > 0;
        public static bool HasSource => _source != null;
        private static ActivitySource? _source;

        // This is called on the 'AddLanfuseTracing' configuration extension, to create the singleton source for the app,
        // to prevent the overriding of the source by accidental calls to 'CreateSource' with different names.
        // The source name is used to identify the source of the telemetry data in Langfuse.
        public static ActivitySource CreateSource(string source = "LameChain")
        {
            _source = new ActivitySource(source);
            
            return _source;
        }

        // This is either the LameAgent initial Prompt method or the first step of a chain, which is the first activity to be started and the one that will be used to trace the entire chain execution.
        // It is also used to set the trace-level attributes (Langfuse OpenTelemetry property mapping) on the root activity.
        private static Dictionary<string, Activity> _rootActivities = new Dictionary<string, Activity>();
        // this is the singleton source for the app. It will create a source if it is null, but that would mean that the user has not configure Langfuse tracing so the system won't crash but won't trace either
        public static ActivitySource Source { get { return _source ?? CreateSource(); } }

        // Starts a new root activity with the given name. If an activity with the same name already exists,
        // it will return the existing activity instead of creating a new one. (so ensure that the activity name is unique per agent or chain execution,
        // otherwise it will be reused and the trace will be incorrect)
        public static Activity? StartRootActivity(string activityName)
        {
            // Ensure that the current activity is not from a different source before starting a new root activity
            if (Activity.Current?.Source != Source)
                Activity.Current = null;

            var rootActivity = Source.StartActivity(activityName, ActivityKind.Internal);
            
            if (rootActivity != null)
                _rootActivities[activityName] = rootActivity;
            
            return rootActivity;
        }

        public static bool TryStopRootActivity(string activityName)
        {
            if (_rootActivities.TryGetValue(activityName, out var rootActivity))
            {
                rootActivity.Stop();
                _rootActivities.Remove(activityName);
                return true;
            }

            return false;
        }
        public static Activity? StartActivity(string activityName = "generation", ActivityKind kind = ActivityKind.Client)
            => _source == null ? StartRootActivity(activityName) : _source.StartActivity(activityName, kind);
        

        // Trace-level attributes (Langfuse OpenTelemetry property mapping), set on the agent or chain root activity
        public static void TraceExecutionConfig(Activity activity, TraceConfig config)
        {
            activity.SetTag("langfuse.trace.name", config.TraceName);
            activity.SetTag("langfuse.session.id", config.SessionId);
            activity.SetTag("langfuse.user.id", config.UserId);
            activity.SetTag("langfuse.trace.tags", config.Tags.ToArray());

            foreach (var item in config.Metadata)
                activity.SetTag($"langfuse.trace.metadata.{item.Key}", item.Value);
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

        // Stops and removes the _source and clears the _rootActivities dictionary.
        // Call this on app shutdown to ensure that the telemetry data is flushed and the source is disposed.
        public static void StopSource()
        {
            if (_source != null)
            {
                _rootActivities.Clear();
                _source.Dispose();
                _source = null;
            }
        }
    }
}
