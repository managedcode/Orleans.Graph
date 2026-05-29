namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.ObservedGrainCallVertex")]
public sealed record ObservedGrainCallVertex(
    [property: Id(0)] string Id,
    [property: Id(1)] string DisplayName);
