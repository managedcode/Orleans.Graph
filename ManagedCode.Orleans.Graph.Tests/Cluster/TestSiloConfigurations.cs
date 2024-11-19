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
                graph.AddGrainTransition<IGrainA, IGrainB>().WithReentrancy().AllMethods();
                graph.AddGrainTransition<IGrainB, IGrainC>().WithReentrancy().AllMethods();
                graph.AddGrainTransition<IGrainC, IGrainD>().WithReentrancy().AllMethods();
            });
    }
}