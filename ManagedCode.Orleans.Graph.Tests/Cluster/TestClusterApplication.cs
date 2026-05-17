using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.Cluster;

public class TestClusterApplication : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public TestClusterApplication()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurations>();
        builder.AddClientBuilderConfigurator<TestClientConfigurations>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public TestCluster Cluster { get; }

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
