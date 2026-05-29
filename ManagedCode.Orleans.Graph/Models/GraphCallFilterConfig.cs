namespace ManagedCode.Orleans.Graph.Models;

[Immutable]
[GenerateSerializer]
[Alias("MC.GraphCallFilterConfig")]
public class GraphCallFilterConfig
{
    [Id(0)]
    public bool TrackOrleansCalls { get; set; } = false;

    [Id(1)]
    public bool TrackOrleansGraphInternalCalls { get; set; } = false;

    [Id(2)]
    public TimeSpan LiveGraphFlushPeriod { get; set; } = TimeSpan.FromSeconds(1);
}
