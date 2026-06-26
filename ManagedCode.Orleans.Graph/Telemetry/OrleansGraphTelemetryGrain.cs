using System.Runtime.InteropServices;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

public sealed class OrleansGraphTelemetryGrain : Grain, IOrleansGraphTelemetryGrain, IObservedGrainCallSink
{
    private readonly Dictionary<ObservedGrainCallKey, ObservedGrainCallAccumulator> _observedCalls = new();

    public Task MergeObservedCallsAsync(IReadOnlyCollection<ObservedGrainCall> observedCalls)
    {
        ArgumentNullException.ThrowIfNull(observedCalls);

        RecordObservedCalls(observedCalls);
        return Task.CompletedTask;
    }

    public Task<ObservedGrainCallGraph> GetObservedGraphAsync()
    {
        var snapshot = CreateSnapshot();
        DeactivateIfEmpty(snapshot);
        return Task.FromResult(GrainTransitionManager.BuildObservedGraphFromSnapshot(snapshot));
    }

    public Task<string> GenerateLiveMermaidDiagramAsync()
    {
        var snapshot = CreateSnapshot();
        DeactivateIfEmpty(snapshot);
        var observedGraph = GrainTransitionManager.BuildObservedGraphFromSnapshot(snapshot);
        return Task.FromResult(GrainTransitionManager.GenerateObservedGraphMermaidDiagram(observedGraph));
    }

    public Task ClearAsync()
    {
        _observedCalls.Clear();
        DeactivateOnIdle();
        return Task.CompletedTask;
    }

    private void DeactivateIfEmpty(ObservedGrainCall[] snapshot)
    {
        if (snapshot.Length == 0)
        {
            DeactivateOnIdle();
        }
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
