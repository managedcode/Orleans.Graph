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
        var caller = ResolveCallerInterface(context);

        var call = context.GetCallHistory();
        call.Push(new OutCall(context.SourceId, context.TargetId, caller, context.InterfaceName, context.MethodName));
        context.SetCallHistory(call);
        return true;
    }

    public static bool TrackIncomingCall(this IIncomingGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig)
    {
        if (!graphCallFilterConfig.TrackOrleansCalls && context.ImplementationMethod.Module.Name.StartsWith("Orleans."))
        {
            return false;
        }

        return context.TrackIncomingCall();
    }

    public static bool TrackOutgoingCall(this IOutgoingGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig)
    {
        if (!graphCallFilterConfig.TrackOrleansCalls && context.InterfaceMethod.Module.Name.StartsWith("Orleans."))
        {
            return false;
        }

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

    private static string ResolveCallerInterface(IOutgoingGrainCallContext context)
    {
        if (!context.SourceId.HasValue)
        {
            return Constants.ClientCallerId;
        }

        var normalizedName = NormalizeGrainTypeName(context.SourceId.Value.Type.ToString() ?? string.Empty);

        if (context.SourceContext?.GrainInstance is { } instance)
        {
            var grainInterfaces = instance.GetType()
                .GetInterfaces()
                .Where(i => typeof(IGrain).IsAssignableFrom(i))
                .ToArray();

            var directMatch = grainInterfaces.FirstOrDefault(i =>
                string.Equals(i.FullName, normalizedName, StringComparison.Ordinal));

            if (directMatch?.FullName is not null)
            {
                return directMatch.FullName;
            }

            var firstInterface = grainInterfaces
                .OrderBy(i => i.FullName, StringComparer.Ordinal)
                .FirstOrDefault();

            if (firstInterface?.FullName is not null)
            {
                return firstInterface.FullName;
            }
        }

        return normalizedName;
    }

    private static string NormalizeGrainTypeName(string grainType)
    {
        if (string.IsNullOrWhiteSpace(grainType))
        {
            return string.Empty;
        }

        var value = grainType.Trim();

        var colonIndex = value.LastIndexOf(':');
        if (colonIndex >= 0 && colonIndex < value.Length - 1)
        {
            value = value[(colonIndex + 1)..].Trim();
        }

        if (value.StartsWith('[') && value.EndsWith(']') && value.Length > 2)
        {
            value = value[1..^1];
        }

        return value;
    }
}
