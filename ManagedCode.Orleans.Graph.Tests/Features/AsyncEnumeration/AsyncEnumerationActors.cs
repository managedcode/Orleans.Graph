using System.Runtime.CompilerServices;
using ManagedCode.Orleans.Graph.Extensions;
using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

public sealed partial class AsyncEnumerationSourceGrain : Grain, IAsyncEnumerationSource, IAsyncEnumerationCatalogProbe
{
    public IAsyncEnumerable<StreamItem> AllowedAsync(string streamKey) => EnumerateAsync(streamKey, false);

    public IAsyncEnumerable<StreamItem> DeniedAsync(string streamKey) => EnumerateAsync(streamKey, false);

    public IAsyncEnumerable<StreamItem> CancellableAsync(string streamKey, CancellationToken cancellationToken) =>
        EnumerateAsync(streamKey, true, cancellationToken);

    public Task<StreamCounts> GetCountsAsync() => Task.FromResult(AsyncEnumerationExecutionCounters.Read(this.GetPrimaryKeyString()));

    private async IAsyncEnumerable<StreamItem> EnumerateAsync(
        string streamKey,
        bool waitForCancellation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AsyncEnumerationExecutionCounters.Started(streamKey);
        try
        {
            yield return CreateItem(streamKey, 0, null);
            await Task.Yield();
            if (waitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            AsyncEnumerationExecutionCounters.LeafAttempted(streamKey);
            var probe = await GrainFactory.GetGrain<IAsyncEnumerationLeaf>(streamKey).ObserveAsync(streamKey);
            yield return CreateItem(streamKey, 1, probe);

            await Task.Yield();
            AsyncEnumerationExecutionCounters.LeafAttempted(streamKey);
            var laterProbe = await GrainFactory.GetGrain<IAsyncEnumerationLeaf>(streamKey).ObserveAsync(streamKey);
            yield return CreateItem(streamKey, 2, laterProbe);
        }
        finally
        {
            AsyncEnumerationExecutionCounters.Settled(streamKey);
        }
    }

    private static StreamItem CreateItem(string streamKey, int sequence, StreamProbe? probe)
    {
        if (RequestContextHelper.CaptureCurrentCaller() is not CurrentCallerContext caller ||
            RequestContext.Get(Constants.RequestContextKey) is not CallHistory history)
        {
            throw new InvalidOperationException("Native stream execution lost its Graph request context.");
        }

        var sameBranch = AsyncEnumerationExecutionCounters.RecordHistory(streamKey, history);
        return new StreamItem(streamKey, sequence, caller.Caller, caller.Method, history.History.Count, sameBranch, probe);
    }
}

public sealed class AsyncEnumerationLeafGrain : Grain, IAsyncEnumerationLeaf
{
    public Task<StreamProbe> ObserveAsync(string streamKey)
    {
        AsyncEnumerationExecutionCounters.LeafEntered(streamKey);
        if (RequestContextHelper.CaptureCurrentCaller() is not CurrentCallerContext caller ||
            RequestContext.Get(Constants.RequestContextKey) is not CallHistory history ||
            history.History.Peek() is not InCall incoming)
        {
            throw new InvalidOperationException("Native leaf invocation lost its Graph transition context.");
        }

        var edge = GrainTransitionManager.GetLatestObservedCall(history)
            ?? throw new InvalidOperationException("Native leaf invocation has no observed Graph edge.");
        return Task.FromResult(new StreamProbe(
            streamKey,
            caller.Caller,
            caller.Method,
            edge.Source,
            edge.Target,
            edge.SourceMethod,
            edge.TargetMethod,
            incoming.SourceId?.ToString() ?? string.Empty,
            incoming.TargetId?.ToString() ?? string.Empty));
    }
}

public sealed class AsyncEnumerationOriginGrain : Grain, IAsyncEnumerationOrigin
{
    public Task<OriginResult> ReadAsync(string streamKey, CancellationToken cancellationToken) =>
        ReadStreamAsync(streamKey, false, cancellationToken);

    public Task<OriginResult> ReadDeniedAsync(string streamKey, CancellationToken cancellationToken) =>
        ReadStreamAsync(streamKey, true, cancellationToken);

    private async Task<OriginResult> ReadStreamAsync(string streamKey, bool denied, CancellationToken cancellationToken)
    {
        var priorCaller = RequestContextHelper.CaptureCurrentCaller();
        var priorHistory = RequestContext.Get(Constants.RequestContextKey);
        StreamProbe? probe = null;
        var source = GrainFactory.GetGrain<IAsyncEnumerationSource>(streamKey);
        var stream = SetNativeBatchSize(denied ? source.DeniedAsync(streamKey) : source.AllowedAsync(streamKey));
        try
        {
            await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
            while (await enumerator.MoveNextAsync())
            {
                probe = enumerator.Current.Probe ?? probe;
            }
        }
        finally
        {
            AsyncEnumerationExecutionCounters.RecordOriginRestoration(
                streamKey,
                Equals(priorCaller, RequestContextHelper.CaptureCurrentCaller()),
                ReferenceEquals(priorHistory, RequestContext.Get(Constants.RequestContextKey)));
        }

        if (RequestContextHelper.CaptureCurrentCaller() is not CurrentCallerContext caller || probe is null)
        {
            throw new InvalidOperationException("Silo-origin native enumeration did not restore its caller context.");
        }

        return CreateOriginResult(caller, probe, priorCaller, priorHistory, cancellationObserved: false);
    }

