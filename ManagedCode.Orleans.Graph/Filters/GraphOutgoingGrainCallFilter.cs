using System;
using System.Threading.Tasks;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Orleans;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphOutgoingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IOutgoingGrainCallFilter
{
    private GrainGraphManager? GraphManager => serviceProvider.GetService<GrainGraphManager>();

    public Task Invoke(IOutgoingGrainCallContext context)
    {
        if (context.TrackOutgoingCall(graphCallFilterConfig))
        {
            if (GraphManager?.IsTransitionAllowed(context.GetCallHistory()) == false )
            {
                throw new InvalidOperationException("Transition is not allowed.\n" + context.GetCallHistory());
            }
        }

        return context.Invoke();
    }
}