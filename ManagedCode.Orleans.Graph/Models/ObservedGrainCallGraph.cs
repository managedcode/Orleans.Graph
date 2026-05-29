namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.ObservedGrainCallGraph")]
public sealed record ObservedGrainCallGraph(
    [property: Id(0)] IReadOnlyCollection<ObservedGrainCallVertex> Vertices,
    [property: Id(1)] IReadOnlyCollection<ObservedGrainCall> Edges);
