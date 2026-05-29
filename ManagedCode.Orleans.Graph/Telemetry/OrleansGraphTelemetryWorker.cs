using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Telemetry;

[StatelessWorker(1)]
public sealed class OrleansGraphTelemetryWorker(GraphCallFilterConfig graphCallFilterConfig) : Grain, IOrleansGraphTelemetryWorker, IObservedGrainCallSink
{
    private static readonly TimeSpan _defaultFlushPeriod = TimeSpan.FromSeconds(1);
    private readonly Dictionary<ObservedGrainCallKey, ObservedGrainCall> _edges = new();

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

    public Task RecordAsync(IReadOnlyCollection<ObservedGrainCall> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        RecordObservedCalls(edges);
        return Task.CompletedTask;
    }

    public async Task FlushAsync()
    {
        if (_edges.Count == 0)
        {
            return;
        }

        var snapshot = _edges.Values.ToArray();
        _edges.Clear();

        try
        {
            await RequestContextHelper.RunWithTelemetrySuppressedAsync(() =>
                GrainFactory
                    .GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey)
                    .MergeAsync(snapshot));
        }
        catch
        {
            RecordObservedCalls(snapshot);
            throw;
        }
    }

    public void RecordObservedCalls(IReadOnlyCollection<ObservedGrainCall> edges)
    {
        foreach (var edge in edges)
        {
            var key = ObservedGrainCallKey.From(edge);
            _edges[key] = _edges.TryGetValue(key, out var existing)
                ? existing.Merge(edge)
                : edge;
        }
    }

    private Task FlushTimerAsync(CancellationToken cancellationToken) => FlushAsync();
}
