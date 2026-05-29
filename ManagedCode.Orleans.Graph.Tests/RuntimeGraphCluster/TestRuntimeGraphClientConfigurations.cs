using ManagedCode.Orleans.Graph.Extensions;
using Microsoft.Extensions.Configuration;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

public class TestRuntimeGraphClientConfigurations : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        clientBuilder.AddOrleansGraph();
    }
}
