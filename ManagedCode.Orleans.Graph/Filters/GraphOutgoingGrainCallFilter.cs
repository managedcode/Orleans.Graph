using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphOutgoingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IOutgoingGrainCallFilter
{
    private readonly GrainTransitionManager? _graphManager = serviceProvider.GetService<GrainTransitionManager>();

    public Task Invoke(IOutgoingGrainCallContext context)
    {
        if (context.TrackOutgoingCall(graphCallFilterConfig))
        {
            if (!context.IsOrleansGraphTelemetryCall())
            {
                var callHistory = context.GetCallHistory();
                _graphManager?.DetectLatestDeadlock(callHistory, true);
                _graphManager?.IsLatestTransitionAllowed(callHistory, true);
            }
        }

        return context.Invoke();
    }
}
