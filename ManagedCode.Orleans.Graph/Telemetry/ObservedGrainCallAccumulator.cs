using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

internal struct ObservedGrainCallAccumulator(ObservedGrainCall observedCall)
{
    private readonly string _source = observedCall.Source;
    private readonly string _target = observedCall.Target;
    private readonly string _sourceMethod = observedCall.SourceMethod;
    private readonly string _targetMethod = observedCall.TargetMethod;
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

    public readonly ObservedGrainCall ToObservedGrainCall()
    {
        return new ObservedGrainCall(
            _source,
            _target,
            _sourceMethod,
            _targetMethod,
            _count,
            _firstSeenUtc,
            _lastSeenUtc);
    }
}
