using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainB : Grain, IGrainB
{
    public async Task<int> MethodB1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodC2(int input)
    {
        return await GrainFactory.GetGrain<IGrainC>(this.GetPrimaryKeyString())
            .MethodA2(input);
    }
}