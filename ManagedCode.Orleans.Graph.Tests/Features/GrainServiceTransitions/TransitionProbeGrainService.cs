using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManagedCode.Orleans.Graph.Tests.Features.GrainServiceTransitions;

public sealed class TransitionProbeGrainService(
    GrainId grainId,
    Silo silo,
    ILoggerFactory loggerFactory,
    ILocalSiloDetails localSilo,
    TransitionProbeObservation observation)
    : GrainService(grainId, silo, loggerFactory), ITransitionProbeGrainService
{
    private IGrainFactory? _grainFactory;

    public override Task Init(IServiceProvider serviceProvider)
    {
        _grainFactory = serviceProvider.GetRequiredService<IGrainFactory>();
        return base.Init(serviceProvider);
    }

    protected override async Task StartInBackground()
    {
        await base.StartInBackground();
        try
        {
            var grainFactory = _grainFactory ?? throw new InvalidOperationException("The probe service was not initialized.");
            var target = grainFactory.GetGrain<ITransitionProbeGrain>(localSilo.SiloAddress.ToString());
            var cancellationToken = StoppedCancellationTokenSource.Token;
            var firstAllowed = await target.AllowedAsync(cancellationToken);
            string? deniedTransition = null;
            string? deniedBodyResult = null;
            try
            {
                deniedBodyResult = await target.DeniedAsync(cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                deniedTransition = exception.Message;
            }

            var finalAllowed = await target.AllowedAsync(cancellationToken);
            observation.RecordProbe(new TransitionProbeOutcome(firstAllowed, deniedTransition, deniedBodyResult, finalAllowed));
        }
        catch (Exception exception)
        {
            observation.RecordProbeFailure(exception);
            throw;
        }
    }

    public override async Task Stop()
    {
        try
        {
            await base.Stop();
        }
        finally
        {
            observation.RecordStopped();
        }
    }
}
