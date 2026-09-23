using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using ManagedCode.Orleans.Graph.Tests.RuntimeGraphCluster;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<TestRuntimeGraphClusterApplication>(Shared = SharedType.PerTestSession)]
public class GraphCallHistoryIsolationTests(TestRuntimeGraphClusterApplication fixture)
{
    [Test]
    public async Task ConcurrentChildCallsKeepIndependentHistoryBranchesAsync()
    {
        var depths = await fixture.Cluster.Client.GetGrain<IGrainA>(Guid.NewGuid().ToString("N"))
            .ObserveParallelCallHistoryAsync(32);

        depths.Length.ShouldBe(32);
        depths.ShouldAllBe(depth => depth == depths[0]);
        depths[0].ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task OneWayIncomingCallStartsDetachedWaitChainAsync()
    {
        var key = Guid.NewGuid().ToString("N");
        await fixture.Cluster.Client.GetGrain<IGrainA>(key).StartOneWayHistoryProbeAsync();
        var grain = fixture.Cluster.Client.GetGrain<IGrainB>(key);
        int? depth = null;
        for (var attempt = 0; attempt < 50 && depth is null; attempt++)
        {
            depth = await grain.GetOneWayHistoryDepthAsync();
            if (depth is null)
            {
                await Task.Delay(20);
            }
        }

        depth.ShouldBe(0);
    }
}
