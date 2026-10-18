using Orleans.Runtime.Services;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

public sealed class SystemTargetProbeClient(IServiceProvider services) : GrainServiceClient<ISystemTargetProbe>(services)
{
    public Task<SystemTargetCallbackReply> CallbackAsync(SiloAddress destination, bool refused, CancellationToken cancellationToken) =>
        GetGrainService(destination).CallbackAsync(refused, cancellationToken).WaitAsync(cancellationToken);

    public Task<SystemTargetProbeReply> ProbeAsync(SiloAddress destination, CancellationToken cancellationToken) =>
        GetGrainService(destination).ProbeAsync(cancellationToken).WaitAsync(cancellationToken);
}
