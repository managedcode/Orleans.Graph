using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.OutCall")]
public class OutCall(string caller, string type, string method) : Call(Direction.Out, type, method)
{
    [Id(0)]
    public string Caller { get; set; } = caller;
}