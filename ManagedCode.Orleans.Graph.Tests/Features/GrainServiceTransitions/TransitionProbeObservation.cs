namespace ManagedCode.Orleans.Graph.Tests.Features.GrainServiceTransitions;

public sealed record TransitionProbeOutcome(
    string FirstAllowedResult,
    string? DeniedTransition,
    string? DeniedBodyResult,
    string FinalAllowedResult);

public sealed class TransitionProbeObservation
{
    private readonly TaskCompletionSource<TransitionProbeOutcome> _probe =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TransitionProbeOutcome> Probe => _probe.Task;
    public Task Stopped => _stopped.Task;

    public void RecordProbe(TransitionProbeOutcome outcome) => _probe.TrySetResult(outcome);

    public void RecordProbeFailure(Exception exception) => _probe.TrySetException(exception);

    public void RecordStopped() => _stopped.TrySetResult();
}
