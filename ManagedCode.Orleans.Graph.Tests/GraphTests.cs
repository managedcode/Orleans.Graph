using ManagedCode.Orleans.Graph.Tests.Cluster;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests;

[ClassDataSource<TestClusterApplication>(Shared = SharedType.PerTestSession)]
public class GraphTests(TestClusterApplication testApp)
{
    private readonly TestClusterApplication _testApp = testApp;

    [Test]
    public async Task GrainA_A1TestAsync()
    {
        await _testApp.Cluster
            .Client
            .GetGrain<IGrainA>("1")
            .MethodA1(1);
    }

    [Test]
    public async Task GrainB_B1TestAsync()
    {
        await _testApp.Cluster
            .Client
            .GetGrain<IGrainB>("1")
            .MethodB1(1);
    }

    [Test]
    public async Task GrainC_C1TestAsync()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await _testApp.Cluster
                .Client
                .GetGrain<IGrainC>("1")
                .MethodC1(1);
        });

        exception.Message
            .StartsWith("Transition from ORLEANS_GRAIN_CLIENT to ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces.IGrainC is not allowed.", StringComparison.Ordinal)
            .ShouldBeTrue();
    }

    [Test]
    public async Task GrainA_B1TestAsync()
    {
        await _testApp.Cluster
            .Client
            .GetGrain<IGrainA>("1")
            .MethodB1(1);
    }

    [Test]
    public async Task GrainACallsTestAsync()
    {
        await _testApp.Cluster
            .Client
            .GetGrain<IGrainA>("1")
            .MethodA1(1);

        await _testApp.Cluster
            .Client
            .GetGrain<IGrainA>("1")
            .MethodB1(1);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await _testApp.Cluster
                .Client
                .GetGrain<IGrainA>("1")
                .MethodC1(1);
        });

        exception.Message.ShouldStartWith("Transition from");
    }

    [Test]
    public async Task DeadLock_TestsAsync()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await _testApp.Cluster
                .Client
                .GetGrain<IGrainA>("1")
                .MethodB2(1);
        });

        exception.Message.ShouldStartWith("Deadlock detected.");
    }
}
