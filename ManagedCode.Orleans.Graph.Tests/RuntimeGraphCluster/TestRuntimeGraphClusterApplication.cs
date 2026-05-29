using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

public class TestRuntimeGraphClusterApplication : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public TestRuntimeGraphClusterApplication()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestRuntimeGraphSiloConfigurations>();
        builder.AddClientBuilderConfigurator<TestRuntimeGraphClientConfigurations>();
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
