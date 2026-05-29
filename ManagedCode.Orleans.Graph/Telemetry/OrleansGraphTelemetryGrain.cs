using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

public sealed class OrleansGraphTelemetryGrain : Grain, IOrleansGraphTelemetryGrain, IObservedGrainCallSink
{
    private readonly Dictionary<ObservedGrainCallKey, ObservedGrainCall> _edges = new();

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        DelayDeactivation(Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    public Task MergeAsync(IReadOnlyCollection<ObservedGrainCall> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        RecordObservedCalls(edges);
        return Task.CompletedTask;
    }

    public Task<ObservedGrainCallGraph> GetObservedGraphAsync()
    {
        return Task.FromResult(GrainTransitionManager.BuildObservedGraph(_edges.Values));
    }

    public Task<string> GenerateLiveMermaidDiagramAsync()
    {
        var observedGraph = GrainTransitionManager.BuildObservedGraph(_edges.Values);
        return Task.FromResult(GrainTransitionManager.GenerateObservedGraphMermaidDiagram(observedGraph));
    }

    public Task ClearAsync()
    {
        _edges.Clear();
        return Task.CompletedTask;
    }

    public void RecordObservedCalls(IReadOnlyCollection<ObservedGrainCall> edges)
    {
        foreach (var edge in edges)
        {
            var key = ObservedGrainCallKey.From(edge);
            _edges[key] = _edges.TryGetValue(key, out var existing)
                ? existing.Merge(edge)
                : edge;
        }
    }
}
