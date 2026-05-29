using System.Runtime.InteropServices;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

public sealed class OrleansGraphTelemetryGrain : Grain, IOrleansGraphTelemetryGrain, IObservedGrainCallSink
{
    private readonly Dictionary<ObservedGrainCallKey, ObservedGrainCallAccumulator> _observedCalls = new();

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        DelayDeactivation(Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    public Task MergeObservedCallsAsync(IReadOnlyCollection<ObservedGrainCall> observedCalls)
    {
        ArgumentNullException.ThrowIfNull(observedCalls);

        RecordObservedCalls(observedCalls);
        return Task.CompletedTask;
    }

    public Task<ObservedGrainCallGraph> GetObservedGraphAsync()
    {
        return Task.FromResult(GrainTransitionManager.BuildObservedGraphFromSnapshot(CreateSnapshot()));
    }

    public Task<string> GenerateLiveMermaidDiagramAsync()
    {
        var observedGraph = GrainTransitionManager.BuildObservedGraphFromSnapshot(CreateSnapshot());
        return Task.FromResult(GrainTransitionManager.GenerateObservedGraphMermaidDiagram(observedGraph));
    }

    public Task ClearAsync()
    {
        _observedCalls.Clear();
        return Task.CompletedTask;
    }

    public void RecordObservedCalls(IReadOnlyCollection<ObservedGrainCall> observedCalls)
    {
        foreach (var observedCall in observedCalls)
        {
            RecordObservedCall(observedCall);
        }
    }

    public void RecordObservedCall(ObservedGrainCall observedCall)
    {
        var key = ObservedGrainCallKey.From(observedCall);
        ref var accumulator = ref CollectionsMarshal.GetValueRefOrAddDefault(_observedCalls, key, out var exists);
        if (exists)
        {
            accumulator.Merge(observedCall);
            return;
        }

        accumulator = new ObservedGrainCallAccumulator(observedCall);
    }

    private ObservedGrainCall[] CreateSnapshot()
    {
        var snapshot = new ObservedGrainCall[_observedCalls.Count];
        var index = 0;
        foreach (var accumulator in _observedCalls.Values)
        {
            snapshot[index++] = accumulator.ToObservedGrainCall();
        }

        return snapshot;
    }
}
