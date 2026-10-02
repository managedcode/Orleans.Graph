using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

public sealed class SystemTargetTestCluster : IAsyncDisposable
{
    public SystemTargetTestCluster()
    {
        var builder = new TestClusterBuilder(SystemTargetTestProtocol.SiloCount);
        builder.AddSiloBuilderConfigurator<SystemTargetTestSiloConfiguration>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public TestCluster Cluster { get; }
    public IServiceProvider PrimaryServices => Cluster.GetSiloServiceProvider(Cluster.Primary!.SiloAddress);

    public ISystemTargetCallerGrain CreateCaller() => PrimaryServices.GetRequiredService<IGrainFactory>()
        .GetGrain<ISystemTargetCallerGrain>(Guid.NewGuid());

    public ValueTask DisposeAsync() => Cluster.DisposeAsync();
}
