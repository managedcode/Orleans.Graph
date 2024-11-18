using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.Cluster;

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
         siloBuilder.AddOrleansGraph()
            .CreateGraph(graph => graph
            .AddAllowedTransition<IGrainA, IGrainB>()
            .AddAllowedTransition<IGrainA, IGrainC>()
            
            // .AddAllowedTransition<IGrainB, IGrainC>()
            //
            // .AddAllowedTransition<IGrainC, IGrainA>()
            //
            // .AddAllowedTransition<IGrainD, IGrainE>()
            // .AddAllowedTransition<IGrainE, IGrainD>()
            );

    }
}