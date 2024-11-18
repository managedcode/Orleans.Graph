using ManagedCode.Orleans.Graph.Tests.Cluster.Grains.Interfaces;

namespace ManagedCode.Orleans.Graph.Tests.Cluster.Grains;

public class GrainE : Grain, IGrainE
{
    public async Task<int> MethodE1(int input)
    {
        return await Task.FromResult(input + 1);
    }

    public async Task<int> MethodD2(int input)
    {
        return await GrainFactory.GetGrain<IGrainD>(this.GetPrimaryKeyString()).MethodD1(input);
    }
}