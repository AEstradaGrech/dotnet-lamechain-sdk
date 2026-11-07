using System.Collections.Concurrent;
using System.Diagnostics;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Telemetry;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Telemetry
{
    // LameTelemetry is static (one ActivitySource + IsEnabled flag for the whole process) and an
    // ActivityListener sees every activity of its source, so every test that enables tracing runs in
    // this non-parallel collection, isolated from the rest of the suite.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class TelemetryCollection
    {
        public const string Name = "LameTelemetry";
    }

    // Stands in for the OpenTelemetry TracerProvider: listens to the "LameChain" source and records
    // every stopped activity, in stop order. Disposing it removes the listener and resets LameTelemetry.
    public sealed class ActivityCollector : IDisposable
    {
        public const string RootName = "test-root";

        private readonly ActivityListener _listener;
        private readonly ConcurrentQueue<Activity> _stopped = new();

        public ActivityCollector(string sourceName = "LameChain")
        {
            // Known starting point: no source and tracing disabled.
            LameTelemetry.StopSource();

            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == sourceName,
                // Same rule as OpenTelemetry's default ParentBased(AlwaysOn) sampler: a child of a
                // parent that is not recorded is dropped (StartActivity returns null).
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    options.Parent != default && !options.Parent.TraceFlags.HasFlag(ActivityTraceFlags.Recorded)
                        ? ActivitySamplingResult.None
                        : ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _stopped.Enqueue(activity)
            };

            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<Activity> Stopped => _stopped.ToList();

        public IReadOnlyList<string> StoppedNames => Stopped.Select(a => a.OperationName).ToList();

        public IReadOnlyList<Activity> Named(string name) => Stopped.Where(a => a.OperationName == name).ToList();

        public Activity Single(string name) => Stopped.Single(a => a.OperationName == name);

        public Activity Root => Single(RootName);

        // Opens the root a LameAgent would open, so handler tests can check what hangs from it.
        public static Activity StartRoot() => LameTelemetry.StartRootActivity(RootName)
            ?? throw new InvalidOperationException("The test root activity was not created: is the collector listening?");

        public void Dispose()
        {
            _listener.Dispose();
            LameTelemetry.StopSource();
        }
    }

    public static class ActivityAssertionExtensions
    {
        public static object? Tag(this Activity activity, string key) => activity.GetTagItem(key);

        public static string? StringTag(this Activity activity, string key) => activity.GetTagItem(key) as string;
    }
}
