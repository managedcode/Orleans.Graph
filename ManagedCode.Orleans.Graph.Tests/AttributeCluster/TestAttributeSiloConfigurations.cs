using ManagedCode.Orleans.Graph.Extensions;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.AttributeCluster;

public class TestAttributeSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddOrleansGraph();
    }
}
