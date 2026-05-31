using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainA : Grain, IGrainA, IRemindable
{
    private const int TimerOriginatedCallInput = 41;
    private const int ReminderOriginatedCallInput = 41;
    private const string ReminderOriginatedCallName = "reminder-originated-call";
    private static readonly TimeSpan TimerOriginatedCallDueTime = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan TimerOriginatedCallPeriod = Timeout.InfiniteTimeSpan;
    private static readonly TimeSpan ReminderOriginatedCallDueTime = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ReminderOriginatedCallPeriod = TimeSpan.FromSeconds(1);
    private IGrainTimer? _timerOriginatedCall;
    private int? _timerOriginatedCallResult;
    private string? _timerOriginatedCallFailure;
    private int? _reminderOriginatedCallResult;
    private string? _reminderOriginatedCallFailure;

    public async Task<int> MethodA1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodB1(int input)
    {
        return await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
            .MethodB1(input);
    }

    public async Task<int> MethodB2(int input)
    {
        return await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
            .MethodC2(input);
    }

    public async Task<int> MethodC1(int input)
    {
        return await GrainFactory.GetGrain<IGrainC>(this.GetPrimaryKeyString())
            .MethodC1(input);
    }

    public async Task<int> MethodComplexFlow(int input)
    {
        return await RunBranchingFlowAsync(input);
    }

    public async Task<int> MethodGrainOnlyComplexFlow(int input)
    {
        await ClearRuntimeGraphTelemetryAsync();

        return await RunBranchingFlowAsync(input);
    }

    public Task StartTimerOriginatedCallAsync()
    {
        _timerOriginatedCall?.Dispose();
        _timerOriginatedCallResult = null;
        _timerOriginatedCallFailure = null;

        _timerOriginatedCall = this.RegisterGrainTimer(
            RunTimerOriginatedCallAsync,
            new GrainTimerCreationOptions
            {
                DueTime = TimerOriginatedCallDueTime,
                Period = TimerOriginatedCallPeriod,
                Interleave = true,
                KeepAlive = false
            });

        return Task.CompletedTask;
    }

    public Task<int?> GetTimerOriginatedCallResultAsync()
    {
        return Task.FromResult(_timerOriginatedCallResult);
    }

    public Task<string?> GetTimerOriginatedCallFailureAsync()
    {
        return Task.FromResult(_timerOriginatedCallFailure);
    }

    public async Task StartReminderOriginatedCallAsync()
    {
        await UnregisterReminderOriginatedCallAsync();

        _reminderOriginatedCallResult = null;
        _reminderOriginatedCallFailure = null;

        await this.RegisterOrUpdateReminder(
            ReminderOriginatedCallName,
            ReminderOriginatedCallDueTime,
            ReminderOriginatedCallPeriod);
    }

    public Task<int?> GetReminderOriginatedCallResultAsync()
    {
        return Task.FromResult(_reminderOriginatedCallResult);
    }

    public Task<string?> GetReminderOriginatedCallFailureAsync()
    {
        return Task.FromResult(_reminderOriginatedCallFailure);
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (!string.Equals(reminderName, ReminderOriginatedCallName, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _reminderOriginatedCallResult = await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
                .MethodB1(ReminderOriginatedCallInput);
        }
        catch (Exception exception)
        {
            _reminderOriginatedCallFailure = $"{exception.GetType().FullName}: {exception.Message}";
        }
        finally
        {
            await UnregisterReminderOriginatedCallAsync();
        }
    }

    private async Task<int> RunBranchingFlowAsync(int input)
    {
        var grainKey = this.GetPrimaryKeyString();
        var fromB = await GrainFactory.GetGrain<IGrainB>(grainKey)
            .MethodB1(input);
        var fromC = await GrainFactory.GetGrain<IGrainC>(grainKey)
            .MethodBranchingFlow(fromB);

        return await GrainFactory.GetGrain<IGrainD>(grainKey)
            .MethodE2(fromC);
    }

    private async Task RunTimerOriginatedCallAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _timerOriginatedCallResult = await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
                .MethodB1(TimerOriginatedCallInput);
        }
        catch (Exception exception)
        {
            _timerOriginatedCallFailure = $"{exception.GetType().FullName}: {exception.Message}";
        }
        finally
        {
            _timerOriginatedCall?.Dispose();
            _timerOriginatedCall = null;
        }
    }

    private async Task UnregisterReminderOriginatedCallAsync()
    {
        var reminder = await this.GetReminder(ReminderOriginatedCallName);
        if (reminder is not null)
        {
            await this.UnregisterReminder(reminder);
        }
    }

    private async Task ClearRuntimeGraphTelemetryAsync()
    {
        await Task.Delay(100);
        await GrainFactory.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey).FlushAsync();
        await GrainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey).ClearAsync();
    }
}
