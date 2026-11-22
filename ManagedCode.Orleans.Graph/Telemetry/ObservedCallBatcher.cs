using System.Collections.Concurrent;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Telemetry;

internal sealed class ObservedCallBatcher(IGrainFactory grainFactory)
{
    private const int MaximumBatchSize = 64;
    private readonly ConcurrentQueue<PendingObservation> _pending = new();
    private int _reporting;

    public Task ReportAsync(ObservedGrainCall observation)
    {
        if (_pending.IsEmpty && TryStartReporting())
        {
            return ReportImmediatelyAsync(observation);
        }

        var pending = new PendingObservation(observation);
        _pending.Enqueue(pending);
        SchedulePendingReports();
        return pending.Task;
    }

    private IOrleansGraphTelemetryWorker Worker =>
        field ??= grainFactory.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey);

    private bool TryStartReporting() => Interlocked.CompareExchange(ref _reporting, 1, 0) == 0;

    private async Task ReportImmediatelyAsync(ObservedGrainCall observation)
    {
        try
        {
            RequestContext.Remove(Constants.RequestContextKey);
            RequestContext.Set(Constants.TelemetrySuppressionContextKey, true);
            await Worker.RecordObservedCallAsync(observation).ConfigureAwait(false);
        }
        finally
        {
            FinishReporting();
        }
    }

    private void SchedulePendingReports()
    {
        if (!_pending.IsEmpty && TryStartReporting())
        {
            // The batch spans calls from different activations. Dispatch it without any one caller's context.
            ThreadPool.UnsafeQueueUserWorkItem(static batcher => _ = batcher.ReportPendingAsync(), this, preferLocal: false);
        }
    }

    private async Task ReportPendingAsync()
    {
        try
        {
            var observers = new List<PendingObservation>(MaximumBatchSize);
            var observations = new List<ObservedGrainCall>(MaximumBatchSize);
            RequestContext.Set(Constants.TelemetrySuppressionContextKey, true);
            while (_pending.TryDequeue(out var pending))
            {
                observers.Add(pending);
                observations.Add(pending.Observation);
                while (observers.Count < MaximumBatchSize && _pending.TryDequeue(out pending))
                {
                    observers.Add(pending);
                    observations.Add(pending.Observation);
                }

                try
                {
                    await Worker.RecordObservedCallsAsync(observations).ConfigureAwait(false);
                    foreach (var observer in observers)
                    {
                        observer.SetResult();
                    }
                }
                catch (Exception exception)
                {
                    foreach (var observer in observers)
                    {
                        observer.SetException(exception);
                    }
                }

                observers.Clear();
                observations.Clear();
            }
        }
        finally
        {
            FinishReporting();
        }
    }

    private void FinishReporting()
    {
        Volatile.Write(ref _reporting, 0);
        SchedulePendingReports();
    }

    private sealed class PendingObservation(ObservedGrainCall observation)
        : TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
    {
        public ObservedGrainCall Observation { get; } = observation;
    }
}
