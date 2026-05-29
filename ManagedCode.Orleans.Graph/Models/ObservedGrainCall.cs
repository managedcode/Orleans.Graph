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

    public ObservedGrainCall Merge(ObservedGrainCall edge)
    {
        if (!string.Equals(Source, edge.Source, StringComparison.Ordinal) ||
            !string.Equals(Target, edge.Target, StringComparison.Ordinal) ||
            !string.Equals(SourceMethod, edge.SourceMethod, StringComparison.Ordinal) ||
            !string.Equals(TargetMethod, edge.TargetMethod, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Observed grain call edges must share the same identity to be merged.");
        }

        return this with
        {
            Count = Count + edge.Count,
            FirstSeenUtc = FirstSeenUtc <= edge.FirstSeenUtc ? FirstSeenUtc : edge.FirstSeenUtc,
            LastSeenUtc = LastSeenUtc >= edge.LastSeenUtc ? LastSeenUtc : edge.LastSeenUtc
        };
    }
}
