using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Features.AsyncEnumeration;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;
using ManagedCode.Orleans.Graph.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Orleans.CodeGeneration;
using Orleans.Serialization.Invocation;

namespace ManagedCode.Orleans.Graph.Filters;

public class GraphIncomingGrainCallFilter : IIncomingGrainCallFilter
{
    private readonly GraphCallFilterConfig _graphCallFilterConfig;
    private readonly GrainTransitionManager? _graphManager;
    private readonly IGrainFactory? _grainFactory;
    private readonly AsyncEnumerationFactoryCatalog? _enumerationCatalog;
    private readonly TimeProvider _timeProvider;
    private readonly ObservedCallBatcher? _batcher;

    public GraphIncomingGrainCallFilter(IServiceProvider serviceProvider, GraphCallFilterConfig graphCallFilterConfig)
    {
        _graphCallFilterConfig = graphCallFilterConfig;
        _graphManager = serviceProvider.GetService<GrainTransitionManager>();
        _grainFactory = serviceProvider.GetService<IGrainFactory>();
        _enumerationCatalog = serviceProvider.GetService<AsyncEnumerationFactoryCatalog>();
        _timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        _batcher = _grainFactory is { } grainFactory ? new ObservedCallBatcher(grainFactory) : null;
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var currentCaller = RequestContextHelper.CaptureCurrentCaller();
        AsyncEnumerationRequestAdapter.TryGetOriginal(context.Request, out var originalRequest);
        var tracked = context.TrackIncomingCall(_graphCallFilterConfig, originalRequest);
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
        if (!_graphCallFilterConfig.EnableLiveGraphTelemetry)
        {
            return;
        }

        var observedCall = GrainTransitionManager.GetLatestObservedCall(callHistory, _timeProvider);
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

        if (!_graphCallFilterConfig.TrackOrleansGraphInternalCalls)
        {
            await _batcher!.ReportAsync(observedCall);
            return;
        }

        // Internal tracking attributes the telemetry RPC to this exact caller.
        RequestContext.Set(Constants.TelemetrySuppressionContextKey, true);
        await _grainFactory
            .GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey)
            .RecordObservedCallAsync(observedCall);
    }
}
