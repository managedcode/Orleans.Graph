using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Concurrency;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphIncomingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IIncomingGrainCallFilter
{
    private readonly GrainTransitionManager? _graphManager = serviceProvider.GetService<GrainTransitionManager>();
    private readonly IGrainFactory? _grainFactory = serviceProvider.GetService<IGrainFactory>();

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var currentCaller = RequestContextHelper.CaptureCurrentCaller();
        var tracked = context.TrackIncomingCall(graphCallFilterConfig);
        var previousHistory = RequestContext.Get(Constants.RequestContextKey);
        var detachOneWay = context.InterfaceMethod.IsDefined(typeof(OneWayAttribute), inherit: true) &&
                           previousHistory is CallHistory;

        try
        {
            if (tracked)
            {
                var callHistory = context.GetCallHistory();

                if (!context.IsOrleansGraphTelemetryCall())
                {
                    _graphManager?.IsLatestTransitionAllowed(callHistory, true);
                }

                await ReportObservedEdgeAsync(context, callHistory);
            }

            if (detachOneWay)
            {
                RequestContext.Set(Constants.RequestContextKey, ((CallHistory)previousHistory!).Fork(detach: true));
            }

            await context.Invoke();
        }
        finally
        {
            if (detachOneWay)
            {
                RequestContext.Set(Constants.RequestContextKey, previousHistory!);
            }

            if (tracked)
            {
                RequestContextHelper.RestoreCurrentCaller(currentCaller);
            }
        }
    }

    private async Task ReportObservedEdgeAsync(IIncomingGrainCallContext context, CallHistory callHistory)
    {
        var observedCall = GrainTransitionManager.GetLatestObservedCall(callHistory);
        if (observedCall is null)
        {
            return;
        }

        if (context.Grain is IObservedGrainCallSink sink && context.IsOrleansGraphTelemetryCall())
        {
            sink.RecordObservedCall(observedCall);
            return;
        }

        if (RequestContextHelper.IsTelemetrySuppressed())
        {
            return;
        }

        if (_grainFactory is null)
        {
            return;
        }

        await RequestContextHelper.RunWithTelemetrySuppressedAsync(() =>
            _grainFactory
                .GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey)
                .RecordObservedCallAsync(observedCall));
    }
}
