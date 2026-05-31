namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

[GenerateSerializer]
public sealed record StatelessWorkerCallResult(
    [property: Id(0)] int Value,
    [property: Id(1)] string ActivationId);
