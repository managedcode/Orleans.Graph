namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

[ClassDataSource<AsyncEnumerationTestCluster>(Shared = SharedType.PerTestSession)]
public sealed class NativeAsyncEnumerationContextTests(AsyncEnumerationTestCluster fixture)
{
    private static readonly TimeSpan _testOperationDeadline = TimeSpan.FromSeconds(10);

    [Test]
    public async Task SiloOriginCallAndConcurrentStreamsKeepIndependentBranchesAsync()
    {
        var originKey = NewKey();
        var streamKey = NewKey();
        var origin = fixture.Cluster.Client.GetGrain<IAsyncEnumerationOrigin>(originKey);
        using var readDeadline = CreateDeadline();
        var result = await origin.ReadAsync(streamKey, readDeadline.Token);

        result.Caller.ShouldBe(typeof(IAsyncEnumerationOrigin).FullName);
        result.CallerMethod.ShouldBe(nameof(IAsyncEnumerationOrigin.ReadAsync));
        result.CallerRestored.ShouldBeTrue();
        result.HistoryRestored.ShouldBeTrue();
        AssertAllowedLeaf(result.Probe!, streamKey, nameof(IAsyncEnumerationSource.AllowedAsync));
        AsyncEnumerationExecutionCounters.LeafAttemptCount(streamKey).ShouldBe(2);
        AsyncEnumerationExecutionCounters.LeafCallCount(streamKey).ShouldBe(2);
        AsyncEnumerationExecutionCounters.Remove(streamKey);

        var deniedStreamKey = NewKey();
        using var deniedDeadline = CreateDeadline();
        var failure = await Should.ThrowAsync<InvalidOperationException>(
            () => origin.ReadDeniedAsync(deniedStreamKey, deniedDeadline.Token));
        failure.Message.ShouldContain(typeof(IAsyncEnumerationOrigin).FullName!);
        AsyncEnumerationExecutionCounters.Read(deniedStreamKey).ShouldBe(new StreamCounts(0, 0));
        AssertOriginRestored(deniedStreamKey);
        AsyncEnumerationExecutionCounters.Remove(deniedStreamKey);

        await VerifyNestedErrorRestorationAsync(NewKey());
        await VerifyPartialDisposalRestorationAsync(NewKey());
        await VerifyCancellationRestorationAsync(NewKey());

        await DrainConcurrentAsync(NewKey(), NewKey());
    }

    private async Task VerifyNestedErrorRestorationAsync(string streamKey)
    {
        var origin = fixture.Cluster.Client.GetGrain<IAsyncEnumerationOrigin>(NewKey());
        using var deadline = CreateDeadline();
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => origin.ReadDeniedLeafAsync(streamKey, deadline.Token));

