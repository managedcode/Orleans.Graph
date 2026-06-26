using ManagedCode.Orleans.Graph.Extensions;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

public class TestRuntimeGraphSlowFlushClusterApplication : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public TestRuntimeGraphSlowFlushClusterApplication()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestRuntimeGraphSlowFlushSiloConfigurations>();
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

public class TestRuntimeGraphSlowFlushSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddOrleansGraph(
            configureFilters: filters =>
            {
                filters.LiveGraphFlushPeriod = TimeSpan.FromMinutes(30);
            },
            configureGraph: graph => graph.AllowAll());
    }
}
