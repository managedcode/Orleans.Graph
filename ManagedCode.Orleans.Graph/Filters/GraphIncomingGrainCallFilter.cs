using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphIncomingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IIncomingGrainCallFilter
{
    private GrainTransitionManager? GraphManager => serviceProvider.GetService<GrainTransitionManager>();

    public Task Invoke(IIncomingGrainCallContext context)
    {
        if (context.TrackIncomingCall(graphCallFilterConfig))
        {
            GraphManager?.IsTransitionAllowed(context.GetCallHistory(), true);
            GraphManager?.DetectDeadlocks(context.GetCallHistory(), true);
        }

        return context.Invoke();
    }
}
