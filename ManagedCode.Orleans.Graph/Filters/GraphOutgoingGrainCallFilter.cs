using System;
using System.Threading.Tasks;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Orleans;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphOutgoingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IOutgoingGrainCallFilter
{
    private GrainTransitionManager? GraphManager => serviceProvider.GetService<GrainTransitionManager>();

    public Task Invoke(IOutgoingGrainCallContext context)
    {
        if (context.TrackOutgoingCall(graphCallFilterConfig))
        {
            GraphManager?.IsTransitionAllowed(context.GetCallHistory(), true);
            GraphManager?.DetectDeadlocks(context.GetCallHistory(), true);
        }

        return context.Invoke();
    }
}