using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphOutgoingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IOutgoingGrainCallFilter
{
    private readonly GrainTransitionManager? _graphManager = serviceProvider.GetService<GrainTransitionManager>();

    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        var previous = RequestContext.Get(Constants.RequestContextKey);
        if (previous is CallHistory parentHistory)
        {
            RequestContext.Set(Constants.RequestContextKey, parentHistory.Fork());
        }

        try
        {
            if (context.TrackOutgoingCall(graphCallFilterConfig) &&
                !context.IsOrleansGraphTelemetryCall())
            {
                var callHistory = context.GetCallHistory();
                _graphManager?.DetectLatestDeadlock(callHistory, true);
                _graphManager?.IsLatestTransitionAllowed(callHistory, true);
            }

            await context.Invoke();
        }
        finally
        {
            if (previous is CallHistory)
            {
                RequestContext.Set(Constants.RequestContextKey, previous);
            }
        }
    }
}
