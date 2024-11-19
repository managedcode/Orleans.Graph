using Orleans;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.OutCall")]
public class OutCall(GrainId? sourceId, GrainId? targetId, string caller, string type, string method) : Call(sourceId, targetId, Direction.Out, type, method)
{
    [Id(0)]
    public string Caller { get; set; } = caller;
    
    public override string ToString()
    {
        return $"\nCaller: {Caller}\nDirection: {Direction,-3} | Interface: {Interface,-20} | Method: {Method}";
    }
}