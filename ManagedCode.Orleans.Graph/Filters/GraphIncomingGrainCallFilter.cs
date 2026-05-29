using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphIncomingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IIncomingGrainCallFilter
{
    private GrainTransitionManager? GraphManager => serviceProvider.GetService<GrainTransitionManager>();
    private IGrainFactory? GrainFactory => serviceProvider.GetService<IGrainFactory>();

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var currentCaller = RequestContextHelper.CaptureCurrentCaller();
        var tracked = context.TrackIncomingCall(graphCallFilterConfig);

        try
        {
            if (tracked)
            {
                if (!context.IsOrleansGraphTelemetryCall())
                {
                    GraphManager?.IsTransitionAllowed(context.GetCallHistory(), true);
                    GraphManager?.DetectDeadlocks(context.GetCallHistory(), true);
                }

                await ReportObservedEdgeAsync(context);
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

    private async Task ReportObservedEdgeAsync(IIncomingGrainCallContext context)
    {
        var observedEdge = GrainTransitionManager.GetLatestObservedCall(context.GetCallHistory());
        if (observedEdge is null)
        {
            return;
        }

        var observedCalls = new[] { observedEdge };
        if (context.Grain is IObservedGrainCallSink sink && context.IsOrleansGraphTelemetryCall())
        {
            sink.RecordObservedCalls(observedCalls);
            return;
        }

        if (RequestContextHelper.IsTelemetrySuppressed())
        {
            return;
        }

        if (GrainFactory is null)
        {
            return;
        }

        await RequestContextHelper.RunWithTelemetrySuppressedAsync(() =>
            GrainFactory
                .GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey)
                .RecordAsync(observedCalls));
    }
}
