using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Features.AsyncEnumeration;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphOutgoingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IOutgoingGrainCallFilter
{
    private readonly GrainTransitionManager? _graphManager = serviceProvider.GetService<GrainTransitionManager>();
    private readonly AsyncEnumerationFactoryCatalog? _enumerationCatalog = serviceProvider.GetService<AsyncEnumerationFactoryCatalog>();

    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        if (AsyncEnumerationRequestAdapter.TryGetOriginal(context.Request, out var originalRequest) &&
            (_enumerationCatalog is null || !_enumerationCatalog.Contains(originalRequest.GetMethod())))
        {
            throw new InvalidOperationException("Asynchronous enumeration method is not registered in the Graph catalog.");
        }

        var previous = RequestContext.Get(Constants.RequestContextKey);
        if (previous is CallHistory parentHistory)
        {
            RequestContext.Set(Constants.RequestContextKey, parentHistory.Fork());
        }

        try
        {
            if (context.TrackOutgoingCall(graphCallFilterConfig) &&
                !context.IsOrleansGraphTelemetryCall())
            {
                var callHistory = context.GetCallHistory();
                _graphManager?.DetectLatestDeadlock(callHistory, true);
                _graphManager?.IsLatestTransitionAllowed(callHistory, true);
            }

            await context.Invoke();
        }
        finally
        {
            if (previous is CallHistory)
            {
                RequestContext.Set(Constants.RequestContextKey, previous);
            }
        }
    }
}
