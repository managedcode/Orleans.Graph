using System.Runtime.InteropServices;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Telemetry;

[StatelessWorker(1)]
public sealed class OrleansGraphTelemetryWorker(GraphCallFilterConfig graphCallFilterConfig) : Grain, IOrleansGraphTelemetryWorker, IObservedGrainCallSink
{
    private static readonly TimeSpan _defaultFlushPeriod = TimeSpan.FromSeconds(1);
    private readonly Dictionary<ObservedGrainCallKey, ObservedGrainCallAccumulator> _observedCalls = new();

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var flushPeriod = graphCallFilterConfig.LiveGraphFlushPeriod > TimeSpan.Zero
            ? graphCallFilterConfig.LiveGraphFlushPeriod
            : _defaultFlushPeriod;

        this.RegisterGrainTimer(
            FlushTimerAsync,
            new GrainTimerCreationOptions
            {
                DueTime = flushPeriod,
                Period = flushPeriod,
                Interleave = true,
                KeepAlive = false
            });

        return Task.CompletedTask;
    }

    public Task RecordObservedCallsAsync(IReadOnlyCollection<ObservedGrainCall> observedCalls)
    {
        ArgumentNullException.ThrowIfNull(observedCalls);

        RecordObservedCalls(observedCalls);
        return Task.CompletedTask;
    }

    public Task RecordObservedCallAsync(ObservedGrainCall observedCall)
    {
        RecordObservedCall(observedCall);
        return Task.CompletedTask;
    }

    public async Task FlushAsync()
    {
        if (_observedCalls.Count == 0)
        {
            return;
        }

        var snapshot = CreateSnapshot();
        _observedCalls.Clear();

        try
        {
            await RequestContextHelper.RunWithTelemetrySuppressedAsync(() =>
                RequestContextHelper.RunWithCurrentCallerAsync(
                    typeof(IOrleansGraphTelemetryWorker).GetTypeName(),
                    nameof(IOrleansGraphTelemetryWorker.FlushAsync),
                    () => GrainFactory
                        .GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey)
                        .MergeObservedCallsAsync(snapshot)));
        }
        catch
        {
            RecordObservedCalls(snapshot);
            throw;
        }
    }

    public void RecordObservedCalls(IReadOnlyCollection<ObservedGrainCall> observedCalls)
    {
        foreach (var observedCall in observedCalls)
        {
            RecordObservedCall(observedCall);
        }
    }

    public void RecordObservedCall(ObservedGrainCall observedCall)
    {
        var key = ObservedGrainCallKey.From(observedCall);
        ref var accumulator = ref CollectionsMarshal.GetValueRefOrAddDefault(_observedCalls, key, out var exists);
        if (exists)
        {
            accumulator.Merge(observedCall);
            return;
        }

        accumulator = new ObservedGrainCallAccumulator(observedCall);
    }

    private ObservedGrainCall[] CreateSnapshot()
    {
        var snapshot = new ObservedGrainCall[_observedCalls.Count];
        var index = 0;
        foreach (var accumulator in _observedCalls.Values)
        {
            snapshot[index++] = accumulator.ToObservedGrainCall();
        }

        return snapshot;
    }

    private Task FlushTimerAsync(CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested ? Task.CompletedTask : FlushAsync();
    }
}
