using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.AttributeCluster;

public class TestAttributeClusterApplication : IDisposable, IAsyncDisposable
{
    private bool _disposed;

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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cluster.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await Cluster.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
