using System;
using System.Threading.Tasks;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Orleans;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphIncomingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IIncomingGrainCallFilter
{
    private GrainGraphManager? GraphManager => serviceProvider.GetService<GrainGraphManager>();

    public Task Invoke(IIncomingGrainCallContext context)
    {
        if (context.TrackIncomingCall(graphCallFilterConfig))
        {
            if (GraphManager?.IsTransitionAllowed(context.GetCallHistory()) == false )
            {
                throw new InvalidOperationException("Transition is not allowed.\n" + context.GetCallHistory());
            }
        }

        return context.Invoke();
    }
}