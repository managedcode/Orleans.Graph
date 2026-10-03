using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<TestRuntimeGraphClusterApplication>(Shared = SharedType.PerTestSession)]
public sealed class MayInterleaveDeadlockTests(TestRuntimeGraphClusterApplication fixture)
{
    [Test]
    public async Task BlockingRequestMayInterleavePredicateAllowsCallbackCycleAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IMayInterleaveCycleRoot>(Guid.NewGuid().ToString("N"));

        (await root.StartCycleAsync(true)).ShouldBe(42);
    }

    [Test]
    public async Task BlockingRequestMayInterleavePredicateRejectsCallbackCycleAsync()
    {
        var root = fixture.Cluster.Client.GetGrain<IMayInterleaveCycleRoot>(Guid.NewGuid().ToString("N"));

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => root.StartCycleAsync(false));
        failure.Message.ShouldStartWith("Deadlock detected.");
    }
}
