using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Features.AsyncEnumeration;

internal sealed class AsyncEnumerationScope(CurrentCallerContext caller, CallHistory history)
{
    public static AsyncEnumerationScope Capture()
    {
        if (RequestContextHelper.CaptureCurrentCaller() is not CurrentCallerContext caller ||
            RequestContext.Get(Constants.RequestContextKey) is not CallHistory history)
        {
            throw new InvalidOperationException("Validated asynchronous enumeration has no Graph caller context.");
        }

        return new AsyncEnumerationScope(caller, history.Fork());
    }

    public IDisposable Enter()
    {
        var previousCaller = RequestContextHelper.CaptureCurrentCaller();
        var previousHistory = RequestContext.Get(Constants.RequestContextKey);
        RequestContext.Set(Constants.CurrentCallerContextKey, caller);
        RequestContext.Set(Constants.RequestContextKey, history);
        return new RestoreRequestContext(previousCaller, previousHistory);
    }

    private sealed class RestoreRequestContext(object? previousCaller, object? previousHistory) : IDisposable
    {
        public void Dispose()
        {
            Restore(Constants.CurrentCallerContextKey, previousCaller);
            Restore(Constants.RequestContextKey, previousHistory);
        }

        private static void Restore(string key, object? value)
        {
            if (value is null)
            {
                RequestContext.Remove(key);
            }
            else
            {
                RequestContext.Set(key, value);
            }
        }
    }
}
