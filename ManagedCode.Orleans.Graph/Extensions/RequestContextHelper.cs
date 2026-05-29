using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class RequestContextHelper
{
    public static bool TrackIncomingCall(this IIncomingGrainCallContext context)
    {
        EnsureValidGrainIdentity(context.InterfaceName, context.MethodName);

        var call = context.GetCallHistory();
        call.Push(new InCall(context.SourceId, context.TargetId, context.InterfaceName, context.MethodName));
        context.SetCallHistory(call);
        SetCurrentCaller(context.InterfaceName, context.MethodName);
        return true;
    }

    public static bool TrackOutgoingCall(this IOutgoingGrainCallContext context)
    {
        EnsureValidGrainIdentity(context.InterfaceName, context.MethodName);

        var caller = ResolveCallerInterface(context);
        var callerMethod = ResolveCallerMethod(context);

        var call = context.GetCallHistory();
        call.Push(new OutCall(
            context.SourceId,
            context.TargetId,
            caller,
            context.InterfaceName,
            context.MethodName,
            callerMethod));
        context.SetCallHistory(call);
        return true;
    }

    public static bool TrackIncomingCall(this IIncomingGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig)
    {
        if (context.ShouldSkipTracking(graphCallFilterConfig, context.ImplementationMethod.Module.Name))
        {
            return false;
        }

        return context.TrackIncomingCall();
    }

    public static bool TrackOutgoingCall(this IOutgoingGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig)
    {
        if (context.ShouldSkipTracking(graphCallFilterConfig, context.InterfaceMethod.Module.Name))
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

    public static bool IsOrleansGraphTelemetryCall(this IGrainCallContext context)
    {
        return string.Equals(context.InterfaceName, typeof(IOrleansGraphTelemetryWorker).FullName, StringComparison.Ordinal) ||
               string.Equals(context.InterfaceName, typeof(IOrleansGraphTelemetryGrain).FullName, StringComparison.Ordinal);
    }

    public static bool IsTelemetrySuppressed()
    {
        return RequestContext.Get(Constants.TelemetrySuppressionContextKey) is true;
    }

    public static (object? Caller, object? Method) CaptureCurrentCaller()
    {
        return (
            RequestContext.Get(Constants.CurrentCallerInterfaceContextKey),
            RequestContext.Get(Constants.CurrentCallerMethodContextKey));
    }

    public static void RestoreCurrentCaller((object? Caller, object? Method) currentCaller)
    {
        RestoreRequestContextValue(Constants.CurrentCallerInterfaceContextKey, currentCaller.Caller);
        RestoreRequestContextValue(Constants.CurrentCallerMethodContextKey, currentCaller.Method);
    }

    public static async Task RunWithTelemetrySuppressedAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var previous = RequestContext.Get(Constants.TelemetrySuppressionContextKey);
        RequestContext.Set(Constants.TelemetrySuppressionContextKey, true);

        try
        {
            await action();
        }
        finally
        {
            if (previous is null)
            {
                RequestContext.Remove(Constants.TelemetrySuppressionContextKey);
            }
            else
            {
                RequestContext.Set(Constants.TelemetrySuppressionContextKey, previous);
            }
        }
    }

    private static bool ShouldSkipTracking(this IGrainCallContext context, GraphCallFilterConfig graphCallFilterConfig, string moduleName)
    {
        if (!graphCallFilterConfig.TrackOrleansCalls && moduleName.StartsWith("Orleans.", StringComparison.Ordinal))
        {
            return true;
        }

        if (!graphCallFilterConfig.TrackOrleansGraphInternalCalls && context.IsOrleansGraphTelemetryCall())
        {
            return true;
        }

        if (!graphCallFilterConfig.TrackOrleansGraphInternalCalls && IsTelemetrySuppressed())
        {
            return true;
        }

        return false;
    }

    private static string ResolveCallerInterface(IOutgoingGrainCallContext context)
    {
        if (!context.SourceId.HasValue)
        {
            return Constants.ClientCallerId;
        }

        if (RequestContext.Get(Constants.CurrentCallerInterfaceContextKey) is string caller &&
            IsValidGrainIdentity(caller))
        {
            return caller;
        }

        throw new InvalidOperationException(
            $"Unable to resolve caller grain interface for outgoing call to {context.InterfaceName}.{context.MethodName}.");
    }

    private static string ResolveCallerMethod(IOutgoingGrainCallContext context)
    {
        if (!context.SourceId.HasValue)
        {
            return Constants.AnyMethod;
        }

        if (RequestContext.Get(Constants.CurrentCallerMethodContextKey) is string method &&
            !string.IsNullOrWhiteSpace(method))
        {
            return method;
        }

        throw new InvalidOperationException(
            $"Unable to resolve caller grain method for outgoing call to {context.InterfaceName}.{context.MethodName}.");
    }

    private static void SetCurrentCaller(string caller, string method)
    {
        RequestContext.Set(Constants.CurrentCallerInterfaceContextKey, caller);
        RequestContext.Set(Constants.CurrentCallerMethodContextKey, method);
    }

    private static void RestoreRequestContextValue(string key, object? value)
    {
        if (value is null)
        {
            RequestContext.Remove(key);
            return;
        }

        RequestContext.Set(key, value);
    }

    private static void EnsureValidGrainIdentity(string grainType, string method)
    {
        if (IsValidGrainIdentity(grainType))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Resolved Orleans graph identity for {grainType}.{method} is not a concrete grain interface.");
    }

    private static bool IsBaseGrainType(string grainType)
    {
        return string.Equals(grainType, nameof(Grain), StringComparison.Ordinal) ||
               string.Equals(grainType, typeof(Grain).FullName, StringComparison.Ordinal);
    }

    private static bool IsValidGrainIdentity(string grainType)
    {
        return !string.IsNullOrWhiteSpace(grainType) && !IsBaseGrainType(grainType);
    }
}
