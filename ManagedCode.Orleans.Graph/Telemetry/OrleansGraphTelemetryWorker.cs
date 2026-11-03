using System.Runtime.InteropServices;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.Hosting;
using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Telemetry;

[StatelessWorker(1)]
public sealed class OrleansGraphTelemetryWorker(
    GraphCallFilterConfig graphCallFilterConfig,
    IHostApplicationLifetime applicationLifetime,
    ISiloStatusOracle siloStatusOracle) : Grain, IOrleansGraphTelemetryWorker, IObservedGrainCallSink
{
    private readonly Dictionary<ObservedGrainCallKey, ObservedGrainCallAccumulator> _observedCalls = new();

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var flushPeriod = graphCallFilterConfig.LiveGraphFlushPeriod > TimeSpan.Zero
            ? graphCallFilterConfig.LiveGraphFlushPeriod
            : GraphCallFilterConfig.DefaultLiveGraphFlushPeriod;

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

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (reason.ReasonCode == DeactivationReasonCode.ShuttingDown || cancellationToken.IsCancellationRequested)
        {
            // Live graph observations are process-local diagnostics, not durable records.
            // The final silo cannot activate a telemetry target while its catalog closes.
            _observedCalls.Clear();
            return Task.CompletedTask;
        }

        return FlushBufferedCallsAsync(deactivateWhenEmpty: false);
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

    public Task FlushAsync()
    {
        return FlushBufferedCallsAsync(deactivateWhenEmpty: true);
    }

    private async Task FlushBufferedCallsAsync(bool deactivateWhenEmpty)
    {
        if (applicationLifetime.ApplicationStopping.IsCancellationRequested ||
            applicationLifetime.ApplicationStopped.IsCancellationRequested ||
            siloStatusOracle.CurrentStatus.IsTerminating())
        {
            _observedCalls.Clear();
            return;
        }

        if (_observedCalls.Count == 0)
        {
            if (deactivateWhenEmpty)
            {
                DeactivateOnIdle();
            }

            return;
        }

        var snapshot = ObservedGrainCallAccumulator.CreateSnapshot(_observedCalls);
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

    private Task FlushTimerAsync(CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested ? Task.CompletedTask : FlushAsync();
    }
}
