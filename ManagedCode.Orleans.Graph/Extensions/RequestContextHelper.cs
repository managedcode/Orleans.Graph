using System.Linq;
using ManagedCode.Orleans.Graph.Models;
using Orleans;
using Orleans.Runtime;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class RequestContextHelper
{
    public static void TrackIncomingCall(this IIncomingGrainCallContext context)
    {
        CallHistory call = context.GetCallHistory();
        call.Push( new InCall($"{context.Request.GetInterfaceName()}.{context.Request.GetMethod().Name}"));
        context.SetCallHistory(call);
    }
    
    public static void TrackIncomingCall(this IIncomingGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig)
    {
        if(!graphCallFilterConfig.TrackOrleansCalls && context.Request.GetType().Namespace!.Contains("Orleans"))
            return;
        
        context.TrackIncomingCall();
    }
    
    public static void TrackOutgoingCall(this IOutgoingGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig)
    {
        if(!graphCallFilterConfig.TrackOrleansCalls && context.InterfaceName.Contains("Orleans"))
            return;
            
        context.TrackOutgoingCall();
    }
    
    public static void TrackOutgoingCall(this IOutgoingGrainCallContext context)
    {
        CallHistory call = context.GetCallHistory();
        call.Push( new OutCall(context.SourceContext?.GrainInstance?.GetType().Name ?? Interfaces.Constants.ClientCallerId, $"{context.Request.GetInterfaceName()}.{context.Request.GetMethod().Name}"));
        context.SetCallHistory(call);
    }
    
    public static CallHistory GetCallHistory(this IGrainCallContext context)
    {
        return RequestContext.Get(Interfaces.Constants.RequestContextKey) as CallHistory ?? new CallHistory();
    }
    
    public static bool IsCallHistoryExist(this IGrainCallContext context)
    {
        return RequestContext.Get(Interfaces.Constants.RequestContextKey) is CallHistory;
    }
    
    public static void SetCallHistory(this IGrainCallContext context, CallHistory callHistory)
    {
        RequestContext.Set(Interfaces.Constants.RequestContextKey, callHistory);
    }
}