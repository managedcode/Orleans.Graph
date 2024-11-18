using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainC : Grain, IGrainC
{
    public async Task<int> MethodC1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodA2(int input)
    {
        return await GrainFactory.GetGrain<IGrainA>(this.GetPrimaryKeyString())
            .MethodA1(input);
    }
}