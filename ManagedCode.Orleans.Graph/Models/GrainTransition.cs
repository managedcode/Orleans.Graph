namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.GrainTransition")]
public record GrainTransition(string SourceMethod, string TargetMethod, bool IsReentrant = false)
{
    public bool MatchesMethods(string sourceMethod, string targetMethod) =>
        (string.Equals(SourceMethod, "*", System.StringComparison.Ordinal) || string.Equals(SourceMethod, sourceMethod, System.StringComparison.Ordinal)) &&
        (string.Equals(TargetMethod, "*", System.StringComparison.Ordinal) || string.Equals(TargetMethod, targetMethod, System.StringComparison.Ordinal));
}
