namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.CurrentCallerContext")]
internal readonly record struct CurrentCallerContext(
    [property: Id(0)] string Caller,
    [property: Id(1)] string Method);
