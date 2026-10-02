using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

public sealed class SystemTargetTestSiloConfiguration : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.Services.AddSingleton<SystemTargetProbeObservation>();
        siloBuilder.Services.AddSingleton<SystemTargetProbeClient>();
        siloBuilder.AddGrainService<SystemTargetProbeGrainService>();
        siloBuilder.AddOrleansGraph(configureGraph: graph =>
        {
            graph.AllowClientCallGrain<ISystemTargetCallerGrain>();
            graph.AddGrainTransition<ISystemTargetCallerGrain, IGrainB>().MethodByName(
                nameof(ISystemTargetCallerGrain.CallAllowedApplicationAsync), nameof(IGrainB.MethodB1));
        });
    }
}
