using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainA : Grain, IGrainA, IRemindable
{
    private const int TimerOriginatedCallInput = 41;
    private const int ReminderOriginatedCallInput = 41;
    private const string ReminderOriginatedCallName = "reminder-originated-call";
    private static readonly TimeSpan _timerOriginatedCallDueTime = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan _timerOriginatedCallPeriod = Timeout.InfiniteTimeSpan;
    private static readonly TimeSpan _reminderOriginatedCallDueTime = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan _reminderOriginatedCallPeriod = TimeSpan.FromSeconds(1);
    private IGrainTimer? _timerOriginatedCall;
    private int? _timerOriginatedCallResult;
    private string? _timerOriginatedCallFailure;
    private int? _reminderOriginatedCallResult;
    private string? _reminderOriginatedCallFailure;

    public async Task<int> MethodA1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<string> CallPeerAsync(string peerKey, int remainingHops)
    {
        var currentKey = this.GetPrimaryKeyString();
        if (remainingHops == 0)
        {
            return currentKey;
        }

        using var suppression = RequestContext.SuppressCallChainReentrancy();
        return await GrainFactory.GetGrain<IGrainA>(peerKey).CallPeerAsync(currentKey, remainingHops - 1);
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

    public async Task<int> MethodGrainOnlyComplexFlow(int input, DateTime startedAt)
    {
        await ClearRuntimeGraphTelemetryAsync(startedAt);

        return await RunBranchingFlowAsync(input);
    }

    public async Task<int[]> ObserveParallelCallHistoryAsync(int calls)
    {
        var grain = GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString());
        return await Task.WhenAll(Enumerable.Range(0, calls)
            .Select(_ => grain.GetCallHistoryDepthAsync()));
    }

    public Task StartOneWayHistoryProbeAsync()
    {
        _ = GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
            .RecordOneWayHistoryDepthAsync();
        return Task.CompletedTask;
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
                DueTime = _timerOriginatedCallDueTime,
                Period = _timerOriginatedCallPeriod,
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
            _reminderOriginatedCallDueTime,
            _reminderOriginatedCallPeriod);
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

    private async Task ClearRuntimeGraphTelemetryAsync(DateTime startedAt)
    {
        var telemetry = GrainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey);
        var timeProvider = GrainContext.ActivationServices.GetRequiredService<TimeProvider>();
        if (RequestContext.Get(Constants.RequestContextKey) is CallHistory history &&
            GrainTransitionManager.GetLatestObservedCall(history, timeProvider) is { } incomingCall)
        {
            // Wait for the silo-local worker to publish the entry edge before clearing it.
            var key = ObservedGrainCallKey.From(incomingCall);
            for (var attempt = 0; attempt < 100; attempt++)
            {
                var graph = await telemetry.GetObservedGraphAsync();
                if (graph.Edges.Any(edge => edge.LastSeenUtc >= startedAt && ObservedGrainCallKey.From(edge) == key))
                {
                    await telemetry.ClearAsync();
                    return;
                }

                await Task.Delay(50);
            }

            throw new InvalidOperationException($"Entry edge {key} was not published before the grain-only flow.");
        }

        await telemetry.ClearAsync();
    }
}
