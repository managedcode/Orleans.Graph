using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Features.AsyncEnumeration;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Orleans.CodeGeneration;
using Orleans.Serialization.Invocation;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphIncomingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig) : IIncomingGrainCallFilter
{
    private readonly GrainTransitionManager? _graphManager = serviceProvider.GetService<GrainTransitionManager>();
    private readonly IGrainFactory? _grainFactory = serviceProvider.GetService<IGrainFactory>();
    private readonly AsyncEnumerationFactoryCatalog? _enumerationCatalog = serviceProvider.GetService<AsyncEnumerationFactoryCatalog>();

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var currentCaller = RequestContextHelper.CaptureCurrentCaller();
        AsyncEnumerationRequestAdapter.TryGetOriginal(context.Request, out var originalRequest);
        var tracked = context.TrackIncomingCall(graphCallFilterConfig, originalRequest);
        var previousHistory = context.Request is IRequest request && (request.Options & InvokeMethodOptions.OneWay) != 0
            ? RequestContext.Get(Constants.RequestContextKey) as CallHistory
            : null;

        try
        {
            if (tracked)
            {
                var callHistory = context.GetCallHistory();

                var isTelemetryCall = RequestContextHelper.IsOrleansGraphTelemetryRequest(originalRequest ?? context.Request);
                if (!isTelemetryCall)
                {
                    _graphManager?.IsLatestTransitionAllowed(callHistory, true);
                }

                await ReportObservedEdgeAsync(context, callHistory, isTelemetryCall);
            }

            if (originalRequest is not null && tracked)
            {
                WrapEnumerationRequest(context, originalRequest);
            }

            if (previousHistory is not null)
            {
                RequestContext.Set(Constants.RequestContextKey, previousHistory.Fork(detach: true));
            }

            await context.Invoke();
        }
        finally
        {
            if (previousHistory is not null)
            {
                RequestContext.Set(Constants.RequestContextKey, previousHistory);
            }

            if (tracked)
            {
                RequestContextHelper.RestoreCurrentCaller(currentCaller);
            }
        }
    }

    private void WrapEnumerationRequest(IIncomingGrainCallContext context, IInvokable originalRequest)
    {
        if (_enumerationCatalog is null)
        {
            throw new InvalidOperationException("Asynchronous enumeration Graph catalog is not registered.");
        }

        var scope = AsyncEnumerationScope.Capture();
        context.Request.SetArgument(1, _enumerationCatalog.Wrap(originalRequest, scope));
    }

    private async Task ReportObservedEdgeAsync(IIncomingGrainCallContext context, CallHistory callHistory, bool isTelemetryCall)
    {
        if (!graphCallFilterConfig.EnableLiveGraphTelemetry)
        {
            return;
        }

        var observedCall = GrainTransitionManager.GetLatestObservedCall(callHistory);
        if (observedCall is null)
        {
            return;
        }

        if (isTelemetryCall && context.Grain is IObservedGrainCallSink sink)
        {
            sink.RecordObservedCall(observedCall);
            return;
        }

        if (RequestContextHelper.IsTelemetrySuppressed())
        {
            return;
        }

        if (_grainFactory is null)
        {
            return;
        }

        // The async method scopes RequestContext changes to this telemetry call.
        if (!graphCallFilterConfig.TrackOrleansGraphInternalCalls)
        {
            RequestContext.Remove(Constants.RequestContextKey);
        }

        RequestContext.Set(Constants.TelemetrySuppressionContextKey, true);
        await _grainFactory
            .GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey)
            .RecordObservedCallAsync(observedCall);
    }
}
