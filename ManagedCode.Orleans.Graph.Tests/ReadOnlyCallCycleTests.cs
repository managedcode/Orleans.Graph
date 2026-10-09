using ManagedCode.Orleans.Graph.Tests.Cluster.Grains;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<TestRuntimeGraphClusterApplication>(Shared = SharedType.PerTestSession)]
public class ReadOnlyCallCycleTests(TestRuntimeGraphClusterApplication fixture)
{
    [Test]
    public async Task ReadOnlyReturnCanInterleaveWithAnOutstandingReadAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        (await root.ReadThroughPeerAsync()).ShouldBe(ReadOnlyCycleRoot.Value);
    }

    [Test]
    public async Task AlwaysInterleavingReturnCanInterleaveWithAnOutstandingWriteAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        (await root.WriteThroughPeerAsync(true)).ShouldBe(ReadOnlyCycleRoot.Value);
    }

    [Test]
    public async Task ReadOnlyReturnCannotInterleaveWithAnOutstandingWriteAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => root.WriteThroughPeerAsync(false));
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task InterleavingReturnDoesNotHideALaterBlockedCallAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        var failure = await Should.ThrowAsync<InvalidOperationException>(root.WriteThroughPeerAgainAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task AlwaysInterleavingEntryAllowsNormalSelfCallbackAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        (await root.WriteThroughSelfAsync()).ShouldBe(ReadOnlyCycleRoot.Value);
    }

    [Test]
    public async Task AlwaysInterleavingEntryAllowsNormalPeerCallbackAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        (await root.InterleavingWriteThroughPeerAsync()).ShouldBe(ReadOnlyCycleRoot.Value);
    }

    [Test]
    public async Task NormalEntryRejectsNormalPeerCallbackAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        var failure = await Should.ThrowAsync<InvalidOperationException>(root.WriteThroughPeerNormallyAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task InterleavingPeerDoesNotHideBlockingRootWriteAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        var failure = await Should.ThrowAsync<InvalidOperationException>(root.WriteThroughInterleavingPeerAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task InterleavingEntryDoesNotHideLaterBlockingPeerWriteAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        var failure = await Should.ThrowAsync<InvalidOperationException>(root.InterleavingWriteThroughPeerAgainAsync);
        failure.Message.ShouldStartWith("Deadlock detected.");
    }

    [Test]
    public async Task ScopedCallChainReentrancyAllowsNormalPeerCallbackAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IReadOnlyCycleRoot>(Guid.NewGuid().ToString("N"));
        (await root.WriteThroughPeerWithCallChainReentrancyAsync()).ShouldBe(ReadOnlyCycleRoot.Value);
    }

}
