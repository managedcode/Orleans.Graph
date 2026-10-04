using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.Features.GrainServiceTransitions;

public sealed class TransitionProbeTestCluster : IAsyncDisposable
{
    private int _disposed;

    public TransitionProbeTestCluster()
    {
        var builder = new TestClusterBuilder(TransitionProbeProtocol.SiloCount);
        builder.AddSiloBuilderConfigurator<TransitionProbeSiloConfiguration>();
        builder.AddClientBuilderConfigurator<TransitionProbeClientConfiguration>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public TestCluster Cluster { get; }

    public TransitionProbeObservation GetObservation(SiloAddress address) =>
        Cluster.GetSiloServiceProvider(address).GetRequiredService<TransitionProbeObservation>();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await Cluster.DisposeAsync();
        }
    }
}
