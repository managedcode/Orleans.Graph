using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.GrainTransition")]
public record GrainTransition(string SourceMethod, string TargetMethod, bool IsReentrant = false)
{
    public bool MatchesMethods(string sourceMethod, string targetMethod) => 
        (SourceMethod.Equals("*") || SourceMethod == sourceMethod) && 
        (TargetMethod.Equals("*") || TargetMethod == targetMethod);
}