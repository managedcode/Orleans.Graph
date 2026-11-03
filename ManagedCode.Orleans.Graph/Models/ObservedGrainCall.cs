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
    [property: Id(5)] DateTime FirstSeenUtc,
    [property: Id(6)] DateTime LastSeenUtc)
{
    public static ObservedGrainCall Create(string source, string target, string sourceMethod, string targetMethod, DateTime observedAtUtc)
    {
        if (observedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The observation timestamp must be UTC.", nameof(observedAtUtc));
        }

        return new ObservedGrainCall(source, target, sourceMethod, targetMethod, 1, observedAtUtc, observedAtUtc);
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

        return Merge(call.Count, call.FirstSeenUtc, call.LastSeenUtc);
    }

    internal ObservedGrainCall Merge(long count, DateTime firstSeenUtc, DateTime lastSeenUtc)
    {
        return this with
        {
            Count = Count + count,
            FirstSeenUtc = FirstSeenUtc <= firstSeenUtc ? FirstSeenUtc : firstSeenUtc,
            LastSeenUtc = LastSeenUtc >= lastSeenUtc ? LastSeenUtc : lastSeenUtc
        };
    }
}
