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
                    .From<IGrainA>().To<IGrainB>().Methods(
                        (a => a.MethodA2, b => b.MethodB1),
                        (a => a.MethodA2, b => b.MethodB2)
                    ).WithReentrancy().And()
                    .From<IGrainA>().To<IGrainC>().AllMethods().And()
                    .Group("Group1").AddGrain<IGrainA>().AddGrain<IGrainB>().AllowCallsWithin().And()
                    .AllowAll();
            });
    }
}