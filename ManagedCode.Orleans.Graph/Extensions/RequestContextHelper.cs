using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class RequestContextHelper
{
    public static bool TrackIncomingCall(this IIncomingGrainCallContext context)
    {
        EnsureValidGrainIdentity(context.InterfaceName, context.MethodName);

        var call = GetOrCreateCallHistory(out var created);
        call.Push(new InCall(context.SourceId, context.TargetId, context.InterfaceName, context.MethodName));
        if (created)
        {
            context.SetCallHistory(call);
        }

        SetCurrentCaller(context.InterfaceName, context.MethodName);
        return true;
    }

    public static bool TrackOutgoingCall(this IOutgoingGrainCallContext context)
    {
        EnsureValidGrainIdentity(context.InterfaceName, context.MethodName);

        var callerContext = ResolveCaller(context);

        var call = GetOrCreateCallHistory(out var created);
        call.Push(new OutCall(
            context.SourceId,
            context.TargetId,
            callerContext.Caller,
            context.InterfaceName,
            context.MethodName,
            callerContext.Method));
        if (created)
        {
            context.SetCallHistory(call);
        }

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
        if (RequestContext.Get(Constants.RequestContextKey) is CallHistory callHistory)
        {
            return callHistory;
        }

        throw new InvalidOperationException("Unable to resolve Orleans graph call history from request context.");
    }

    public static bool IsCallHistoryExist(this IGrainCallContext context)
    {
        return RequestContext.Get(Constants.RequestContextKey) is CallHistory;
    }

    public static void SetCallHistory(this IGrainCallContext context, CallHistory callHistory)
    {
        RequestContext.Set(Constants.RequestContextKey, callHistory);
    }

    private static CallHistory GetOrCreateCallHistory(out bool created)
    {
        var callHistory = RequestContext.Get(Constants.RequestContextKey) as CallHistory;
        if (callHistory is not null)
        {
            created = false;
            return callHistory;
        }

        created = true;
        return new CallHistory();
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

    public static object? CaptureCurrentCaller()
    {
        return RequestContext.Get(Constants.CurrentCallerContextKey);
    }

    public static void RestoreCurrentCaller(object? currentCaller)
    {
        RestoreRequestContextValue(Constants.CurrentCallerContextKey, currentCaller);
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

    public static async Task RunWithCurrentCallerAsync(string caller, string method, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureValidGrainIdentity(caller, method);
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new InvalidOperationException($"Unable to set current caller {caller} without a method.");
        }

        var currentCaller = CaptureCurrentCaller();
        SetCurrentCaller(caller, method);

        try
        {
            await action();
        }
        finally
        {
            RestoreCurrentCaller(currentCaller);
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

    private static CurrentCallerContext ResolveCaller(IOutgoingGrainCallContext context)
    {
        if (!context.SourceId.HasValue)
        {
            return new CurrentCallerContext(Constants.ClientCallerId, Constants.AnyMethod);
        }

        if (RequestContext.Get(Constants.CurrentCallerContextKey) is CurrentCallerContext callerContext &&
            IsValidGrainIdentity(callerContext.Caller) &&
            !string.IsNullOrWhiteSpace(callerContext.Method))
        {
            if (IsActivationCallbackCaller(callerContext.Caller))
            {
                var callbackCaller = ResolveSourceContextCaller(context, callerContext.Method);
                if (callbackCaller.HasValue)
                {
                    return callbackCaller.Value;
                }
            }

            return callerContext;
        }

        var sourceCaller = ResolveSourceContextCaller(context, Constants.AnyMethod);
        if (sourceCaller.HasValue)
        {
            return sourceCaller.Value;
        }

        return new CurrentCallerContext(Constants.UnknownCallerId, Constants.AnyMethod);
    }

    private static CurrentCallerContext? ResolveSourceContextCaller(IOutgoingGrainCallContext context, string method)
    {
        var sourceContext = context.SourceContext;
        if (sourceContext is null)
        {
            return null;
        }

        var sourceImplementation = sourceContext.GrainInstance?.GetType().FullName;
        if (sourceImplementation is not null && IsValidGrainIdentity(sourceImplementation))
        {
            return new CurrentCallerContext(sourceImplementation, method);
        }

        var sourceInterface = sourceContext.GrainReference.InterfaceName;
        if (IsValidGrainIdentity(sourceInterface))
        {
            return new CurrentCallerContext(sourceInterface, method);
        }

        return null;
    }

    private static bool IsActivationCallbackCaller(string caller)
    {
        return string.Equals(caller, typeof(IRemindable).FullName, StringComparison.Ordinal);
    }

    private static void SetCurrentCaller(string caller, string method)
    {
        RequestContext.Set(Constants.CurrentCallerContextKey, new CurrentCallerContext(caller, method));
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
            $"Resolved Orleans graph identity for {grainType}.{method} is not a concrete grain interface or implementation type.");
    }

    private static bool IsBaseGrainType(string? grainType)
    {
        return string.Equals(grainType, nameof(Grain), StringComparison.Ordinal) ||
               string.Equals(grainType, typeof(Grain).FullName, StringComparison.Ordinal);
    }

    private static bool IsValidGrainIdentity(string? grainType)
    {
        return !string.IsNullOrWhiteSpace(grainType) && !IsBaseGrainType(grainType);
    }

}
