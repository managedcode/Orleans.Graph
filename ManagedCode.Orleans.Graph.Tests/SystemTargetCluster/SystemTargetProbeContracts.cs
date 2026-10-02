using Orleans.Services;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

[Alias(SystemTargetTestProtocol.ProbeAlias)]
public interface ISystemTargetProbe : IGrainService
{
    [Alias(SystemTargetTestProtocol.ProbeMethodAlias)]
    Task<SystemTargetProbeReply> ProbeAsync(CancellationToken cancellationToken);
}

[Immutable]
[GenerateSerializer]
[Alias(SystemTargetTestProtocol.ReplyAlias)]
public sealed record SystemTargetProbeReply(
    [property: Id(0)] bool ExecutedBeforeGrainServicesCompleted,
    [property: Id(1)] int ApplicationHistoryDepth,
    [property: Id(2)] bool ContainsSystemTargetHistory,
    [property: Id(3)] int HighestCompletedStage);

[Alias(SystemTargetTestProtocol.CallerAlias)]
public interface ISystemTargetCallerGrain : IGrainWithGuidKey
{
    [Alias(SystemTargetTestProtocol.ServiceMethodAlias)]
    Task<SystemTargetProbeReply> CallServiceAsync(CancellationToken cancellationToken);

    [Alias(SystemTargetTestProtocol.AllowedMethodAlias)]
    Task<int> CallAllowedApplicationAsync(int input);

    [Alias(SystemTargetTestProtocol.DeniedMethodAlias)]
    Task<int> CallDeniedApplicationAsync(int input);
}
