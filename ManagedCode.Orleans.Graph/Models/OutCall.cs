using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.OutCall")]
public class OutCall(string caller, string method) : Call(Direction.Out, method)
{
    [Id(0)]
    public string Caller { get; set; } = caller;
}