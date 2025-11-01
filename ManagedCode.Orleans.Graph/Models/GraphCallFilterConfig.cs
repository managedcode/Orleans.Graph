using Orleans;

namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.GraphCallFilterConfig")]
public class GraphCallFilterConfig
{
    [Id(0)]
    public bool TrackOrleansCalls { get; set; } = false;
}
