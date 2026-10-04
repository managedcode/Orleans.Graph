using ManagedCode.Orleans.Graph.Extensions;
using Microsoft.Extensions.Configuration;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

public sealed class AsyncEnumerationTestCluster : IAsyncDisposable
{
    private bool _disposed;

    public AsyncEnumerationTestCluster()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<AsyncEnumerationSiloConfiguration>();
        builder.AddClientBuilderConfigurator<AsyncEnumerationClientConfiguration>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public TestCluster Cluster { get; }

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

public sealed class AsyncEnumerationSiloConfiguration : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddOrleansGraph(
            configureFilters: static filters => filters.TrackOrleansCalls = false,
            configureGraph: AsyncEnumerationTestGraph.Configure,
            assemblies: [typeof(AsyncEnumerationSourceGrain).Assembly]);
    }
}

public sealed class AsyncEnumerationClientConfiguration : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        clientBuilder.AddOrleansGraph(
            configureFilters: static filters => filters.TrackOrleansCalls = false,
            configureGraph: AsyncEnumerationTestGraph.Configure,
            assemblies: [typeof(AsyncEnumerationSourceGrain).Assembly]);
    }
}

internal static class AsyncEnumerationTestGraph
{
    public static void Configure(IGrainCallsBuilder graph)
    {
        graph.AllowClientCallGrain<IAsyncEnumerationSource>();
        graph.AllowClientCallGrain<IAsyncEnumerationOrigin>();
        graph.AllowClientCallGrain<IAsyncEnumerationCatalogProbe>();
        graph.AddGrainTransition<IAsyncEnumerationSource, IAsyncEnumerationLeaf>()
            .SpecificSourceMethodsToAllTargetMethods(nameof(IAsyncEnumerationSource.AllowedAsync));
        graph.AddGrainTransition<IAsyncEnumerationOrigin, IAsyncEnumerationSource>()
            .MethodByName(nameof(IAsyncEnumerationOrigin.ReadAsync), nameof(IAsyncEnumerationSource.AllowedAsync));
        graph.AddGrainTransition<IAsyncEnumerationOrigin, IAsyncEnumerationSource>()
            .MethodByName(nameof(IAsyncEnumerationOrigin.ReadPartialAsync), nameof(IAsyncEnumerationSource.AllowedAsync));
        graph.AddGrainTransition<IAsyncEnumerationOrigin, IAsyncEnumerationSource>()
            .MethodByName(nameof(IAsyncEnumerationOrigin.ReadCancelledAsync), nameof(IAsyncEnumerationSource.CancellableAsync));
        graph.AddGrainTransition<IAsyncEnumerationOrigin, IAsyncEnumerationSource>()
            .MethodByName(nameof(IAsyncEnumerationOrigin.ReadDeniedLeafAsync), nameof(IAsyncEnumerationSource.DeniedAsync));
    }
}
