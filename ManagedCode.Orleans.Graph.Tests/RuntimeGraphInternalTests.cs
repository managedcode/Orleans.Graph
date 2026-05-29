using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<TestRuntimeGraphInternalClusterApplication>(Shared = SharedType.PerTestSession)]
public class RuntimeGraphInternalTests(TestRuntimeGraphInternalClusterApplication fixture)
{
    private readonly TestRuntimeGraphInternalClusterApplication _fixture = fixture;

    [Test]
    public async Task Telemetry_TracksOrleansGraphInternalCallsWhenEnabledAsync()
    {
        await _fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey).FlushAsync();
        await Task.Delay(200);
        await _fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey).ClearAsync();

        await _fixture.Cluster.Client
            .GetGrain<IGrainA>("internal-enabled")
            .MethodB1(1);

        var telemetry = _fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        IReadOnlyCollection<ObservedGrainCall> edges = [];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var graph = await telemetry.GetObservedGraphAsync();
            edges = graph.Edges;
            if (edges.Any(edge => edge.Target == typeof(IOrleansGraphTelemetryWorker).FullName))
            {
                break;
            }

            await Task.Delay(100);
        }

        edges.ShouldContain(edge => edge.Target == typeof(IOrleansGraphTelemetryWorker).FullName);
        edges.ShouldContain(edge =>
            edge.Source == typeof(IOrleansGraphTelemetryWorker).FullName &&
            edge.Target == typeof(IOrleansGraphTelemetryGrain).FullName &&
            edge.SourceMethod == nameof(IOrleansGraphTelemetryWorker.FlushAsync) &&
            edge.TargetMethod == nameof(IOrleansGraphTelemetryGrain.MergeObservedCallsAsync));
    }
}
