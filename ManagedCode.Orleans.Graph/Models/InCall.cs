using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.InCall")]
public class InCall(string type, string method) : Call(Direction.In, type, method)
{
    public override string ToString()
    {
        return $"Direction: {Direction,-3} | Interface: {Interface,-20} | Method: {Method}";
    }
}