        failure.Message.ShouldContain(typeof(IAsyncEnumerationLeaf).FullName!);
        AsyncEnumerationExecutionCounters.Read(streamKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.LeafAttemptCount(streamKey).ShouldBe(1);
        AsyncEnumerationExecutionCounters.LeafCallCount(streamKey).ShouldBe(0);
        AssertOriginRestored(streamKey);
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    private async Task VerifyPartialDisposalRestorationAsync(string streamKey)
    {
        using var deadline = CreateDeadline();
        var result = await fixture.Cluster.Client.GetGrain<IAsyncEnumerationOrigin>(NewKey())
            .ReadPartialAsync(streamKey, deadline.Token);

        result.Caller.ShouldBe(typeof(IAsyncEnumerationOrigin).FullName);
        result.CallerMethod.ShouldBe(nameof(IAsyncEnumerationOrigin.ReadPartialAsync));
        result.Probe.ShouldBeNull();
        result.CallerRestored.ShouldBeTrue();
        result.HistoryRestored.ShouldBeTrue();
        TimeSpan.FromTicks(result.ProducerSettlementTicks).ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(10));
        AsyncEnumerationExecutionCounters.Read(streamKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    private async Task VerifyCancellationRestorationAsync(string streamKey)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await fixture.Cluster.Client.GetGrain<IAsyncEnumerationOrigin>(NewKey())
            .ReadCancelledAsync(streamKey, deadline.Token);

        result.Caller.ShouldBe(typeof(IAsyncEnumerationOrigin).FullName);
        result.CallerMethod.ShouldBe(nameof(IAsyncEnumerationOrigin.ReadCancelledAsync));
        result.CallerRestored.ShouldBeTrue();
        result.HistoryRestored.ShouldBeTrue();
        result.CancellationObserved.ShouldBeTrue();
        result.OperationDeadlineRemainedActive.ShouldBeTrue();
        result.SourceCancellationWasRequested.ShouldBeTrue();
        TimeSpan.FromTicks(result.ProducerSettlementTicks).ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(10));
        AsyncEnumerationExecutionCounters.Read(streamKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    private async Task DrainConcurrentAsync(string firstKey, string secondKey)
    {
        var firstStream = fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(firstKey).AllowedAsync(firstKey);
        var secondStream = fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(secondKey).AllowedAsync(secondKey);
        SetNativeBatchSize(firstStream, 1);
        SetNativeBatchSize(secondStream, 1);
        using var firstDeadline = CreateDeadline();
        using var secondDeadline = CreateDeadline();
        var disposalTimestamp = 0L;
        await using (var first = firstStream.GetAsyncEnumerator(firstDeadline.Token))
        await using (var second = secondStream.GetAsyncEnumerator(secondDeadline.Token))
        {
            (await first.MoveNextAsync()).ShouldBeTrue();
            (await second.MoveNextAsync()).ShouldBeTrue();
            var pulls = await Task.WhenAll(first.MoveNextAsync().AsTask(), second.MoveNextAsync().AsTask());

            pulls.ShouldAllBe(static pull => pull);
            first.Current.StreamKey.ShouldBe(firstKey);
            second.Current.StreamKey.ShouldBe(secondKey);
            ReferenceEquals(
                AsyncEnumerationExecutionCounters.ReadHistory(firstKey),
                AsyncEnumerationExecutionCounters.ReadHistory(secondKey)).ShouldBeFalse();
            first.Current.HistoryDepth.ShouldBe(second.Current.HistoryDepth);
            AssertAllowedLeaf(first.Current.Probe!, firstKey, nameof(IAsyncEnumerationSource.AllowedAsync));
            AssertAllowedLeaf(second.Current.Probe!, secondKey, nameof(IAsyncEnumerationSource.AllowedAsync));
            first.Current.SameHistoryBranch.ShouldBeTrue();
            second.Current.SameHistoryBranch.ShouldBeTrue();
            disposalTimestamp = TimeProvider.System.GetTimestamp();
        }

        var firstSettlement = await WaitForSettlementAsync(firstKey, disposalTimestamp, firstDeadline.Token);
        var secondSettlement = await WaitForSettlementAsync(secondKey, disposalTimestamp, secondDeadline.Token);
        firstSettlement.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(10));
        secondSettlement.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(10));
        AsyncEnumerationExecutionCounters.Read(firstKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.Read(secondKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.Remove(firstKey);
        AsyncEnumerationExecutionCounters.Remove(secondKey);
    }

    private static async Task<TimeSpan> WaitForSettlementAsync(string streamKey, long startTimestamp, CancellationToken deadline)
    {
        var maximumElapsed = TimeSpan.FromSeconds(10);
        while (AsyncEnumerationExecutionCounters.Read(streamKey).Settled != 1)
        {
            var remaining = maximumElapsed - TimeProvider.System.GetElapsedTime(startTimestamp);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("Concurrent native stream did not settle within ten seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(10, remaining.TotalMilliseconds)), TimeProvider.System, deadline);
        }

        return TimeProvider.System.GetElapsedTime(startTimestamp);
    }

    private static void AssertOriginRestored(string streamKey)
    {
        var restoration = AsyncEnumerationExecutionCounters.ReadOriginRestoration(streamKey);
        if (!restoration.HasValue)
        {
            throw new InvalidOperationException("Silo-origin stream did not record its restored ambient context.");
        }

        restoration.Value.Caller.ShouldBeTrue();
        restoration.Value.History.ShouldBeTrue();
    }

    private static void SetNativeBatchSize(IAsyncEnumerable<StreamItem> stream, int batchSize)
    {
        var nativeRequest = (IAsyncEnumerableRequest<StreamItem>)stream;
        nativeRequest.MaxBatchSize = batchSize;
        nativeRequest.MaxBatchSize.ShouldBe(batchSize);
    }

    private void AssertAllowedLeaf(StreamProbe probe, string streamKey, string sourceMethod)
    {
        probe.StreamKey.ShouldBe(streamKey);
        probe.Caller.ShouldBe(typeof(IAsyncEnumerationLeaf).FullName);
        probe.CallerMethod.ShouldBe(nameof(IAsyncEnumerationLeaf.ObserveAsync));
        probe.EdgeSource.ShouldBe(typeof(IAsyncEnumerationSource).FullName);
        probe.EdgeTarget.ShouldBe(typeof(IAsyncEnumerationLeaf).FullName);
        probe.EdgeSourceMethod.ShouldBe(sourceMethod);
        probe.EdgeTargetMethod.ShouldBe(nameof(IAsyncEnumerationLeaf.ObserveAsync));
        probe.NativeSourceId.ShouldBe(fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(streamKey).GetGrainId().ToString());
        probe.NativeTargetId.ShouldBe(fixture.Cluster.Client.GetGrain<IAsyncEnumerationLeaf>(streamKey).GetGrainId().ToString());
    }

    private static string NewKey() => Guid.NewGuid().ToString("N");

    private static CancellationTokenSource CreateDeadline() => new(_testOperationDeadline);
}
