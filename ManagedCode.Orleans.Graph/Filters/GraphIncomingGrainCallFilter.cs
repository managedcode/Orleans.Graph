using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphIncomingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IIncomingGrainCallFilter
{
    private readonly GrainTransitionManager? _graphManager = serviceProvider.GetService<GrainTransitionManager>();
    private readonly IGrainFactory? _grainFactory = serviceProvider.GetService<IGrainFactory>();

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var currentCaller = RequestContextHelper.CaptureCurrentCaller();
        var tracked = context.TrackIncomingCall(graphCallFilterConfig);

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

            await context.Invoke();
        }
        finally
        {
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
