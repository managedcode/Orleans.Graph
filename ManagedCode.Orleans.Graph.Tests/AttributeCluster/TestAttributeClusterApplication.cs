using Orleans.TestingHost;
using Xunit;

namespace ManagedCode.Orleans.Graph.Tests.AttributeCluster;

[CollectionDefinition(nameof(TestAttributeClusterApplication))]
public class TestAttributeClusterApplication : ICollectionFixture<TestAttributeClusterApplication>, IDisposable
{
    public TestCluster Cluster { get; }

    public TestAttributeClusterApplication()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestAttributeSiloConfigurations>();
        builder.AddClientBuilderConfigurator<TestAttributeClientConfigurations>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.Dispose();
    }
}
