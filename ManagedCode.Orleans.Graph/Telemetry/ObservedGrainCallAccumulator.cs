using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

internal struct ObservedGrainCallAccumulator(ObservedGrainCall observedCall)
{
    private long _count = observedCall.Count;
    private DateTimeOffset _firstSeenUtc = observedCall.FirstSeenUtc;
    private DateTimeOffset _lastSeenUtc = observedCall.LastSeenUtc;

    public void Merge(ObservedGrainCall observedCall)
    {
        _count += observedCall.Count;
        if (observedCall.FirstSeenUtc < _firstSeenUtc)
        {
            _firstSeenUtc = observedCall.FirstSeenUtc;
        }

        if (observedCall.LastSeenUtc > _lastSeenUtc)
        {
            _lastSeenUtc = observedCall.LastSeenUtc;
        }
    }

    private readonly ObservedGrainCall ToObservedGrainCall(ObservedGrainCallKey key)
    {
        return new ObservedGrainCall(
            key.Source,
            key.Target,
            key.SourceMethod,
            key.TargetMethod,
            _count,
            _firstSeenUtc,
            _lastSeenUtc);
    }

    public static ObservedGrainCall[] CreateSnapshot(Dictionary<ObservedGrainCallKey, ObservedGrainCallAccumulator> calls)
    {
        var snapshot = new ObservedGrainCall[calls.Count];
        var index = 0;
        foreach (var (key, accumulator) in calls)
        {
            snapshot[index++] = accumulator.ToObservedGrainCall(key);
        }

        return snapshot;
    }
}
