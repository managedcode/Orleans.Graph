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
                graph.AddGrain<IGrainA>()
                    .WithReentrancy();
                
                graph.AddGrainTransition<IGrainA, IGrainB>().AllMethods().WithReentrancy();
                graph.AddGrainTransition<IGrainB, IGrainC>().AllMethods().WithReentrancy();
                graph.AddGrainTransition<IGrainC, IGrainD>().AllMethods().WithReentrancy();
            });
    }
}