using Orleans.Runtime.Services;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

public sealed class SystemTargetProbeClient(IServiceProvider services) : GrainServiceClient<ISystemTargetProbe>(services)
{
    public Task<SystemTargetProbeReply> ProbeAsync(SiloAddress destination, CancellationToken cancellationToken) =>
        GetGrainService(destination).ProbeAsync(cancellationToken).WaitAsync(cancellationToken);
}
