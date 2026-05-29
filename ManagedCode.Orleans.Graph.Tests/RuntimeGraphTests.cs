using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;
using Microsoft.Extensions.DependencyInjection;

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
        var diagram = await telemetry.GenerateLiveMermaidDiagramAsync();

        diagram.ShouldContain("graph LR");
        diagram.ShouldContain("ORLEANS_GRAIN_CLIENT");
        diagram.ShouldContain("IGrainA");
        diagram.ShouldContain("IGrainB");
        diagram.ShouldContain("==>");
        diagram.ShouldContain(nameof(IGrainA.MethodB1));
        diagram.ShouldContain("hits: 1");
    }

    [Test]
    public async Task AllowAll_BuildsExactComplexRuntimeGraphAsync()
    {
        await ResetTelemetryAsync(_fixture.Cluster.Client);

        var result = await _fixture.Cluster.Client
            .GetGrain<IGrainA>("complex-flow")
            .MethodComplexFlow(1);

        result.ShouldBe(5);

        var expectedEdges = BuildExpectedComplexFlowEdges(nameof(IGrainA.MethodComplexFlow), includeClient: true);

        var edges = await WaitForEdgesAsync(_fixture.Cluster.Client, edges =>
            edges.Count == expectedEdges.Length && ContainsExpectedEdges(edges, expectedEdges));

        AssertExpectedEdges(edges, expectedEdges);
        edges.Any(IsTelemetryEdge).ShouldBeFalse();

        var telemetry = _fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        var diagram = await telemetry.GenerateLiveMermaidDiagramAsync();

        diagram.ShouldBe(BuildExpectedComplexLiveMermaidDiagram(nameof(IGrainA.MethodComplexFlow), includeClient: true));
    }

    [Test]
    public async Task AllowAll_BuildsExactGrainOnlyRuntimeGraphWithoutClientAsync()
    {
        await ResetTelemetryAsync(_fixture.Cluster.Client);

        var result = await _fixture.Cluster.Client
            .GetGrain<IGrainA>("grain-only-complex-flow")
            .MethodGrainOnlyComplexFlow(1);

        result.ShouldBe(5);

        var expectedEdges = BuildExpectedComplexFlowEdges(nameof(IGrainA.MethodGrainOnlyComplexFlow), includeClient: false);

        var edges = await WaitForEdgesAsync(_fixture.Cluster.Client, edges =>
            edges.Count == expectedEdges.Length && ContainsExpectedEdges(edges, expectedEdges));

        AssertExpectedEdges(edges, expectedEdges);
        edges.Any(edge => edge.Source == Constants.ClientCallerId || edge.Target == Constants.ClientCallerId).ShouldBeFalse();
        edges.Any(IsTelemetryEdge).ShouldBeFalse();

        var telemetry = _fixture.Cluster.Client.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        var diagram = await telemetry.GenerateLiveMermaidDiagramAsync();

        diagram.ShouldBe(BuildExpectedComplexLiveMermaidDiagram(nameof(IGrainA.MethodGrainOnlyComplexFlow), includeClient: false));
    }

    [Test]
    public async Task AllowAll_BuildsExactGrainFactoryRuntimeGraphWithoutClientAsync()
    {
        var grainFactory = GetPrimarySiloGrainFactory();
        await ResetTelemetryAsync(grainFactory);

        var result = await grainFactory
            .GetGrain<IGrainA>("grain-factory-complex-flow")
            .MethodGrainOnlyComplexFlow(1);

        result.ShouldBe(5);

        var expectedEdges = BuildExpectedComplexFlowEdges(nameof(IGrainA.MethodGrainOnlyComplexFlow), includeClient: false);
        var edges = await WaitForEdgesAsync(grainFactory, edges =>
            edges.Count == expectedEdges.Length && ContainsExpectedEdges(edges, expectedEdges));

        AssertExpectedEdges(edges, expectedEdges);
        edges.Any(edge => edge.Source == Constants.ClientCallerId || edge.Target == Constants.ClientCallerId).ShouldBeFalse();
        edges.Any(IsTelemetryEdge).ShouldBeFalse();

        var telemetry = grainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        var diagram = await telemetry.GenerateLiveMermaidDiagramAsync();

        diagram.ShouldBe(BuildExpectedComplexLiveMermaidDiagram(nameof(IGrainA.MethodGrainOnlyComplexFlow), includeClient: false));
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

    private IGrainFactory GetPrimarySiloGrainFactory()
    {
        var serviceProvider = _fixture.Cluster.GetSiloServiceProvider(_fixture.Cluster.Primary.SiloAddress);
        return serviceProvider.GetRequiredService<IGrainFactory>();
    }

    private static async Task ResetTelemetryAsync(IGrainFactory grainFactory)
    {
        await grainFactory.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey).FlushAsync();
        await Task.Delay(200);
        await grainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey).ClearAsync();
    }

    private static async Task<IReadOnlyCollection<ObservedGrainCallEdge>> WaitForEdgesAsync(
        IGrainFactory grainFactory,
        Func<IReadOnlyCollection<ObservedGrainCallEdge>, bool> predicate)
    {
        var telemetry = grainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);

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

    private static void AssertExpectedEdges(
        IReadOnlyCollection<ObservedGrainCallEdge> edges,
        IReadOnlyCollection<ExpectedObservedEdge> expectedEdges)
    {
        edges.Count.ShouldBe(expectedEdges.Count);

        foreach (var expected in expectedEdges)
        {
            edges.ShouldContain(edge =>
                edge.Source == expected.Source &&
                edge.Target == expected.Target &&
                edge.SourceMethod == expected.SourceMethod &&
                edge.TargetMethod == expected.TargetMethod &&
                edge.Count == expected.Count);
        }
    }

    private static bool ContainsExpectedEdges(
        IReadOnlyCollection<ObservedGrainCallEdge> edges,
        IReadOnlyCollection<ExpectedObservedEdge> expectedEdges)
    {
        return expectedEdges.All(expected =>
            edges.Any(edge =>
                edge.Source == expected.Source &&
                edge.Target == expected.Target &&
                edge.SourceMethod == expected.SourceMethod &&
                edge.TargetMethod == expected.TargetMethod &&
                edge.Count == expected.Count));
    }

    private static ExpectedObservedEdge[] BuildExpectedComplexFlowEdges(string rootMethod, bool includeClient)
    {
        var edges = new List<ExpectedObservedEdge>
        {
            new(
                typeof(IGrainA).FullName!,
                typeof(IGrainB).FullName!,
                rootMethod,
                nameof(IGrainB.MethodB1),
                1),
            new(
                typeof(IGrainA).FullName!,
                typeof(IGrainC).FullName!,
                rootMethod,
                nameof(IGrainC.MethodBranchingFlow),
                1),
            new(
                typeof(IGrainC).FullName!,
                typeof(IGrainB).FullName!,
                nameof(IGrainC.MethodBranchingFlow),
                nameof(IGrainB.MethodB1),
                1),
            new(
                typeof(IGrainC).FullName!,
                typeof(IGrainD).FullName!,
                nameof(IGrainC.MethodBranchingFlow),
                nameof(IGrainD.MethodE2),
                1),
            new(
                typeof(IGrainA).FullName!,
                typeof(IGrainD).FullName!,
                rootMethod,
                nameof(IGrainD.MethodE2),
                1),
            new(
                typeof(IGrainD).FullName!,
                typeof(IGrainE).FullName!,
                nameof(IGrainD.MethodE2),
                nameof(IGrainE.MethodE1),
                2)
        };

        if (includeClient)
        {
            edges.Insert(0, new ExpectedObservedEdge(
                Constants.ClientCallerId,
                typeof(IGrainA).FullName!,
                Constants.AnyMethod,
                rootMethod,
                1));
        }

        return edges.ToArray();
    }

    private static string BuildExpectedComplexLiveMermaidDiagram(string rootMethod, bool includeClient)
    {
        var grainA = MermaidNode<IGrainA>();
        var grainB = MermaidNode<IGrainB>();
        var grainC = MermaidNode<IGrainC>();
        var grainD = MermaidNode<IGrainD>();
        var grainE = MermaidNode<IGrainE>();

        var lines = new List<string>
        {
            "graph LR",
            $"    {grainA.Id}[\"{grainA.DisplayName}\"] ==>|{rootMethod}->{nameof(IGrainB.MethodB1)}<br/>hits: 1| {grainB.Id}[\"{grainB.DisplayName}\"]",
            $"    {grainA.Id}[\"{grainA.DisplayName}\"] ==>|{rootMethod}->{nameof(IGrainC.MethodBranchingFlow)}<br/>hits: 1| {grainC.Id}[\"{grainC.DisplayName}\"]",
            $"    {grainA.Id}[\"{grainA.DisplayName}\"] ==>|{rootMethod}->{nameof(IGrainD.MethodE2)}<br/>hits: 1| {grainD.Id}[\"{grainD.DisplayName}\"]",
            $"    {grainC.Id}[\"{grainC.DisplayName}\"] ==>|{nameof(IGrainC.MethodBranchingFlow)}->{nameof(IGrainB.MethodB1)}<br/>hits: 1| {grainB.Id}[\"{grainB.DisplayName}\"]",
            $"    {grainC.Id}[\"{grainC.DisplayName}\"] ==>|{nameof(IGrainC.MethodBranchingFlow)}->{nameof(IGrainD.MethodE2)}<br/>hits: 1| {grainD.Id}[\"{grainD.DisplayName}\"]",
            $"    {grainD.Id}[\"{grainD.DisplayName}\"] ==>|{nameof(IGrainD.MethodE2)}->{nameof(IGrainE.MethodE1)}<br/>hits: 2| {grainE.Id}[\"{grainE.DisplayName}\"]"
        };

        if (includeClient)
        {
            lines.Add($"    {Constants.ClientCallerId}[\"{Constants.ClientCallerId}\"] ==>|{rootMethod}<br/>hits: 1| {grainA.Id}[\"{grainA.DisplayName}\"]");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static (string Id, string DisplayName) MermaidNode<TGrain>()
        where TGrain : IGrain
    {
        return (typeof(TGrain).FullName!.Replace('.', '_'), typeof(TGrain).Name);
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

    private readonly record struct ExpectedObservedEdge(
        string Source,
        string Target,
        string SourceMethod,
        string TargetMethod,
        long Count);
}
