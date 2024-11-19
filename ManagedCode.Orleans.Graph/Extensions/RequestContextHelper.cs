using System;
using System.Linq;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Orleans;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class RequestContextHelper
{
    public static bool TrackIncomingCall(this IIncomingGrainCallContext context)
    {
        var call = context.GetCallHistory();
        //var caller = context.TargetContext!.GrainInstance!.GetType().Name;
        call.Push(new InCall(context.SourceId, context.TargetId, context.InterfaceName, context.MethodName));
        context.SetCallHistory(call);
        return true;
    }

    public static bool TrackOutgoingCall(this IOutgoingGrainCallContext context)
    {
        var caller = context.SourceId is null
            ? Constants.ClientCallerId
            : context.SourceContext!.GrainInstance!.GetType()
                .GetInterfaces()
                .FirstOrDefault(i => typeof(IGrain).IsAssignableFrom(i))?.FullName ?? string.Empty;
        
        var call = context.GetCallHistory();
        call.Push(new OutCall(context.SourceId, context.TargetId, caller, context.InterfaceName, context.MethodName));
        context.SetCallHistory(call);
        return true;
    }

    public static bool TrackIncomingCall(this IIncomingGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig)
    {
        if (!graphCallFilterConfig.TrackOrleansCalls && context.ImplementationMethod.Module.Name.StartsWith("Orleans."))
            return false;

        return context.TrackIncomingCall();
    }

    public static bool TrackOutgoingCall(this IOutgoingGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig)
    {
        if (!graphCallFilterConfig.TrackOrleansCalls && context.InterfaceMethod.Module.Name.StartsWith("Orleans."))
            return false;

        return context.TrackOutgoingCall();
    }

    public static CallHistory GetCallHistory(this IGrainCallContext context)
    {
        return RequestContext.Get(Constants.RequestContextKey) as CallHistory ?? new CallHistory();
    }

    public static bool IsCallHistoryExist(this IGrainCallContext context)
    {
        return RequestContext.Get(Constants.RequestContextKey) is CallHistory;
    }

    public static void SetCallHistory(this IGrainCallContext context, CallHistory callHistory)
    {
        RequestContext.Set(Constants.RequestContextKey, callHistory);
    }
}