    public async Task<OriginResult> ReadPartialAsync(string streamKey, CancellationToken cancellationToken)
    {
        var priorCaller = RequestContextHelper.CaptureCurrentCaller();
        var priorHistory = RequestContext.Get(Constants.RequestContextKey);
        var stream = SetNativeBatchSize(GrainFactory.GetGrain<IAsyncEnumerationSource>(streamKey).AllowedAsync(streamKey));
        StreamItem? first = null;
        var disposalTimestamp = 0L;
        await using (var enumerator = stream.GetAsyncEnumerator(cancellationToken))
        {
            if (await enumerator.MoveNextAsync())
            {
                first = enumerator.Current;
            }

            disposalTimestamp = TimeProvider.System.GetTimestamp();
        }

        var producerSettlement = await WaitForProducerSettlementAsync(streamKey, disposalTimestamp, cancellationToken);
        if (first is null || RequestContextHelper.CaptureCurrentCaller() is not CurrentCallerContext caller)
        {
            throw new InvalidOperationException("Native partial enumeration did not restore its caller context.");
        }

        return CreateOriginResult(
            caller,
            null,
            priorCaller,
            priorHistory,
            cancellationObserved: false,
            producerSettlementTicks: producerSettlement.Ticks);
    }

    public async Task<OriginResult> ReadCancelledAsync(string streamKey, CancellationToken operationCancellation)
    {
        var priorCaller = RequestContextHelper.CaptureCurrentCaller();
        var priorHistory = RequestContext.Get(Constants.RequestContextKey);
        using var cancellation = new CancellationTokenSource();
        var stream = SetNativeBatchSize(
            GrainFactory.GetGrain<IAsyncEnumerationSource>(streamKey).CancellableAsync(streamKey, cancellation.Token));
        var cancellationObserved = false;
        var cancelTimestamp = 0L;
        await using (var enumerator = stream.GetAsyncEnumerator(operationCancellation))
        {
            if (!await enumerator.MoveNextAsync())
            {
                throw new InvalidOperationException("Native cancellation stream did not yield its first chunk.");
            }

            cancelTimestamp = TimeProvider.System.GetTimestamp();
            await cancellation.CancelAsync();
            try
            {
                await enumerator.MoveNextAsync();
            }
            catch (OperationCanceledException)
            {
                cancellationObserved = true;
            }
        }

        var cancelToSettlement = await WaitForProducerSettlementAsync(streamKey, cancelTimestamp, operationCancellation);
        if (!cancellationObserved || RequestContextHelper.CaptureCurrentCaller() is not CurrentCallerContext caller)
        {
            throw new InvalidOperationException("Native cancellation did not settle and restore its caller context.");
        }

        return CreateOriginResult(
            caller,
            null,
            priorCaller,
            priorHistory,
            cancellationObserved,
            !operationCancellation.IsCancellationRequested,
            cancellation.IsCancellationRequested,
            cancelToSettlement.Ticks);
    }

    private static async Task<TimeSpan> WaitForProducerSettlementAsync(
        string streamKey,
        long startTimestamp,
        CancellationToken operationDeadline)
    {
        var maximumElapsed = TimeSpan.FromSeconds(10);
        while (AsyncEnumerationExecutionCounters.Read(streamKey).Settled != 1)
        {
            var elapsed = TimeProvider.System.GetElapsedTime(startTimestamp);
            var remaining = maximumElapsed - elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("Native producer did not settle within ten seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(10, remaining.TotalMilliseconds)), TimeProvider.System, operationDeadline);
        }

        var totalElapsed = TimeProvider.System.GetElapsedTime(startTimestamp);
        if (totalElapsed > maximumElapsed)
        {
            throw new TimeoutException("Native producer did not settle within ten seconds.");
        }

        return totalElapsed;
    }

    private static IAsyncEnumerable<StreamItem> SetNativeBatchSize(IAsyncEnumerable<StreamItem> stream)
    {
        var nativeRequest = (IAsyncEnumerableRequest<StreamItem>)stream;
        nativeRequest.MaxBatchSize = 1;
        if (nativeRequest.MaxBatchSize != 1)
        {
            throw new InvalidOperationException("Native stream batch size was not set to one.");
        }

        return stream;
    }

    public async Task ReadDeniedLeafAsync(string streamKey, CancellationToken cancellationToken)
    {
        await ReadStreamAsync(streamKey, true, cancellationToken);
    }

    private static OriginResult CreateOriginResult(
        CurrentCallerContext caller,
        StreamProbe? probe,
        object? priorCaller,
        object? priorHistory,
        bool cancellationObserved,
        bool operationDeadlineRemainedActive = false,
        bool sourceCancellationWasRequested = false,
        long producerSettlementTicks = 0) =>
        new(
            caller.Caller,
            caller.Method,
            probe,
            Equals(priorCaller, caller),
            ReferenceEquals(priorHistory, RequestContext.Get(Constants.RequestContextKey)),
            cancellationObserved,
            operationDeadlineRemainedActive,
            sourceCancellationWasRequested,
            producerSettlementTicks);
}

public sealed class UnallowedAsyncEnumerationSourceGrain : Grain, IUnallowedAsyncEnumerationSource
{
    public async IAsyncEnumerable<StreamItem> StreamAsync(string streamKey)
    {
        AsyncEnumerationExecutionCounters.Started(streamKey);
        try
        {
            yield return new StreamItem(streamKey, 0, string.Empty, string.Empty, 0, false, null);
            await Task.Yield();
        }
        finally
        {
            AsyncEnumerationExecutionCounters.Settled(streamKey);
        }
    }
}
