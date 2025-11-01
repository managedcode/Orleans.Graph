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
}
