using ManagedCode.Orleans.Graph.Interfaces;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.OutCall")]
public class OutCall(
    GrainId? sourceId,
    GrainId? targetId,
    string caller,
    string type,
    string method,
    string callerMethod = Constants.AnyMethod) : Call(sourceId, targetId, Direction.Out, type, method)
{
    [Id(0)]
    public string Caller { get; set; } = caller;

    [Id(1)]
    public string CallerMethod { get; set; } = callerMethod;

    public override string ToString() => $"\nCaller: {Caller}\nDirection: {Direction,-3} | Interface: {Interface,-20} | Method: {Method}";
}
