using ManagedCode.Orleans.Graph.Models;

namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

[Alias("MC.Graph.Tests.AsyncEnumeration.StreamItem")]
[GenerateSerializer]
public sealed record StreamItem(
    [property: Id(0)] string StreamKey,
    [property: Id(1)] int Sequence,
    [property: Id(2)] string Caller,
    [property: Id(3)] string CallerMethod,
    [property: Id(4)] int HistoryDepth,
    [property: Id(5)] bool SameHistoryBranch,
    [property: Id(6)] StreamProbe? Probe);

[Alias("MC.Graph.Tests.AsyncEnumeration.StreamProbe")]
[GenerateSerializer]
public sealed record StreamProbe(
    [property: Id(0)] string StreamKey,
    [property: Id(1)] string Caller,
    [property: Id(2)] string CallerMethod,
    [property: Id(3)] string EdgeSource,
    [property: Id(4)] string EdgeTarget,
    [property: Id(5)] string EdgeSourceMethod,
    [property: Id(6)] string EdgeTargetMethod,
    [property: Id(7)] string NativeSourceId,
    [property: Id(8)] string NativeTargetId);

[Alias("MC.Graph.Tests.AsyncEnumeration.OriginResult")]
[GenerateSerializer]
public sealed record OriginResult(
    [property: Id(0)] string Caller,
    [property: Id(1)] string CallerMethod,
    [property: Id(2)] StreamProbe? Probe,
    [property: Id(3)] bool CallerRestored,
    [property: Id(4)] bool HistoryRestored,
    [property: Id(5)] bool CancellationObserved,
    [property: Id(6)] bool OperationDeadlineRemainedActive,
    [property: Id(7)] bool SourceCancellationWasRequested,
    [property: Id(8)] long ProducerSettlementTicks);

[Alias("MC.Graph.Tests.AsyncEnumeration.CatalogDelegationResult")]
[GenerateSerializer]
public sealed record CatalogDelegationResult(
    [property: Id(0)] string InterfaceName,
    [property: Id(1)] string MethodName,
    [property: Id(2)] string Argument,
    [property: Id(3)] int BatchSize,
    [property: Id(4)] bool NativeMetadataDelegated,
    [property: Id(5)] bool ArgumentMutationDelegated,
    [property: Id(6)] bool BatchMutationDelegated,
    [property: Id(7)] bool OptionsMutationDelegated,
    [property: Id(8)] bool CancellationMutationDelegated,
    [property: Id(9)] bool TargetMutationDelegated,
    [property: Id(10)] bool InvokeFailureDelegated,
    [property: Id(11)] bool UnknownMethodRejected,
    [property: Id(12)] bool OpenGenericUnregistered);

[Alias("MC.Graph.Tests.AsyncEnumeration.StreamCounts")]
[GenerateSerializer]
public sealed record StreamCounts(
    [property: Id(0)] int Started,
    [property: Id(1)] int Settled);

public interface IAsyncEnumerationSource : IGrainWithStringKey
{
    IAsyncEnumerable<StreamItem> AllowedAsync(string streamKey);

    IAsyncEnumerable<StreamItem> DeniedAsync(string streamKey);

    IAsyncEnumerable<StreamItem> CancellableAsync(string streamKey, CancellationToken cancellationToken);

    Task<StreamCounts> GetCountsAsync();
}

public interface IAsyncEnumerationLeaf : IGrainWithStringKey
{
    Task<StreamProbe> ObserveAsync(string streamKey);
}

public interface IAsyncEnumerationOrigin : IGrainWithStringKey
{
    Task<OriginResult> ReadAsync(string streamKey, CancellationToken cancellationToken);

    Task<OriginResult> ReadDeniedAsync(string streamKey, CancellationToken cancellationToken);

    Task<OriginResult> ReadPartialAsync(string streamKey, CancellationToken cancellationToken);

    Task<OriginResult> ReadCancelledAsync(string streamKey, CancellationToken cancellationToken);

    Task ReadDeniedLeafAsync(string streamKey, CancellationToken cancellationToken);
}

public interface IUnallowedAsyncEnumerationSource : IGrainWithStringKey
{
    IAsyncEnumerable<StreamItem> StreamAsync(string streamKey);
}

public interface IAsyncEnumerationCatalogProbe : IGrainWithStringKey
{
    Task<CatalogDelegationResult> InspectAsync(string streamKey, CancellationToken cancellationToken);
}

internal interface IOpenGenericAsyncEnumeration
{
    IAsyncEnumerable<StreamItem> OpenAsync<TValue>(TValue value);
}

public static class AsyncEnumerationExecutionCounters
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Counter> _counters = new(StringComparer.Ordinal);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (bool Caller, bool History)> _originRestorations = new(StringComparer.Ordinal);

    public static void Started(string streamKey) => _counters.GetOrAdd(streamKey, static _ => new Counter()).AddStarted();

    public static void Settled(string streamKey) => _counters.GetOrAdd(streamKey, static _ => new Counter()).AddSettled();

    public static void LeafEntered(string streamKey) => _counters.GetOrAdd(streamKey, static _ => new Counter()).AddLeafEntered();

    public static void LeafAttempted(string streamKey) => _counters.GetOrAdd(streamKey, static _ => new Counter()).AddLeafAttempted();

    public static StreamCounts Read(string streamKey) =>
        _counters.TryGetValue(streamKey, out var counter) ? counter.Read() : new StreamCounts(0, 0);

    public static int LeafCallCount(string streamKey) => _counters.TryGetValue(streamKey, out var counter) ? counter.LeafCallCount : 0;

    public static int LeafAttemptCount(string streamKey) => _counters.TryGetValue(streamKey, out var counter) ? counter.LeafAttemptCount : 0;

    public static bool RecordHistory(string streamKey, CallHistory history) =>
        ReferenceEquals(_histories.GetOrAdd(streamKey, history), history);

    public static CallHistory? ReadHistory(string streamKey) => _histories.GetValueOrDefault(streamKey);

    public static void RecordOriginRestoration(string streamKey, bool caller, bool history) =>
        _originRestorations[streamKey] = (caller, history);

    public static (bool Caller, bool History)? ReadOriginRestoration(string streamKey) =>
        _originRestorations.TryGetValue(streamKey, out var value) ? value : null;

    public static void Remove(string streamKey)
    {
        _counters.TryRemove(streamKey, out _);
        _histories.TryRemove(streamKey, out _);
        _originRestorations.TryRemove(streamKey, out _);
    }

    private sealed class Counter
    {
        private int _started;
        private int _settled;
        private int _leafCalls;
        private int _leafAttempts;

        public void AddStarted() => Interlocked.Increment(ref _started);

        public void AddSettled() => Interlocked.Increment(ref _settled);

        public void AddLeafEntered() => Interlocked.Increment(ref _leafCalls);

        public void AddLeafAttempted() => Interlocked.Increment(ref _leafAttempts);

        public int LeafCallCount => Volatile.Read(ref _leafCalls);

        public int LeafAttemptCount => Volatile.Read(ref _leafAttempts);

        public StreamCounts Read() => new(Volatile.Read(ref _started), Volatile.Read(ref _settled));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CallHistory> _histories = new(StringComparer.Ordinal);
}
