using ManagedCode.Orleans.Graph.Extensions;
using Microsoft.Extensions.Configuration;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.AttributeCluster;

public class TestAttributeClientConfigurations : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        clientBuilder.AddOrleansGraph();
    }
}
