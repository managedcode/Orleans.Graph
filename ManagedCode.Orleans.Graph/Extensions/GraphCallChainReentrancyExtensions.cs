using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Extensions;

public static class GraphCallChainReentrancyExtensions
{
    /// <summary>
    /// Runs an awaited operation in an exact actor-scoped native Orleans call-chain
    /// reentrancy section tracked by Graph. Child RPCs must be awaited within the
    /// operation; escaped background work is not a continuation of this scope.
    /// </summary>
    public static async Task<T> WithCallChainReentrancyAsync<T>(this IGrainBase owner, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(operation);

        var history = GetCurrentHistory(owner);
        using var nativeScope = RequestContext.AllowCallChainReentrancy();
        var branch = RegisterScope(history, owner.GrainContext.GrainId, RequestContext.ReentrancyId);
        RequestContext.Set(Constants.RequestContextKey, branch);
        try
        {
            return await operation();
        }
        finally
        {
            RequestContext.Set(Constants.RequestContextKey, history);
        }
    }

    /// <inheritdoc cref="WithCallChainReentrancyAsync{T}"/>
    public static async Task WithCallChainReentrancyAsync(this IGrainBase owner, Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(operation);

        var history = GetCurrentHistory(owner);
        using var nativeScope = RequestContext.AllowCallChainReentrancy();
        var branch = RegisterScope(history, owner.GrainContext.GrainId, RequestContext.ReentrancyId);
        RequestContext.Set(Constants.RequestContextKey, branch);
        try
        {
            await operation();
        }
        finally
        {
            RequestContext.Set(Constants.RequestContextKey, history);
        }
    }

    private static CallHistory GetCurrentHistory(IGrainBase owner)
    {
        var current = owner.GrainContext.ActivationServices.GetRequiredService<IGrainContextAccessor>().GrainContext;
        if (!ReferenceEquals(current, owner.GrainContext))
        {
            throw new InvalidOperationException("Call-chain reentrancy must be entered by the currently executing actor.");
        }

        return RequestContext.Get(Constants.RequestContextKey) as CallHistory
               ?? throw new InvalidOperationException("Graph call history is required for tracked call-chain reentrancy.");
    }

    private static CallHistory RegisterScope(CallHistory history, GrainId actorId, Guid reentrancyId)
    {
        var participants = new HashSet<GrainId> { actorId };
        foreach (var call in history.History)
        {
            if (call.SourceId is { } source)
            {
                participants.Add(source);
            }
            if (call.TargetId is { } target)
            {
                participants.Add(target);
            }
        }

        var registered = new HashSet<GrainId>();
        var scopes = new List<NativeCallChainScope>(Math.Min(history.NativeCallChainScopes.Length, participants.Count) + 1);
        foreach (var scope in history.NativeCallChainScopes)
        {
            if (scope.ReentrancyId == reentrancyId && participants.Contains(scope.ActorId) && registered.Add(scope.ActorId))
            {
                scopes.Add(scope);
            }
        }

        if (registered.Add(actorId))
        {
            scopes.Add(new NativeCallChainScope(actorId, reentrancyId));
        }

        var branch = history.Fork();
        branch.NativeCallChainScopes = [.. scopes];
        return branch;
    }
}
