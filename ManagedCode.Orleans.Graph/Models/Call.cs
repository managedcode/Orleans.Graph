using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.Call")]
public class Call(Direction direction, string method)
{
    [Id(0)]
    public Direction Direction { get; set; } = direction;

    [Id(1)]
    public string Method { get; set; } = method;
}