using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

[StatelessWorker]
public class StatelessWorkerCallerGrain : Grain, IStatelessWorkerCallerGrain
{
    private const int TimerOriginatedCallInput = 41;
    private static readonly TimeSpan TimerOriginatedCallDueTime = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan TimerOriginatedCallPeriod = Timeout.InfiniteTimeSpan;
    private IGrainTimer? _timerOriginatedCall;

    public async Task<int> CallGrainBAsync(int input)
    {
        return await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
            .MethodB1(input);
    }

    public async Task<StatelessWorkerCallResult> CallGrainBWithActivationAsync(int input, int delayMilliseconds)
    {
        await Task.Delay(delayMilliseconds);

        var result = await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
            .MethodB1(input);

        return new StatelessWorkerCallResult(result, GrainContext.ActivationId.ToString());
    }

    public async Task StartTimerOriginatedCallAsync()
    {
        _timerOriginatedCall?.Dispose();
        await GetStateGrain().ResetTimerOriginatedCallAsync();

        _timerOriginatedCall = this.RegisterGrainTimer(
            RunTimerOriginatedCallAsync,
            new GrainTimerCreationOptions
            {
                DueTime = TimerOriginatedCallDueTime,
                Period = TimerOriginatedCallPeriod,
                Interleave = true,
                KeepAlive = false
            });
    }

    public async Task<int?> GetTimerOriginatedCallResultAsync()
    {
        return await GetStateGrain().GetTimerOriginatedCallResultAsync();
    }

    public async Task<string?> GetTimerOriginatedCallFailureAsync()
    {
        return await GetStateGrain().GetTimerOriginatedCallFailureAsync();
    }

    private async Task RunTimerOriginatedCallAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var result = await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
                .MethodB1(TimerOriginatedCallInput);

            await GetStateGrain().RecordTimerOriginatedCallResultAsync(result);
        }
        catch (Exception exception)
        {
            await GetStateGrain().RecordTimerOriginatedCallFailureAsync(
                $"{exception.GetType().FullName}: {exception.Message}");
        }
        finally
        {
            _timerOriginatedCall?.Dispose();
            _timerOriginatedCall = null;
        }
    }

    private IStatelessWorkerCallStateGrain GetStateGrain()
    {
        return GrainFactory.GetGrain<IStatelessWorkerCallStateGrain>(this.GetPrimaryKeyString());
    }
}
