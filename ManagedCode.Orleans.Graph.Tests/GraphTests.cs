using ManagedCode.Orleans.Graph.Tests.Cluster;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace ManagedCode.Orleans.Graph.Tests;

[Collection(nameof(TestClusterApplication))]
public class GraphTests(TestClusterApplication testApp, ITestOutputHelper outputHelper)
{
    private readonly TestClusterApplication _testApp = testApp;

    [Fact]
    public async Task GrainA_A1Test()
    {
        await _testApp.Cluster
            .Client
            .GetGrain<IGrainA>("1")
            .MethodA1(1);
    }

    [Fact]
    public async Task GrainB_B1Test()
    {
        await _testApp.Cluster
            .Client
            .GetGrain<IGrainB>("1")
            .MethodB1(1);
    }

    [Fact]
    public async Task GrainC_C1Test()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _testApp.Cluster
                .Client
                .GetGrain<IGrainC>("1")
                .MethodC1(1);
        });

        Assert.StartsWith(
            "Transition from ORLEANS_GRAIN_CLIENT to ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces.IGrainC is not allowed.",
            exception.Message);
    }

    [Fact]
    public async Task GrainA_B1Test()
    {
        await _testApp.Cluster
            .Client
            .GetGrain<IGrainA>("1")
            .MethodB1(1);
    }

    [Fact]
    public async Task GrainACallsTest()
    {
        await _testApp.Cluster
            .Client
            .GetGrain<IGrainA>("1")
            .MethodA1(1);

        await _testApp.Cluster
            .Client
            .GetGrain<IGrainA>("1")
            .MethodB1(1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _testApp.Cluster
                .Client
                .GetGrain<IGrainA>("1")
                .MethodC1(1);
        });

        Assert.StartsWith("Transition from", exception.Message);
    }

    [Fact]
    public async Task DeadLock_Tests()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _testApp.Cluster
                .Client
                .GetGrain<IGrainA>("1")
                .MethodB2(1);
        });

        Assert.StartsWith("Deadlock detected.", exception.Message);
    }
}
