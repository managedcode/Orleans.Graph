using Orleans.Services;

namespace ManagedCode.Orleans.Graph.Tests.Features.GrainServiceTransitions;

public interface ITransitionProbeGrain : IGrainWithStringKey
{
    Task<string> AllowedAsync(CancellationToken cancellationToken);

    Task<string> AlsoAllowedAsync(CancellationToken cancellationToken);

    Task<string> DeniedAsync(CancellationToken cancellationToken);
}

public interface ITransitionProbeGrainService : IGrainService
{
}

internal static class TransitionProbeProtocol
{
    internal const string AllowedResult = "allowed-body-executed";
    internal const string DeniedResult = "denied-body-executed";
    internal const string DenialPrefix = "Transition from ";
    internal const int SiloCount = 2;
    internal const int TestTimeoutMilliseconds = 60_000;
}
