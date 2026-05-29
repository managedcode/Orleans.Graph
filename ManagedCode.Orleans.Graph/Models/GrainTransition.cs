using ManagedCode.Orleans.Graph.Interfaces;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.GrainTransition")]
public record GrainTransition(string SourceMethod, string TargetMethod, bool IsReentrant = false)
{
    public bool MatchesMethods(string sourceMethod, string targetMethod) =>
        (string.Equals(SourceMethod, Constants.AnyMethod, StringComparison.Ordinal) || string.Equals(SourceMethod, sourceMethod, StringComparison.Ordinal)) &&
        (string.Equals(TargetMethod, Constants.AnyMethod, StringComparison.Ordinal) || string.Equals(TargetMethod, targetMethod, StringComparison.Ordinal));
}
