using ManagedCode.Orleans.Graph.Interfaces;
using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainA : Grain, IGrainA
{
    public async Task<int> MethodA1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodB1(int input)
    {
        return await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
            .MethodB1(input);
    }

    public async Task<int> MethodB2(int input)
    {
        return await GrainFactory.GetGrain<IGrainB>(this.GetPrimaryKeyString())
            .MethodC2(input);
    }

    public async Task<int> MethodC1(int input)
    {
        return await GrainFactory.GetGrain<IGrainC>(this.GetPrimaryKeyString())
            .MethodC1(input);
    }

    public async Task<int> MethodComplexFlow(int input)
    {
        return await RunBranchingFlowAsync(input);
    }

    public async Task<int> MethodGrainOnlyComplexFlow(int input)
    {
        await ClearRuntimeGraphTelemetryAsync();

        return await RunBranchingFlowAsync(input);
    }

    private async Task<int> RunBranchingFlowAsync(int input)
    {
        var grainKey = this.GetPrimaryKeyString();
        var fromB = await GrainFactory.GetGrain<IGrainB>(grainKey)
            .MethodB1(input);
        var fromC = await GrainFactory.GetGrain<IGrainC>(grainKey)
            .MethodBranchingFlow(fromB);

        return await GrainFactory.GetGrain<IGrainD>(grainKey)
            .MethodE2(fromC);
    }

    private async Task ClearRuntimeGraphTelemetryAsync()
    {
        await Task.Delay(100);
        await GrainFactory.GetGrain<IOrleansGraphTelemetryWorker>(Constants.LiveGraphTelemetryGrainKey).FlushAsync();
        await GrainFactory.GetGrain<IOrleansGraphTelemetryGrain>(Constants.LiveGraphTelemetryGrainKey).ClearAsync();
    }
}
