namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.ObservedGrainCall")]
public sealed record ObservedGrainCall(
    [property: Id(0)] string Source,
    [property: Id(1)] string Target,
    [property: Id(2)] string SourceMethod,
    [property: Id(3)] string TargetMethod,
    [property: Id(4)] long Count,
    [property: Id(5)] DateTimeOffset FirstSeenUtc,
    [property: Id(6)] DateTimeOffset LastSeenUtc)
{
    public static ObservedGrainCall Create(string source, string target, string sourceMethod, string targetMethod)
    {
        var now = DateTimeOffset.UtcNow;
        return new ObservedGrainCall(source, target, sourceMethod, targetMethod, 1, now, now);
    }

    public ObservedGrainCall Merge(ObservedGrainCall call)
    {
        if (!string.Equals(Source, call.Source, StringComparison.Ordinal) ||
            !string.Equals(Target, call.Target, StringComparison.Ordinal) ||
            !string.Equals(SourceMethod, call.SourceMethod, StringComparison.Ordinal) ||
            !string.Equals(TargetMethod, call.TargetMethod, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Observed grain calls must share the same identity to be merged.");
        }

        return this with
        {
            Count = Count + call.Count,
            FirstSeenUtc = FirstSeenUtc <= call.FirstSeenUtc ? FirstSeenUtc : call.FirstSeenUtc,
            LastSeenUtc = LastSeenUtc >= call.LastSeenUtc ? LastSeenUtc : call.LastSeenUtc
        };
    }
}
