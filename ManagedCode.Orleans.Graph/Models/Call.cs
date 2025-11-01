using System.Diagnostics;
using Orleans;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.Call")]
[DebuggerDisplay("{ToString()}")]
public class Call(GrainId? sourceId, GrainId? targetId, Direction direction, string type, string method)
{
    [Id(0)]
    public Direction Direction { get; set; } = direction;

    [Id(1)]
    public string Interface { get; set; } = type;

    [Id(2)]
    public string Method { get; set; } = method;

    [Id(3)]
    public GrainId? SourceId { get; set; } = sourceId;

    [Id(4)]
    public GrainId? TargetId { get; set; } = targetId;

    public override string ToString() => $"Direction: {Direction,-3} | Interface: {Interface,-20} | Method: {Method}";
}
