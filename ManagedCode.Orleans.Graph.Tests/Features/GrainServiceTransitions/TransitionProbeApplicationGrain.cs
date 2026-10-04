namespace ManagedCode.Orleans.Graph.Tests.Features.GrainServiceTransitions;

public sealed class TransitionProbeApplicationGrain : Grain, ITransitionProbeGrain
{
    public Task<string> AllowedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TransitionProbeProtocol.AllowedResult);
    }

    public Task<string> AlsoAllowedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TransitionProbeProtocol.AllowedResult);
    }

    public Task<string> DeniedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TransitionProbeProtocol.DeniedResult);
    }
}
