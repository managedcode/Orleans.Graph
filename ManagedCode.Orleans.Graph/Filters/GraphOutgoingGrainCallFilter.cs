using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphOutgoingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IOutgoingGrainCallFilter
{
    GrainGraphManager graphManager => serviceProvider.GetService<GrainGraphManager>();
    
    
    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        context.TrackOutgoingCall(graphCallFilterConfig);
        
        var transitionAllowed = graphManager?.IsTransitionAllowed(context.Grain.GetType(), context.Grain.GetType());
        
        await context.Invoke();
        
        if (context.IsCallHistoryExist())
        {
            var history = context.GetCallHistory();
            var x = 5;
        }
    }
}


        
        
// var id = RequestContext.Get("ID") ?? Guid.NewGuid().ToString();
// RequestContext.Set("ID", id);
//
// var sourceContext = context.SourceContext;
// var interfaceNameSource = sourceContext?.GrainReference?.InterfaceName;
//
// var interfaceName = context.InterfaceName;
// var methodName = context.MethodName;
//
//
// var callerGrainType = context.SourceContext?.GrainInstance?.GetType().Name ?? "CLIENT";
//
// var grainType = context.Grain.GetType().Name;
//
// var targetInterface = context.Request.GetInterfaceName();
// var tergettMethod = context.Request.GetMethod().Name;
//
// if (interfaceName.StartsWith("ManagedCode"))
// {
//     var x = 5;
//     var transitionAllowed = graphManager?.IsTransitionAllowed(context.Grain.GetType(), context.Grain.GetType());
//     RequestContext.Set(Guid.NewGuid().ToString(), $"out => {callerGrainType} > {targetInterface}.{tergettMethod}");
// }
//
//
//
// await context.Invoke();
//
// if (interfaceName.StartsWith("ManagedCode"))
// {
//     var x = 5;
//     var xx = RequestContext.Entries?.ToArray() ?? [];
// }