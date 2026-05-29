using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

public sealed class OrleansGraphTelemetryGrain : Grain, IOrleansGraphTelemetryGrain, IObservedGrainCallSink
{
    private readonly Dictionary<ObservedGrainCallKey, ObservedGrainCallEdge> _edges = new();

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        DelayDeactivation(Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    public Task MergeAsync(IReadOnlyCollection<ObservedGrainCallEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        RecordObservedEdges(edges);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ObservedGrainCallEdge>> GetEdgesAsync()
    {
        var snapshot = _edges.Values
            .OrderBy(static edge => edge.Source, StringComparer.Ordinal)
            .ThenBy(static edge => edge.Target, StringComparer.Ordinal)
            .ThenBy(static edge => edge.SourceMethod, StringComparer.Ordinal)
            .ThenBy(static edge => edge.TargetMethod, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ObservedGrainCallEdge>>(snapshot);
    }

    public Task<string> GenerateLiveMermaidDiagramAsync()
    {
        return Task.FromResult(GrainTransitionManager.GenerateObservedMermaidDiagram(_edges.Values));
    }

    public Task ClearAsync()
    {
        _edges.Clear();
        return Task.CompletedTask;
    }

    public void RecordObservedEdges(IReadOnlyCollection<ObservedGrainCallEdge> edges)
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
