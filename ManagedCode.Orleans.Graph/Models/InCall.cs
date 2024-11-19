using Orleans;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.InCall")]
public class InCall(GrainId? sourceId, GrainId? targetId, string type, string method) : Call(sourceId, targetId, Direction.In, type, method)
{
    public override string ToString()
    {
        return $"Direction: {Direction,-3} | Interface: {Interface,-20} | Method: {Method}";
    }
}