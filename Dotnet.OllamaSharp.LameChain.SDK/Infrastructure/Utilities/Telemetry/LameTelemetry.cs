using OllamaSharp.Models.Chat;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;


namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Telemetry
{
    public static class LameTelemetry
    {
        // _source configured and CurrentActivity.Source (one per async flow) is the same as _source, then tracing is enabled and we can start activities and add tags to them.
        public static bool IsTracing => _source != null && Activity.Current?.Source == _source;
        public static bool HasSource => _source != null;
        public static ActivitySource? Source => _source;
        private static ActivitySource? _source;

        // This is either the LameAgent initial Prompt method or the first step of a chain, which is the first activity to be started and the one that will be used to trace the entire chain execution.
        // It is also used to set the trace-level attributes (Langfuse OpenTelemetry property mapping) on the root activity.
        private static ConcurrentDictionary<string, Activity> _rootActivities = new ConcurrentDictionary<string, Activity>();


        // This is called on the 'AddLanfuseTracing' configuration extension, to create the singleton source for the app,
        // to prevent the overriding of the source by accidental calls to 'CreateSource' with different names.
        // The source name is used to identify the source of the telemetry data in Langfuse.
        public static ActivitySource CreateSource(string source = "LameChain")
        {
            _source = new ActivitySource(source);
            
            return _source;
        }

        // Starts a new root activity with the given name. If an activity with the same name already exists,
        // it will return the existing activity instead of creating a new one. (so ensure that the activity name is unique per agent or chain execution,
        // otherwise it will be reused and the trace will be incorrect)
        public static Activity? StartRootActivity(string activityName)
        {
            if (!HasSource) return null;

            // Ensure that the current activity is not from a different source before starting a new root activity
            if (Activity.Current?.Source != _source)
                Activity.Current = null;

            var rootActivity = _source.StartActivity(activityName, ActivityKind.Internal);
            
            if (rootActivity != null)
                _rootActivities[rootActivity.Id] = rootActivity;
            
            return rootActivity;
        }

        public static bool TryStopRootActivity(string activityId)
        {
            if (_rootActivities.ContainsKey(activityId))
            {
                if (_rootActivities.Remove(activityId, out var activity) && activity != null)
                {
                    activity.Stop();

                    return true;
                }

                return false;
            }

            return false;
        }
        public static Activity? StartActivity(string activityName = "generation", ActivityKind kind = ActivityKind.Client)
            => _source != null ? _source.StartActivity(activityName, kind) : null;
        

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
                if(_rootActivities.Count > 0)
                {
                    foreach (var activity in _rootActivities.Values)
                        activity.Stop();
                    
                    _rootActivities.Clear();
                }

                _source.Dispose();
                _source = null;
            }
        }
    }
}
