using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

namespace ManagedCode.Orleans.Graph.Tests.Features.AsyncEnumeration;

[ClassDataSource<TestRuntimeGraphClusterApplication>(Shared = SharedType.PerTestSession)]
public sealed class NativeAsyncEnumerationSchedulingTests(TestRuntimeGraphClusterApplication fixture)
{
    [Test]
    public async Task NativeStreamAllowsOrdinaryCallbacksOnTheFirstAndLaterPullsAsync()
    {
        var source = fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(Guid.NewGuid().ToString("N"));
        var stream = source.CallbacksAsync();
        ((IAsyncEnumerableRequest<int>)stream).MaxBatchSize = 1;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var enumerator = stream.GetAsyncEnumerator(deadline.Token);

        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        enumerator.Current.ShouldBe(1);
        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        enumerator.Current.ShouldBe(2);
        (await enumerator.MoveNextAsync()).ShouldBeFalse();
        (await source.GetCallbackCountAsync()).ShouldBe(2);
    }

    [Test]
    public async Task OrdinaryCallStillRejectsTheSameCallbackBeforeItCanWriteAsync()
    {
        var source = fixture.Cluster.Client.GetGrain<IAsyncEnumerationSource>(Guid.NewGuid().ToString("N"));

        var failure = await Should.ThrowAsync<InvalidOperationException>(source.CallLeafAsync);
        failure.Message.ShouldContain("Deadlock detected");
        (await source.GetCallbackCountAsync()).ShouldBe(0);
    }
}
