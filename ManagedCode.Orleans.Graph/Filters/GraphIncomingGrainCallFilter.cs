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
        if (context.TrackIncomingCall(graphCallFilterConfig))
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

    private async Task ReportObservedEdgeAsync(IIncomingGrainCallContext context)
    {
        var observedEdge = GrainTransitionManager.GetLatestObservedEdge(context.GetCallHistory());
        if (observedEdge is null)
        {
            return;
        }

        var observedEdges = new[] { observedEdge };
        if (context.Grain is IObservedGrainCallSink sink && context.IsOrleansGraphTelemetryCall())
        {
            sink.RecordObservedEdges(observedEdges);
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
                .RecordAsync(observedEdges));
    }
}
