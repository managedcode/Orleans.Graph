namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

[ClassDataSource<AsyncEnumerationTestCluster>(Shared = SharedType.PerTestSession)]
public sealed class NativeAsyncEnumerationPolicyTests(AsyncEnumerationTestCluster fixture)
{
    private static readonly TimeSpan _testOperationDeadline = TimeSpan.FromSeconds(10);

    [Test]
    public async Task ClientStreamPreservesOriginalMethodAcrossLaterNativePullsAsync()
    {
        var streamKey = NewKey();
        var stream = fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(streamKey).AllowedAsync(streamKey);
        SetNativeBatchSize(stream, 1);
        using var deadline = CreateDeadline();
        await using var enumerator = stream.GetAsyncEnumerator(deadline.Token);

        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        AssertStreamItem(enumerator.Current, streamKey, 0, nameof(IAsyncEnumerationSource.AllowedAsync));
        var first = enumerator.Current;

        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        var later = enumerator.Current;
        AssertStreamItem(later, streamKey, 1, nameof(IAsyncEnumerationSource.AllowedAsync));
        first.SameHistoryBranch.ShouldBeTrue();
        later.SameHistoryBranch.ShouldBeTrue();
        later.HistoryDepth.ShouldBe(first.HistoryDepth);
        AssertAllowedLeaf(later.Probe!, streamKey, nameof(IAsyncEnumerationSource.AllowedAsync));

        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        var latest = enumerator.Current;
        AssertStreamItem(latest, streamKey, 2, nameof(IAsyncEnumerationSource.AllowedAsync));
        latest.SameHistoryBranch.ShouldBeTrue();
        latest.HistoryDepth.ShouldBe(first.HistoryDepth);
        AssertAllowedLeaf(latest.Probe!, streamKey, nameof(IAsyncEnumerationSource.AllowedAsync));

        (await enumerator.MoveNextAsync()).ShouldBeFalse();
        (await fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(streamKey).GetCountsAsync())
            .ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.LeafAttemptCount(streamKey).ShouldBe(2);
        AsyncEnumerationExecutionCounters.LeafCallCount(streamKey).ShouldBe(2);
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    [Test]
    public async Task MissingMethodSpecificTransitionFailsBeforeLeafEntryAsync()
    {
        var streamKey = NewKey();
        var source = fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(streamKey);
        var stream = source.DeniedAsync(streamKey);
        SetNativeBatchSize(stream, 1);
        using var deadline = CreateDeadline();
        await using var enumerator = stream.GetAsyncEnumerator(deadline.Token);

        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        AssertStreamItem(enumerator.Current, streamKey, 0, nameof(IAsyncEnumerationSource.DeniedAsync));
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => enumerator.MoveNextAsync().AsTask());

        failure.Message.ShouldContain(typeof(IAsyncEnumerationSource).FullName!);
        AsyncEnumerationExecutionCounters.LeafCallCount(streamKey).ShouldBe(0);
        (await source.GetCountsAsync()).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.LeafAttemptCount(streamKey).ShouldBe(1);
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    [Test]
    public async Task ClientAccessDenialDoesNotEnterUnallowedProducerAsync()
    {
        var streamKey = NewKey();
        var stream = fixture.Cluster.Client.GetGrain<IUnallowedAsyncEnumerationSource>(streamKey).StreamAsync(streamKey);
        SetNativeBatchSize(stream, 1);
        using var deadline = CreateDeadline();
        await using var enumerator = stream.GetAsyncEnumerator(deadline.Token);

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => enumerator.MoveNextAsync().AsTask());

        failure.Message.ShouldContain(typeof(IUnallowedAsyncEnumerationSource).FullName!);
        AsyncEnumerationExecutionCounters.Read(streamKey).ShouldBe(new StreamCounts(0, 0));
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    private static void SetNativeBatchSize(IAsyncEnumerable<StreamItem> stream, int batchSize)
    {
        var nativeRequest = (IAsyncEnumerableRequest<StreamItem>)stream;
        nativeRequest.MaxBatchSize = batchSize;
        nativeRequest.MaxBatchSize.ShouldBe(batchSize);
    }

    private static void AssertStreamItem(StreamItem item, string streamKey, int sequence, string method)
    {
        item.StreamKey.ShouldBe(streamKey);
        item.Sequence.ShouldBe(sequence);
        item.Caller.ShouldBe(typeof(IAsyncEnumerationSource).FullName);
        item.CallerMethod.ShouldBe(method);
        item.HistoryDepth.ShouldBe(2);
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
