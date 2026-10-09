namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.NativeCallChainScope")]
public sealed class NativeCallChainScope(GrainId actorId, Guid reentrancyId)
{
    [Id(0)]
    public GrainId ActorId { get; init; } = actorId;

    [Id(1)]
    public Guid ReentrancyId { get; init; } = reentrancyId;
}
