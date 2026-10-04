namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

[ClassDataSource<AsyncEnumerationTestCluster>(Shared = SharedType.PerTestSession)]
public sealed class NativeAsyncEnumerationLifetimeTests(AsyncEnumerationTestCluster fixture)
{
    private static readonly TimeSpan _testOperationDeadline = TimeSpan.FromSeconds(10);

    [Test]
    public async Task CancellationAndEarlyDisposalSettleActualNativeEnumeratorsAsync()
    {
        await VerifyCancellationAsync(NewKey());
        await VerifyEarlyDisposalAsync(NewKey());
    }

    private async Task VerifyCancellationAsync(string streamKey)
    {
        using var cancellation = new CancellationTokenSource();
        var source = fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(streamKey);
        var stream = source.CancellableAsync(streamKey, cancellation.Token);
        SetNativeBatchSize(stream, 1);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancelTimestamp = 0L;
        await using (var enumerator = stream.GetAsyncEnumerator(deadline.Token))
        {
            (await enumerator.MoveNextAsync()).ShouldBeTrue();
            cancelTimestamp = TimeProvider.System.GetTimestamp();
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }

        var cancelToSettlement = await WaitForSettlementAsync(streamKey, cancelTimestamp, deadline.Token);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        deadline.IsCancellationRequested.ShouldBeFalse();
        cancelToSettlement.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(10));
        AsyncEnumerationExecutionCounters.Read(streamKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    private async Task VerifyEarlyDisposalAsync(string streamKey)
    {
        var source = fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(streamKey);
        var stream = source.AllowedAsync(streamKey);
        SetNativeBatchSize(stream, 1);
        using var deadline = CreateDeadline();
        var disposalTimestamp = 0L;
        await using (var enumerator = stream.GetAsyncEnumerator(deadline.Token))
        {
            (await enumerator.MoveNextAsync()).ShouldBeTrue();
            disposalTimestamp = TimeProvider.System.GetTimestamp();
        }

        var disposalToSettlement = await WaitForSettlementAsync(streamKey, disposalTimestamp, deadline.Token);
        disposalToSettlement.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(10));
        AsyncEnumerationExecutionCounters.Read(streamKey).ShouldBe(new StreamCounts(1, 1));
        AsyncEnumerationExecutionCounters.Remove(streamKey);
    }

    private static async Task<TimeSpan> WaitForSettlementAsync(string streamKey, long startTimestamp, CancellationToken deadline)
    {
        var maximumElapsed = TimeSpan.FromSeconds(10);
        while (AsyncEnumerationExecutionCounters.Read(streamKey).Settled != 1)
        {
            var remaining = maximumElapsed - TimeProvider.System.GetElapsedTime(startTimestamp);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("Native producer did not settle within ten seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(10, remaining.TotalMilliseconds)), TimeProvider.System, deadline);
        }

        return TimeProvider.System.GetElapsedTime(startTimestamp);
    }

    private static void SetNativeBatchSize(IAsyncEnumerable<StreamItem> stream, int batchSize)
    {
        var nativeRequest = (IAsyncEnumerableRequest<StreamItem>)stream;
        nativeRequest.MaxBatchSize = batchSize;
        nativeRequest.MaxBatchSize.ShouldBe(batchSize);
    }

    private static string NewKey() => Guid.NewGuid().ToString("N");

    private static CancellationTokenSource CreateDeadline() => new(_testOperationDeadline);
}
