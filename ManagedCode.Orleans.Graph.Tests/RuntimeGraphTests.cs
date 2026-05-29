using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<TestRuntimeGraphClusterApplication>(Shared = SharedType.PerTestSession)]
public class RuntimeGraphTests(TestRuntimeGraphClusterApplication fixture)
{
    private readonly TestRuntimeGraphClusterApplication _fixture = fixture;

    [Test]
    public async Task AllowAll_RecordsClientAndGrainRuntimeEdgesAsync()
    {
        await ResetTelemetryAsync(_fixture.Cluster.Client);
        var startedAt = DateTimeOffset.UtcNow;

        await _fixture.Cluster.Client
            .GetGrain<IGrainA>("runtime")
            .MethodB1(1);

        var edges = await WaitForEdgesAsync(_fixture.Cluster.Client, edges =>
            edges.Any(edge =>
                edge.Source == Constants.ClientCallerId &&
                edge.Target == typeof(IGrainA).FullName &&
                edge.TargetMethod == nameof(IGrainA.MethodB1)) &&
            edges.Any(edge =>
                edge.Source == typeof(IGrainA).FullName &&
                edge.Target == typeof(IGrainB).FullName &&
                edge.SourceMethod == nameof(IGrainA.MethodB1) &&
                edge.TargetMethod == nameof(IGrainB.MethodB1)));

        var clientEdge = edges.Single(edge =>
            edge.Source == Constants.ClientCallerId &&
            edge.Target == typeof(IGrainA).FullName &&
            edge.TargetMethod == nameof(IGrainA.MethodB1));

        var grainEdge = edges.Single(edge =>
            edge.Source == typeof(IGrainA).FullName &&
            edge.Target == typeof(IGrainB).FullName &&
            edge.SourceMethod == nameof(IGrainA.MethodB1) &&
            edge.TargetMethod == nameof(IGrainB.MethodB1));

        clientEdge.Count.ShouldBe(1);
        grainEdge.Count.ShouldBe(1);
        clientEdge.FirstSeenUtc.ShouldBeLessThanOrEqualTo(clientEdge.LastSeenUtc);
        grainEdge.LastSeenUtc.ShouldBeGreaterThanOrEqualTo(startedAt);
    }

    [Test]
    public async Task TelemetryWorker_FlushesObservedEdgesByTimerAsync()
    {
        await ResetTelemetryAsync(_fixture.Cluster.Client);

        await _fixture.Cluster.Client
            .GetGrain<IGrainA>("timer")
            .MethodB1(1);

        var edges = await WaitForEdgesAsync(_fixture.Cluster.Client, edges =>
            edges.Any(edge =>
                edge.Source == typeof(IGrainA).FullName &&
                edge.Target == typeof(IGrainB).FullName));

        edges.ShouldContain(edge =>
            edge.Source == typeof(IGrainA).FullName &&
            edge.Target == typeof(IGrainB).FullName);
    }

    [Test]
    public async Task TelemetryWorker_AggregatesRepeatedRuntimeEdgesAsync()
    {
        await ResetTelemetryAsync(_fixture.Cluster.Client);

        var grain = _fixture.Cluster.Client.GetGrain<IGrainA>("aggregate");
        await grain.MethodB1(1);
        await grain.MethodB1(2);

        var edges = await WaitForEdgesAsync(_fixture.Cluster.Client, edges =>
            edges.Any(edge =>
                edge.Source == Constants.ClientCallerId &&
                edge.Target == typeof(IGrainA).FullName &&
                edge.TargetMethod == nameof(IGrainA.MethodB1) &&
                edge.Count == 2) &&
            edges.Any(edge =>
                edge.Source == typeof(IGrainA).FullName &&
                edge.Target == typeof(IGrainB).FullName &&
                edge.SourceMethod == nameof(IGrainA.MethodB1) &&
                edge.TargetMethod == nameof(IGrainB.MethodB1) &&
                edge.Count == 2));

        edges.Single(edge =>
            edge.Source == Constants.ClientCallerId &&
            edge.Target == typeof(IGrainA).FullName &&
            edge.TargetMethod == nameof(IGrainA.MethodB1)).Count.ShouldBe(2);
        edges.Single(edge =>
            edge.Source == typeof(IGrainA).FullName &&
            edge.Target == typeof(IGrainB).FullName &&
            edge.SourceMethod == nameof(IGrainA.MethodB1) &&
            edge.TargetMethod == nameof(IGrainB.MethodB1)).Count.ShouldBe(2);
    }

    [Test]
    public async Task TelemetryGrain_GeneratesMermaidDiagramFromObservedRuntimeEdgesAsync()
    {
        await ResetTelemetryAsync(_fixture.Cluster.Client);

        await _fixture.Cluster.Client
            .GetGrain<IGrainA>("diagram")
            .MethodB1(1);

        await WaitForEdgesAsync(_fixture.Cluster.Client, edges =>
            edges.Any(edge =>
                edge.Source == Constants.ClientCallerId &&
                edge.Target == typeof(IGrainA).FullName &&
                edge.TargetMethod == nameof(IGrainA.MethodB1)) &&
            edges.Any(edge =>
                edge.Source == typeof(IGrainA).FullName &&
                edge.Target == typeof(IGrainB).FullName &&
                edge.SourceMethod == nameof(IGrainA.MethodB1) &&
                edge.TargetMethod == nameof(IGrainB.MethodB1)));

        var telemetry = _fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        var diagram = await telemetry.GenerateMermaidDiagramAsync();

        diagram.ShouldContain("graph LR");
        diagram.ShouldContain("ORLEANS_GRAIN_CLIENT");
        diagram.ShouldContain("IGrainA");
        diagram.ShouldContain("IGrainB");
        diagram.ShouldContain("==>");
        diagram.ShouldContain(nameof(IGrainA.MethodB1));
        diagram.ShouldContain("hits: 1");
    }

    [Test]
    public async Task Telemetry_DoesNotTrackOrleansGraphInternalCallsByDefaultAsync()
    {
        await ResetTelemetryAsync(_fixture.Cluster.Client);

        await _fixture.Cluster.Client
            .GetGrain<IGrainA>("internal-default")
            .MethodB1(1);

        var edges = await WaitForEdgesAsync(_fixture.Cluster.Client, edges =>
            edges.Any(edge =>
                edge.Source == typeof(IGrainA).FullName &&
                edge.Target == typeof(IGrainB).FullName));

        edges.Any(IsTelemetryEdge).ShouldBeFalse();
    }

    private static async Task ResetTelemetryAsync(IClusterClient client)
    {
        await client.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey).FlushAsync();
        await Task.Delay(200);
        await client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey).ClearAsync();
    }

    private static async Task<IReadOnlyCollection<ObservedGrainCallEdge>> WaitForEdgesAsync(
        IClusterClient client,
        Func<IReadOnlyCollection<ObservedGrainCallEdge>, bool> predicate)
    {
        var telemetry = client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var edges = await telemetry.GetEdgesAsync();
            if (predicate(edges))
            {
                return edges;
            }

            await Task.Delay(100);
        }

        return await telemetry.GetEdgesAsync();
    }

    private static bool IsTelemetryEdge(ObservedGrainCallEdge edge)
    {
        return IsTelemetryEndpoint(edge.Source) || IsTelemetryEndpoint(edge.Target);
    }

    private static bool IsTelemetryEndpoint(string endpoint)
    {
        return endpoint.Contains(nameof(IOrleansGraphTelemetryWorker), StringComparison.Ordinal) ||
               endpoint.Contains(nameof(IOrleansGraphTelemetryGrain), StringComparison.Ordinal);
    }
}
