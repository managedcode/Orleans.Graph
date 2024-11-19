using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.Call")]
public class Call(Direction direction, string type, string method)
{
    [Id(0)]
    public Direction Direction { get; set; } = direction;

    [Id(1)]
    public string Interface { get; set; } = type;

    [Id(2)]
    public string Method { get; set; } = method;
    
    public override string ToString()
    {
        return $"Direction: {Direction,-3} | Interface: {Interface,-20} | Method: {Method}";
    }
}