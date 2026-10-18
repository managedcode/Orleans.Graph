using Orleans.Services;

namespace ManagedCode.Orleans.Graph.Tests.SystemTargetCluster;

[Alias(SystemTargetTestProtocol.ProbeAlias)]
public interface ISystemTargetProbe : IGrainService
{
    [Alias(SystemTargetTestProtocol.ProbeMethodAlias)]
    Task<SystemTargetProbeReply> ProbeAsync(CancellationToken cancellationToken);

    [Alias(SystemTargetTestProtocol.CallbackMethodAlias)]
    Task<SystemTargetCallbackReply> CallbackAsync(bool refused, CancellationToken cancellationToken);
}

[Immutable]
[GenerateSerializer]
[Alias(SystemTargetTestProtocol.ReplyAlias)]
public sealed record SystemTargetProbeReply(
    [property: Id(0)] bool ExecutedBeforeGrainServicesCompleted,
    [property: Id(1)] int ApplicationHistoryDepth,
    [property: Id(2)] bool ContainsSystemTargetHistory,
    [property: Id(3)] int HighestCompletedStage);

[Immutable]
[GenerateSerializer]
[Alias(SystemTargetTestProtocol.CallbackReplyAlias)]
public sealed record SystemTargetCallbackReply(
    [property: Id(0)] string Source,
    [property: Id(1)] string SourceMethod,
    [property: Id(2)] string NativeSourceId,
    [property: Id(3)] bool SourceMatchesService,
    [property: Id(4)] bool ServiceCallerRestored,
    [property: Id(5)] bool ServiceHistoryRestored,
    [property: Id(6)] bool ParentCallerRestored,
    [property: Id(7)] bool ParentHistoryRestored);

[Alias(SystemTargetTestProtocol.CallerAlias)]
public interface ISystemTargetCallerGrain : IGrainWithGuidKey
{
    [Alias(SystemTargetTestProtocol.ServiceMethodAlias)]
    Task<SystemTargetProbeReply> CallServiceAsync(CancellationToken cancellationToken);

    [Alias(SystemTargetTestProtocol.ServiceCallbackMethodAlias)]
    Task<SystemTargetCallbackReply> CallServiceCallbackAsync(bool refused, CancellationToken cancellationToken);

    [Alias(SystemTargetTestProtocol.CallbackTargetMethodAlias)]
    Task<SystemTargetCallbackReply> CallbackTargetAsync();

    [Alias(SystemTargetTestProtocol.RefusedCallbackTargetMethodAlias)]
    Task<SystemTargetCallbackReply> RefusedCallbackTargetAsync();

    [Alias(SystemTargetTestProtocol.AllowedMethodAlias)]
    Task<int> CallAllowedApplicationAsync(int input);

    [Alias(SystemTargetTestProtocol.DeniedMethodAlias)]
    Task<int> CallDeniedApplicationAsync(int input);
}
