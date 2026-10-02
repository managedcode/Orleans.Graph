using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Orleans.Placement;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

[PreferLocalPlacement]
public sealed class SystemTargetCallerGrain(SystemTargetProbeClient client, ILocalSiloDetails localSilo)
    : Grain, ISystemTargetCallerGrain
{
    public Task<SystemTargetProbeReply> CallServiceAsync(CancellationToken cancellationToken) =>
        client.ProbeAsync(localSilo.SiloAddress, cancellationToken);

    public Task<int> CallAllowedApplicationAsync(int input) =>
        GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKey().ToString()).MethodB1(input);

    public Task<int> CallDeniedApplicationAsync(int input) =>
        GrainFactory.GetGrain<IGrainC>(this.GetPrimaryKey().ToString()).MethodC1(input);
}
