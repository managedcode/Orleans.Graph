using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.Cluster;

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddOrleansGraph()
            .CreateGraph(graph =>
            {
                graph.AddGrain<IGrainA>();
                
                graph.From<IGrainA>()
                    .To<IGrainB>()
                    .AllMethods();
                
            });
    }
}