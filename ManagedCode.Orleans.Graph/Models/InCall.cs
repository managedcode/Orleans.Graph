using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.InCall")]
public class InCall(string method) : Call(Direction.In, method);