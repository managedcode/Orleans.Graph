using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphIncomingGrainCallFilter(IServiceProvider serviceProvider) : IIncomingGrainCallFilter
{
    GrainGraphManager graphManager => serviceProvider.GetService<GrainGraphManager>();
    
    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var id = RequestContext.Get("ID") ?? Guid.NewGuid().ToString();
        RequestContext.Set("ID", id);
        
        var targetContext = context.TargetContext;
        var interfaceNameSource = targetContext?.GrainReference?.InterfaceName;
        
        var interfaceName = context.InterfaceName;
        var methodName = context.MethodName;
        
        
        var grainType = context.Grain.GetType().Name;
        
        var callerGrainType = context.TargetContext?.GrainInstance?.GetType().Name;
        
        var requestInterface = context.Request.GetInterfaceName();
        var requestMethod = context.Request.GetMethod().Name;

        if (interfaceName.StartsWith("ManagedCode"))
        {
            var x = 5;
            var transitionAllowed = graphManager?.IsTransitionAllowed(context.TargetContext?.GrainInstance?.GetType(), context.TargetContext?.GrainInstance?.GetType());
            RequestContext.Set(Guid.NewGuid().ToString(), $"in = > {requestInterface}.{requestMethod}");
        }
        

        
        await context.Invoke();
        
        if (interfaceName.StartsWith("ManagedCode"))
        {
            var x = 5;
            var xx = RequestContext.Entries?.ToArray() ?? [];
        }

    }
}